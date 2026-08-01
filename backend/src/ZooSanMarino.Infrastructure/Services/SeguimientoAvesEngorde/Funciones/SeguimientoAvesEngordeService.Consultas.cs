// Consultas de solo lectura del seguimiento diario de aves de engorde: listado por lote,
// historial unificado, liquidación, filtros y resultado. Partial de SeguimientoAvesEngordeService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    public async Task<SeguimientoAvesEngordePorLoteResponseDto> GetByLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → misma forma vacía)
        if (!await PermiteLoteEngordeAsync(loteId))
            return new SeguimientoAvesEngordePorLoteResponseDto(
                Array.Empty<SeguimientoLoteLevanteDto>(),
                Array.Empty<LoteRegistroHistoricoUnificadoDto>());

        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            return new SeguimientoAvesEngordePorLoteResponseDto(
                Array.Empty<SeguimientoLoteLevanteDto>(),
                Array.Empty<LoteRegistroHistoricoUnificadoDto>());

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        foreach (var s in list)
            SanitizeContaminatedDocumentMetadata(s);
        var seguimientos = list.Select(MapToDto).ToList();

        var historico = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<LoteRegistroHistoricoUnificadoDto>> GetHistoricoUnificadoPorLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → vacío)
        if (!await PermiteLoteEngordeAsync(loteId))
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists) return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        return await QueryHistoricoUnificadoDtosAsync(loteId, companyId);
    }

    public async Task<LiquidacionLoteEngordeResumenDto?> GetLiquidacionResumenAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → null/404)
        if (!await PermiteLoteEngordeAsync(loteId)) return null;

        var existe = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!existe) return null;

        // Lote liquidado ⇒ el resumen es la COPIA congelada (lo que se aprobó al liquidar), no un
        // recálculo. Copias de backfill (sin resumen) caen al cálculo en vivo.
        var congelado = await LiquidacionCongeladaAplicador.LeerResumenCongeladoAsync(_ctx, loteId, companyId);
        if (congelado is not null) return congelado;

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        // Cuerpo compartido con el service Ecuador y con el cierre (una sola fórmula).
        return await LiquidacionCongeladaAplicador.CalcularResumenVivoAsync(_ctx, loteId, companyId);
    }

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(int loteId, int companyId)
    {
        // Resolve the lote's physical location (granja / nucleo / galpon).
        // This is the source of truth used to filter by event type:
        //   - VENTA_AVES       → lote level  (lote_ave_engorde_id)
        //   - food movements   → galpon level (farm_id + nucleo_id + galpon_id)
        //     (food is received at galpon level; lote_ave_engorde_id may be NULL if the
        //      trigger ran before the lote was created — this covers that case too)
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int farmId      = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        // Calcular rango de fechas del ciclo de vida del lote:
        // Límite inferior: min(fecha de seguimiento) — Límite superior: max(fecha de seguimiento)
        // Esto aísla el histórico del lote actual de registros de lotes previos en el mismo galpón.
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                // Excluir INV_INGRESO generados por el sistema de seguimiento (devoluciones
                // por edición a la baja). Estos ingresos son reversiones contables del
                // inventario físico y no deben mostrarse como "ingreso de alimento" en la
                // tabla diaria; su ausencia del filtro haría que ingresoKg apareciera inflado.
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    // Bird sales: scoped to the specific lote
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    // Food movements: scoped to the galpon regardless of lote assignment
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

        // Aplicar filtro de rango de fechas (ciclo de vida del lote)
        if (fechaMinSeg.HasValue)
            query = query.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var rows = await query
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        return rows.Select(MapHistoricoUnificado).ToList();
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                       where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        if (e is null) return null;
        // Alcance granular: el registro se lee por su id, pero pertenece a un lote (fail-closed → 404)
        if (!await PermiteLoteEngordeAsync(e.LoteAveEngordeId)) return null;
        return MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        // Alcance granular: filtro por un lote puntual respeta el scope (fail-closed)
        if (loteId.HasValue && !await PermiteLoteEngordeAsync(loteId.Value))
            return Array.Empty<SeguimientoLoteLevanteDto>();

        // Rango por DÍA completo en UTC: las fechas van ancladas a mediodía UTC (FechasPuras),
        // así que un "hasta" a medianoche excluiría los registros de ese mismo día.
        var desdeUtc = FechasPuras.AnclarMediodiaUtc(desde)?.AddHours(-12);
        var hastaExcl = FechasPuras.AnclarMediodiaUtc(hasta)?.AddHours(12);
        var q = from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                where l.CompanyId == companyId && l.DeletedAt == null
                   && (!loteId.HasValue || s.LoteAveEngordeId == loteId.Value)
                   && (!desdeUtc.HasValue || s.Fecha >= desdeUtc.Value)
                   && (!hastaExcl.HasValue || s.Fecha < hastaExcl.Value)
                orderby s.Fecha
                select s;

        // Alcance granular del listado (sin lote puntual): excluye lotes fuera del cierre del usuario
        var qScoped = await AplicarScopeUbicacionAsync(q);

        var list = await qScoped.ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → respuesta vacía)
        if (!await PermiteLoteEngordeAsync(loteId))
            return new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, 0, new List<ResultadoLevanteItemDto>());

        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{loteId}' no existe o no pertenece a la compañía.");

        return await Task.FromResult(new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, 0, new List<ResultadoLevanteItemDto>()));
    }
}
