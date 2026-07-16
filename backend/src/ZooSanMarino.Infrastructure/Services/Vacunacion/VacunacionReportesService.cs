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

    public VacunacionReportesService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

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
