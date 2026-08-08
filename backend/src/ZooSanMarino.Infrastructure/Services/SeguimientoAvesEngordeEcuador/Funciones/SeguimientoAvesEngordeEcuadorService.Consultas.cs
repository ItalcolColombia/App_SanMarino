// Consultas de solo lectura del seguimiento diario de aves engorde Ecuador: por id, por lote,
// filtrado, resumen de liquidación, tabla diaria (función SQL) e histórico unificado.
// Partial de SeguimientoAvesEngordeEcuadorService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeEcuadorService
{
    // ─── Consultas estándar ───────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return null;
        // Alcance granular: el registro se lee por su id, pero pertenece a un lote (fail-closed → 404)
        if (!await PermiteLoteEngordeAsync(entity.LoteAveEngordeId)) return null;
        return MapToDto(entity);
    }

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

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(x => x.LoteAveEngordeId == loteId)
            .OrderBy(x => x.Fecha)
            .ToListAsync();

        var seguimientos = list.Select(MapToDto).ToList();
        var historico    = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(
        int? loteId, DateTime? desde, DateTime? hasta)
    {
        // Alcance granular: filtro por un lote puntual respeta el scope (fail-closed)
        if (loteId.HasValue && !await PermiteLoteEngordeAsync(loteId.Value))
            return Array.Empty<SeguimientoLoteLevanteDto>();

        var query = _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking();
        if (loteId.HasValue) query = query.Where(x => x.LoteAveEngordeId == loteId.Value);
        // Día completo en UTC (fechas ancladas a mediodía por FechasPuras)
        var desdeUtc = FechasPuras.AnclarMediodiaUtc(desde)?.AddHours(-12);
        var hastaExcl = FechasPuras.AnclarMediodiaUtc(hasta)?.AddHours(12);
        if (desdeUtc.HasValue)  query = query.Where(x => x.Fecha >= desdeUtc.Value);
        if (hastaExcl.HasValue) query = query.Where(x => x.Fecha < hastaExcl.Value);

        // Alcance granular del listado (sin lote puntual): excluye lotes fuera del cierre del usuario
        query = await AplicarScopeUbicacionAsync(query);

        var entities = await query.OrderBy(x => x.Fecha).ToListAsync();
        return entities.Select(MapToDto);
    }

    // ─── Resumen liquidación ─────────────────────────────────────────────────

    public async Task<LiquidacionLoteEngordeResumenDto?> GetLiquidacionResumenAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → null/404)
        if (!await PermiteLoteEngordeAsync(loteId)) return null;

        // Lote liquidado ⇒ el resumen es la COPIA congelada (lo que se aprobó al liquidar), no un
        // recálculo. Copias de backfill (sin resumen) caen al cálculo en vivo.
        var congelado = await LiquidacionCongeladaAplicador.LeerResumenCongeladoAsync(_ctx, loteId, companyId);
        if (congelado is not null) return congelado;

        // Cuerpo compartido con SeguimientoAvesEngordeService y con el cierre (una sola fórmula).
        return await LiquidacionCongeladaAplicador.CalcularResumenVivoAsync(_ctx, loteId, companyId);
    }

    // ─── Tabla diaria via función SQL ────────────────────────────────────────

    public async Task<IReadOnlyList<SeguimientoDiarioTablaFilaDto>> GetTablaDiariaAsync(int loteId)
    {
        // Alcance granular: acceso directo por lote respeta el scope (fail-closed → tabla vacía)
        if (!await PermiteLoteEngordeAsync(loteId))
            return Array.Empty<SeguimientoDiarioTablaFilaDto>();

        // `SELECT *` a propósito: el contrato de la fn es su RETURNS TABLE y el mapeo es por nombre
        // de columna (snake_case). Las columnas v15 `apertura_alimento_kg` / `apertura_documentos`
        // —el «Ingreso inicial del ciclo»— viajan por acá sin tocar la consulta.
        return await _ctx.Database
            .SqlQueryRaw<SeguimientoDiarioTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_engorde({0}::int)", loteId)
            .ToListAsync();
    }

    // ─── Histórico unificado ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(
        int loteId, int companyId)
    {
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int    farmId   = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

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
}
