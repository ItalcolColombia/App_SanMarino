// Vacunacion/VacunacionReportesService.cs
// Partial 'ancla': campos, ctor y helpers compartidos (parámetros de las fns + scoping de granjas).
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Reportes de vacunación: envoltorios C# de fn_vacunacion_cumplimiento_lote y
/// fn_vacunacion_cumplimiento_detalle (backend/sql/). La BD filtra y agrega; acá solo se arman
/// parámetros y se mapean filas.</summary>
public sealed partial class VacunacionReportesService : IVacunacionReportesService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ILocationScopeResolver _scopeResolver;

    public VacunacionReportesService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _scopeResolver = scopeResolver;
    }

    /// <summary>
    /// Alcance granular por granja RESTRINGIDA: devuelve, por granja, el conjunto de lotes VISIBLES
    /// expresados con el id que usa el reporte (el de su línea: lote_postura_levante_id /
    /// lote_postura_produccion_id / lote_ave_engorde_id, igual que <c>p_lote_ids</c> de las fns).
    /// La ubicación sale del propio cronograma (vacunacion_cronograma_item ya guarda granja/núcleo/
    /// galpón); cuando la línea tiene lote de la tabla <c>lotes</c> manda el nivel LOTE del cierre.
    /// Diccionario vacío ⇒ ninguna granja del alcance está restringida ⇒ el reporte no se toca.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, HashSet<int>>> ResolverLotesVisiblesPorGranjaRestringidaAsync(
        int[] granjasPermitidas, CancellationToken ct)
    {
        var restringidos = await _scopeResolver.GetRestrictedScopesAsync(granjasPermitidas);
        if (restringidos.Count == 0) return new Dictionary<int, HashSet<int>>();

        var granjasRestringidas = restringidos.Keys.ToList();
        var items = await _ctx.VacunacionCronogramaItem.AsNoTracking()
            .Where(ci => granjasRestringidas.Contains(ci.GranjaId))
            .Select(ci => new
            {
                ci.GranjaId,
                ci.NucleoId,
                ci.GalponId,
                LineaLoteId = ci.LotePosturaLevanteId ?? ci.LotePosturaProduccionId ?? ci.LoteAveEngordeId,
                LoteDeTablaLotes = ci.LotePosturaLevante != null
                    ? ci.LotePosturaLevante.LoteId
                    : (ci.LotePosturaProduccion != null ? ci.LotePosturaProduccion.LoteId : null)
            })
            .ToListAsync(ct);

        var visibles = granjasRestringidas.ToDictionary(g => g, _ => new HashSet<int>());
        foreach (var it in items)
        {
            if (it.LineaLoteId is not int lineaLoteId) continue;
            var scope = restringidos[it.GranjaId];
            var permitido = it.LoteDeTablaLotes.HasValue
                ? scope.PermiteLote(it.LoteDeTablaLotes.Value)
                : (!string.IsNullOrEmpty(it.GalponId) && scope.PermiteGalpon(it.GalponId))
                  || (string.IsNullOrEmpty(it.GalponId) && !string.IsNullOrEmpty(it.NucleoId) && scope.PermiteNucleo(it.NucleoId));
            if (permitido) visibles[it.GranjaId].Add(lineaLoteId);
        }
        return visibles;
    }

    /// <summary>
    /// Fila del reporte visible: granja no restringida ⇒ pasa; granja restringida ⇒ solo si su lote
    /// está en el conjunto visible de esa granja (fail-closed).
    /// </summary>
    private static bool FilaVisible(IReadOnlyDictionary<int, HashSet<int>> visiblesPorGranja, int granjaId, int loteId)
        => !visiblesPorGranja.TryGetValue(granjaId, out var permitidos) || permitidos.Contains(loteId);

    /// <summary>
    /// Scoping de seguridad: interseca las granjas pedidas con las ASIGNADAS al usuario
    /// (user_farms ∩ farms activas de la empresa), igual que filter-data. Sin granjas asignadas
    /// → array vacío → el reporte sale vacío (nunca "toda la empresa" por omisión).
    /// </summary>
    private async Task<int[]> ResolverGranjasPermitidasAsync(IReadOnlyCollection<int>? solicitadas, CancellationToken ct)
    {
        if (!_currentUser.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");
        var userGuid = _currentUser.UserGuid.Value;

        var asignadas = await _ctx.UserFarms.AsNoTracking()
            .Where(uf => uf.UserId == userGuid)
            .Join(
                _ctx.Farms.AsNoTracking().Where(f => f.DeletedAt == null && f.CompanyId == _currentUser.CompanyId),
                uf => uf.FarmId, f => f.Id, (uf, f) => f.Id)
            .Distinct()
            .ToListAsync(ct);

        return (solicitadas is { Count: > 0 })
            ? asignadas.Where(solicitadas.Contains).ToArray()
            : asignadas.ToArray();
    }

    /// <summary>Los 9 parámetros compartidos por ambas funciones de reporte (misma firma).</summary>
    private NpgsqlParameter[] BuildReporteParams(VacunacionCumplimientoFiltroRequest req, int[] granjasPermitidas)
    {
        return new[]
        {
            new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId },
            new NpgsqlParameter("p_pais_id", NpgsqlDbType.Integer)
            {
                Value = _currentUser.PaisId.HasValue ? _currentUser.PaisId.Value : DBNull.Value
            },
            new NpgsqlParameter("p_granja_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = granjasPermitidas },
            new NpgsqlParameter("p_nucleo_id", NpgsqlDbType.Text)
            {
                Value = string.IsNullOrWhiteSpace(req.NucleoId) ? DBNull.Value : req.NucleoId
            },
            new NpgsqlParameter("p_galpon_id", NpgsqlDbType.Text)
            {
                Value = string.IsNullOrWhiteSpace(req.GalponId) ? DBNull.Value : req.GalponId
            },
            new NpgsqlParameter("p_lote_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer)
            {
                Value = (req.LoteIds is { Count: > 0 }) ? req.LoteIds.ToArray() : (object)DBNull.Value
            },
            new NpgsqlParameter("p_linea_productiva", NpgsqlDbType.Text)
            {
                Value = string.IsNullOrWhiteSpace(req.LineaProductiva) ? DBNull.Value : req.LineaProductiva
            },
            new NpgsqlParameter("p_fecha_desde", NpgsqlDbType.Date)
            {
                Value = req.FechaDesde.HasValue ? req.FechaDesde.Value.Date : DBNull.Value
            },
            new NpgsqlParameter("p_fecha_hasta", NpgsqlDbType.Date)
            {
                Value = req.FechaHasta.HasValue ? req.FechaHasta.Value.Date : DBNull.Value
            },
        };
    }
}
