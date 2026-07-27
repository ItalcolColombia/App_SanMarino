// Consultas de lectura del Seguimiento Diario Levante: listados paginados, indicadores semanales
// (fn_indicadores_levante_postura), lectura por id (con metadata sintético legacy), filtros y el
// resultado calculado (sp_recalcular_seguimiento_levante). Partial de SeguimientoLoteLevanteService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoLoteLevanteService
{
    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId)
    {
        // Alcance granular: acceso directo por loteId respeta el scope (fail-closed)
        if (!await _scopeResolver.PermiteLoteAsync(loteId))
            return Array.Empty<SeguimientoLoteLevanteDto>();

        // GetFilteredAsync limita PageSize a 100; hay que paginar para devolver todos los días del lote.
        var baseFilter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoLevante,
            LoteId = loteId.ToString(),
            OrderBy = "Fecha",
            OrderAsc = true
        };
        return await FetchAllLevanteDtoPagesAsync(baseFilter).ConfigureAwait(false);
    }

    /// <summary>
    /// Recorre todas las páginas del listado unificado (máx. 100 filas por página en <see cref="SeguimientoDiarioService"/>).
    /// </summary>
    private async Task<List<SeguimientoLoteLevanteDto>> FetchAllLevanteDtoPagesAsync(SeguimientoDiarioFilterRequest baseFilter)
    {
        const int pageSize = 100;
        var all = new List<SeguimientoLoteLevanteDto>();
        var page = 1;
        long total;
        do
        {
            var filter = baseFilter with { Page = page, PageSize = pageSize };
            var paged = await _seguimientoDiarioService.GetFilteredAsync(filter).ConfigureAwait(false);
            total = paged.Total;
            foreach (var item in paged.Items)
                all.Add(MapToLevanteDto(item));
            if (paged.Items.Count == 0)
                break;
            page++;
        } while (all.Count < total);

        return all;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IndicadorSemanalLevanteDto>> GetIndicadoresSemanalesAsync(int loteId)
    {
        // Alcance granular: acceso directo por loteId respeta el scope (fail-closed)
        if (!await _scopeResolver.PermiteLoteAsync(loteId))
            return Array.Empty<IndicadorSemanalLevanteDto>();

        // Cálculo en la BD (fn_indicadores_levante_postura): el front solo pinta, no calcula.
        return await _ctx.Database
            .SqlQueryRaw<IndicadorSemanalLevanteDto>(
                "SELECT * FROM fn_indicadores_levante_postura({0}::int)", loteId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var u = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (u is null || u.TipoSeguimiento != TipoLevante)
            return null;
        var dto = MapToLevanteDto(u);
        // Alcance granular: el registro se lee por su id, pero pertenece a un lote (fail-closed → 404)
        if (!await _scopeResolver.PermiteLoteAsync(dto.LoteId))
            return null;
        if (dto.Metadata is not null)
            return dto;
        var synthetic = await BuildSyntheticMetadataForLegacyRowAsync(dto, default).ConfigureAwait(false);
        return synthetic is null ? dto : dto with { Metadata = synthetic };
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        // Alcance granular: filtro por un lote puntual respeta el scope (fail-closed). Sin loteId el
        // listado se acota en SeguimientoDiarioService.GetFilteredAsync (mismo cierre de visibilidad).
        if (loteId.HasValue && !await _scopeResolver.PermiteLoteAsync(loteId.Value))
            return Array.Empty<SeguimientoLoteLevanteDto>();

        var baseFilter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoLevante,
            LoteId = loteId?.ToString(),
            FechaDesde = desde,
            FechaHasta = hasta,
            OrderBy = "Fecha",
            OrderAsc = true
        };
        return await FetchAllLevanteDtoPagesAsync(baseFilter).ConfigureAwait(false);
    }

    /// <summary>
    /// Resultado calculado: ejecuta SP y lee ProduccionResultadoLevante.
    /// NOTA: El SP sp_recalcular_seguimiento_levante debe leer de seguimiento_diario (tipo=levante)
    /// en lugar de seguimiento_lote_levante para que los datos coincidan.
    /// </summary>
    public async Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        // Alcance granular: acceso directo por loteId respeta el scope (fail-closed → respuesta vacía,
        // antes incluso de recalcular: un lote fuera del alcance no dispara el SP).
        if (!await _scopeResolver.PermiteLoteAsync(loteId))
            return new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, 0, new List<ResultadoLevanteItemDto>());

        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{loteId}' no existe o no pertenece a la compañía.");

        if (recalcular)
            await _ctx.Database.ExecuteSqlInterpolatedAsync($"select sp_recalcular_seguimiento_levante({loteId})");

        var q = from r in _ctx.ProduccionResultadoLevante.AsNoTracking()
                where r.LoteId == loteId
                select r;
        if (desde.HasValue) q = q.Where(x => x.Fecha >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(x => x.Fecha <= hasta.Value.Date);

        var items = await q.OrderBy(x => x.Fecha)
            .Select(r => new ResultadoLevanteItemDto(
                r.Fecha, r.EdadSemana,
                r.HembraViva, r.MortH, r.SelHOut, r.ErrH,
                r.ConsKgH, r.PesoH, r.UnifH, r.CvH,
                r.MortHPct, r.SelHPct, r.ErrHPct,
                r.MsEhH, r.AcMortH, r.AcSelH, r.AcErrH,
                r.AcConsKgH, r.ConsAcGrH, r.GrAveDiaH,
                r.DifConsHPct, r.DifPesoHPct, r.RetiroHPct, r.RetiroHAcPct,
                r.MachoVivo, r.MortM, r.SelMOut, r.ErrM,
                r.ConsKgM, r.PesoM, r.UnifM, r.CvM,
                r.MortMPct, r.SelMPct, r.ErrMPct,
                r.MsEmM, r.AcMortM, r.AcSelM, r.AcErrM,
                r.AcConsKgM, r.ConsAcGrM, r.GrAveDiaM,
                r.DifConsMPct, r.DifPesoMPct, r.RetiroMPct, r.RetiroMAcPct,
                r.RelMHPct,
                r.PesoHGuia, r.UnifHGuia, r.ConsAcGrHGuia, r.GrAveDiaHGuia, r.MortHPctGuia,
                r.PesoMGuia, r.UnifMGuia, r.ConsAcGrMGuia, r.GrAveDiaMGuia, r.MortMPctGuia,
                r.AlimentoHGuia, r.AlimentoMGuia
            ))
            .ToListAsync();

        return new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, items.Count, items);
    }
}
