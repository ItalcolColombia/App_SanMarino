// Construcción del parche de metadata (Ingreso/Traslado/Documento/Despacho) desde el histórico
// unificado y backfill masivo de esa metadata para registros existentes.
// Partial de SeguimientoAvesEngordeService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    /// <summary>
    /// Calcula totales de Ingreso/Traslado (alimento) y Despacho (ventas aves)
    /// para el lote+fecha del seguimiento, desde la tabla unificada.
    /// Filtra por el rango de fechas del ciclo de vida del lote para evitar duplicación
    /// de datos de lotes anteriores en el mismo galpón.
    /// </summary>
    private async Task<Dictionary<string, object?>> BuildStockMetadataPatchAsync(int loteId, DateTime fecha)
    {
        var day = fecha.Date;
        var companyId = _current.CompanyId;

        // Calcular rango de fechas del lote (ciclo de vida) para aislar de lotes previos
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId
                && x.LoteAveEngordeId == loteId
                && x.FechaOperacion == day
                && !x.Anulado
                && !((x.Referencia != null && x.Referencia.Contains("devolución por eliminación"))
                     || (x.Referencia != null && x.Referencia.Contains("devolucion por eliminacion")))
                // Excluir INV_INGRESO de devoluciones por edición de seguimiento para que
                // ingresoAlimento en metadata no aparezca inflado.
                && !(x.TipoEvento == "INV_INGRESO"
                     && x.Referencia != null
                     && x.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (x.TipoEvento == "INV_INGRESO"
                    || x.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || x.TipoEvento == "VENTA_AVES"));

        // Aplicar filtro de rango de fechas (ciclo de vida del lote)
        if (fechaMinSeg.HasValue)
            query = query.Where(x => x.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(x => x.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var agg = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                IngresoKg = g.Sum(x => x.TipoEvento == "INV_INGRESO" ? (x.CantidadKg ?? 0m) : 0m),
                TrasladoKg = g.Sum(x => x.TipoEvento == "INV_TRASLADO_ENTRADA" ? (x.CantidadKg ?? 0m) : 0m),
                DespachoH = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadHembras ?? 0) : 0),
                DespachoM = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadMachos ?? 0) : 0),
                Documento = g
                    .Where(x => x.TipoEvento == "INV_INGRESO")
                    .Select(x => x.NumeroDocumento ?? x.Referencia)
                    .Max()
            })
            .SingleOrDefaultAsync();

        var patch = new Dictionary<string, object?>();
        if (agg is null) return patch;

        if (agg.IngresoKg > 0)
        {
            var s = FormatKg(agg.IngresoKg);
            patch["ingresoAlimento"] = s;
            patch["ingreso_alimento"] = s;
            patch["ingresoAlimentoKg"] = agg.IngresoKg;
        }

        if (agg.TrasladoKg > 0)
        {
            var s = FormatKg(agg.TrasladoKg);
            patch["traslado"] = s;
            patch["notaTraslado"] = s;
            patch["trasladoAlimento"] = s;
        }

        if (!string.IsNullOrWhiteSpace(agg.Documento))
        {
            var d = agg.Documento.Trim();
            patch["documento"] = d;
            patch["documentoAlimento"] = d;
            patch["nroDocumento"] = d;
            patch["numeroDocumento"] = d;
        }

        if (agg.DespachoH > 0)
        {
            patch["despachoHembras"] = agg.DespachoH;
            patch["despachoH"] = agg.DespachoH;
            patch["despacho_hembra"] = agg.DespachoH;
        }

        if (agg.DespachoM > 0)
        {
            patch["despachoMachos"] = agg.DespachoM;
            patch["despachoM"] = agg.DespachoM;
            patch["despacho_macho"] = agg.DespachoM;
        }

        return patch;
    }

    public async Task<SeguimientoAvesEngordeBackfillResultDto> BackfillMetadataAsync(
        int loteId,
        DateTime? desde,
        DateTime? hasta,
        bool onlyIfMissing = true)
    {
        var companyId = _current.CompanyId;

        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            throw new InvalidOperationException($"Lote aves de engorde '{loteId}' no existe o no pertenece a la compañía.");

        var q = _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId);
        // Día completo en UTC (fechas ancladas a mediodía por FechasPuras)
        var desdeUtc = FechasPuras.AnclarMediodiaUtc(desde)?.AddHours(-12);
        var hastaExcl = FechasPuras.AnclarMediodiaUtc(hasta)?.AddHours(12);
        if (desdeUtc.HasValue) q = q.Where(s => s.Fecha >= desdeUtc.Value);
        if (hastaExcl.HasValue) q = q.Where(s => s.Fecha < hastaExcl.Value);

        var list = await q.OrderBy(s => s.Fecha).ToListAsync();
        var total = list.Count;

        var actualizados = 0;
        var omitidos = 0;
        var sinDatosHistorico = 0;

        foreach (var s in list)
        {
            if (onlyIfMissing && MetadataYaTieneCamposKardex(s.Metadata))
            {
                omitidos++;
                continue;
            }

            var patch = await BuildStockMetadataPatchAsync(loteId, s.Fecha.Date);
            if (patch.Count == 0)
            {
                sinDatosHistorico++;
                omitidos++;
                continue;
            }

            s.Metadata = MergeMetadataWithPatch(s.Metadata, patch);
            _ctx.Entry(s).Property(x => x.Metadata).IsModified = true;
            actualizados++;
        }

        if (actualizados > 0)
            await _ctx.SaveChangesAsync();

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        return new SeguimientoAvesEngordeBackfillResultDto(
            LoteId: loteId,
            Desde: desde?.Date,
            Hasta: hasta?.Date,
            TotalRegistros: total,
            Actualizados: actualizados,
            Omitidos: omitidos,
            SinDatosHistorico: sinDatosHistorico);
    }
}
