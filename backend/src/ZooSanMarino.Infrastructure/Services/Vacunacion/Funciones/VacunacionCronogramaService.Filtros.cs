// Vacunacion/Funciones/VacunacionCronogramaService.Filtros.cs
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService
{
    /// <inheritdoc />
    /// <remarks>UN solo round trip: fn_vacunacion_filter_data resuelve granjas asignadas (lite),
    /// lotes de las 3 líneas, vacunas y usuarios de la empresa como jsonb (antes eran 5+ consultas
    /// secuenciales con el FarmDto completo). El parse es puro (VacunacionFilterDataJson).</remarks>
    public async Task<VacunacionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        var pUser = new NpgsqlParameter("p_user_guid", NpgsqlDbType.Uuid) { Value = _currentUser.UserGuid.Value };
        var pCompany = new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId };
        var pPais = new NpgsqlParameter("p_pais_id", NpgsqlDbType.Integer)
        {
            Value = _currentUser.PaisId.HasValue ? _currentUser.PaisId.Value : DBNull.Value
        };

        const string sql =
            "SELECT public.fn_vacunacion_filter_data(@p_user_guid, @p_company_id, @p_pais_id)::text AS \"Value\"";

        var json = (await _ctx.Database
            .SqlQueryRaw<string>(sql, pUser, pCompany, pPais)
            .ToListAsync(ct)).FirstOrDefault();

        return VacunacionFilterDataJson.Parse(json);
    }
}
