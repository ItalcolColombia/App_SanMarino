// Vacunacion/VacunacionScopeSqlParams.cs
// Puente entre el cierre de visibilidad (C#) y las funciones SQL del módulo.
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Los 4 parámetros de alcance granular que consumen <c>fn_vacunacion_filter_data</c> y
/// <c>fn_vacunacion_pendientes</c>. Vive acá —y no en cada servicio— para que las dos funciones
/// reciban SIEMPRE el mismo cierre: si una subiera el alcance y la otra no, la bandeja mostraría
/// lotes que los combos ya no dejan elegir.
///
/// <para>El cierre lo calcula <see cref="UserLocationScopeCalculos.ComputeScope"/> (vía el resolver,
/// cacheado por request) y lo aplana <see cref="UserLocationScopeCalculos.AplanarParaSql"/>. La BD
/// sólo prueba pertenencia a conjuntos: la lógica del alcance no se duplica en SQL.</para>
///
/// <para>Usuario sin granjas restringidas ⇒ los 4 arrays van vacíos ⇒ las funciones devuelven
/// exactamente lo de antes de W4 (comportamiento clásico, byte a byte).</para>
/// </summary>
internal static class VacunacionScopeSqlParams
{
    /// <summary>Resuelve el cierre del usuario actual y lo arma como parámetros Npgsql.</summary>
    public static async Task<NpgsqlParameter[]> ResolverAsync(ILocationScopeResolver scopeResolver)
    {
        var restringidos = await scopeResolver.GetAllRestrictedScopesAsync();
        return Construir(UserLocationScopeCalculos.AplanarParaSql(restringidos));
    }

    /// <summary>Aplanado → parámetros. Separado para poder armarlos desde un cierre ya resuelto.</summary>
    public static NpgsqlParameter[] Construir(UserLocationScopeCalculos.ScopeSqlParams plano) => new[]
    {
        new NpgsqlParameter("p_scope_farm_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = plano.FarmIds },
        new NpgsqlParameter("p_scope_nucleos",  NpgsqlDbType.Array | NpgsqlDbType.Text)    { Value = plano.Nucleos },
        new NpgsqlParameter("p_scope_galpones", NpgsqlDbType.Array | NpgsqlDbType.Text)    { Value = plano.Galpones },
        new NpgsqlParameter("p_scope_lotes",    NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = plano.Lotes },
    };
}
