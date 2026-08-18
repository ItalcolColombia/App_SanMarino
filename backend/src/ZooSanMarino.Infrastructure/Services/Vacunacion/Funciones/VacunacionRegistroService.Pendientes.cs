// Vacunacion/Funciones/VacunacionRegistroService.Pendientes.cs
// SOLO LECTURA: la bandeja "hoy me toca".
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionRegistroService
{
    /// <inheritdoc />
    /// <remarks>
    /// UN solo round trip: <c>fn_vacunacion_pendientes</c> resuelve alcance, lotes vivos de las 3
    /// líneas, franja y clasificación. La BD filtra, el backend orquesta — traer todo el cronograma
    /// de la empresa para descartarlo en memoria es justo lo que cuelga los endpoints multipaís.
    ///
    /// <para><c>p_hoy</c> viaja como <c>DateTime.UtcNow.Date</c>, la misma base con la que
    /// <c>RegistrarAplicadoAsync</c> sella la fecha: la bandeja y el guardado no pueden discrepar de
    /// día. Sin sesión válida (sin GUID de usuario) la bandeja es vacía: fail-closed.</para>
    ///
    /// <para>Alcance granular (W4): el cierre de ubicación viaja en los mismos 4 arrays que consume
    /// <c>fn_vacunacion_filter_data</c> (<see cref="VacunacionScopeSqlParams"/>) — la bandeja no
    /// puede avisar de un lote que el combo no deja abrir.</para>
    /// </remarks>
    public async Task<List<VacunacionPendienteDto>> GetPendientesAsync(int diasHorizonte, CancellationToken ct = default)
    {
        if (!_currentUser.UserGuid.HasValue) return new List<VacunacionPendienteDto>();

        var horizonte = Math.Clamp(diasHorizonte, 0, 365);

        var pUser = new NpgsqlParameter("p_user_guid", NpgsqlDbType.Uuid) { Value = _currentUser.UserGuid.Value };
        var pCompany = new NpgsqlParameter("p_company_id", NpgsqlDbType.Integer) { Value = _currentUser.CompanyId };
        var pPais = new NpgsqlParameter("p_pais_id", NpgsqlDbType.Integer)
        {
            Value = _currentUser.PaisId.HasValue ? _currentUser.PaisId.Value : DBNull.Value
        };
        var pHoy = new NpgsqlParameter("p_hoy", NpgsqlDbType.Date) { Value = DateTime.UtcNow.Date };
        var pScope = await VacunacionScopeSqlParams.ResolverAsync(_scopeResolver);
        var pHorizonte = new NpgsqlParameter("p_dias_horizonte", NpgsqlDbType.Integer) { Value = horizonte };

        const string sql =
            "SELECT * FROM public.fn_vacunacion_pendientes(@p_user_guid, @p_company_id, @p_pais_id, @p_hoy, " +
            "@p_scope_farm_ids, @p_scope_nucleos, @p_scope_galpones, @p_scope_lotes, @p_dias_horizonte)";

        var rows = await _ctx.Database
            .SqlQueryRaw<VacunacionPendienteRow>(
                sql, new[] { pUser, pCompany, pPais, pHoy }.Concat(pScope).Append(pHorizonte).ToArray())
            .ToListAsync(ct);

        return rows.Select(Mapear).ToList();
    }

    /// <summary>Fila cruda → DTO. Sin lógica: la situación y los días los resolvió la función SQL
    /// (<see cref="VacunacionPendientesCalculos"/> es su especificación ejecutable).</summary>
    private static VacunacionPendienteDto Mapear(VacunacionPendienteRow r) => new(
        r.CronogramaItemId, r.LineaProductiva, r.LoteId, r.LoteNombre,
        r.GranjaId, r.GranjaNombre, r.NucleoId, r.GalponId,
        r.ItemInventarioId, r.ItemInventarioNombre,
        r.UnidadObjetivo, r.ValorObjetivo,
        r.FechaInicioFranja, r.FechaFinFranja,
        r.Situacion, r.Dias);
}
