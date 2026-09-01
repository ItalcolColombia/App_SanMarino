// src/ZooSanMarino.Application/Calculos/HistorialTrasladoLoteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Resolucion del nombre de quien registro un traslado de lote. Logica PURA: recibe las filas de
/// <c>users</c> ya traidas por el service y devuelve el mapa; no toca EF ni estado.
/// </summary>
/// <remarks>
/// <b>Por que hace falta un mapeo y no un JOIN.</b> <c>historial_traslado_lote.created_by_user_id</c>
/// es <c>int</c> y <c>users.id</c> es <c>Guid</c>: no hay FK posible. El puente real —ya usado por
/// <c>LoteBaseEngordeService</c> y por el modulo ItalJira— es que ese entero es la <b>cedula</b> del
/// usuario, guardada como texto. Hasta hoy el historial de traslados no lo sabia y pintaba el
/// literal <c>"Usuario ID: 12345"</c> con un TODO al lado.
///
/// <b>Fail-soft a proposito:</b> un id que no corresponde a ninguna cedula devuelve <c>null</c>, no
/// una excepcion ni un texto inventado. Pasa de verdad —hay filas cuyo <c>created_by_user_id</c> es
/// un hash del id de usuario y no una cedula—, y en ese caso la pantalla muestra el guion.
/// </remarks>
public static class HistorialTrasladoLoteCalculos
{
    /// <summary>
    /// Arma el mapa <c>created_by_user_id → "Nombre Apellido"</c> a partir de las filas de usuarios.
    /// Descarta cedulas no numericas (no pueden ser el <c>int</c> del historial) y nombres vacios
    /// (un mapa con <c>" "</c> haria que la UI muestre un blanco en vez de su guion).
    /// </summary>
    public static Dictionary<int, string> NombresPorCedula(
        IEnumerable<(string? Cedula, string? FirstName, string? SurName)> usuarios)
    {
        var mapa = new Dictionary<int, string>();
        if (usuarios is null) return mapa;

        foreach (var u in usuarios)
        {
            if (!int.TryParse(u.Cedula, out var cedula)) continue;

            var nombre = $"{u.FirstName} {u.SurName}".Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            mapa[cedula] = nombre;
        }

        return mapa;
    }

    /// <summary>
    /// Cedulas a consultar: distintas, en texto (asi se guardan) y sin el 0, que es el valor con el
    /// que <c>fn_mover_lote</c> escribe cuando no hay usuario y jamas va a matchear.
    /// </summary>
    public static List<string> CedulasAConsultar(IEnumerable<int> userIds) =>
        (userIds ?? Enumerable.Empty<int>())
            .Where(id => id != 0)
            .Distinct()
            .Select(id => id.ToString())
            .ToList();

    /// <summary>Nombre del usuario, o <c>null</c> si su id no corresponde a ninguna cedula.</summary>
    public static string? ResolverNombre(IReadOnlyDictionary<int, string> mapa, int userId) =>
        mapa is not null && mapa.TryGetValue(userId, out var nombre) ? nombre : null;
}
