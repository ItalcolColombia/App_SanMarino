namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Un movimiento de inventario visto por la remediación de duplicados: lo mínimo para agruparlos por
/// firma y decidir cuál sobrevive.
/// </summary>
public sealed record MovimientoDuplicable(
    long Id,
    string Referencia,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemId,
    decimal CantidadKg);

/// <summary>Qué hacer con un movimiento duplicado: se revierte y devuelve estos kg a esta ubicación.</summary>
public sealed record ReversionDuplicado(
    long IdARevertir,
    long IdQueSeConserva,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int ItemId,
    decimal KgADevolver);

/// <summary>
/// Regla de remediación de los consumos que la validación aplicó <b>dos veces</b>.
///
/// <para>
/// El defecto: <c>ValidarAsync</c> leía el estado y las reservas fuera de la transacción, así que dos
/// requests solapadas aplicaban el mismo consumo cada una. Quedan N movimientos idénticos donde
/// debería haber uno solo — mismo <c>reference</c>, ítem, ubicación y cantidad —, contra <b>una sola</b>
/// fila de reserva.
/// </para>
///
/// <para>
/// <b>Se conserva el de menor id y se revierten los demás.</b> El primero es el que corresponde a la
/// llamada que efectivamente ganó la carrera; los siguientes son la duplicación. Conservar el menor
/// —y no el mayor— mantiene además el orden natural del kardex.
/// </para>
///
/// <para>
/// 🔴 <b>No se compensa con un ingreso.</b> Un ingreso suelto aparecería en el histórico como una
/// entrada de alimento de ese día y le mentiría al cuadre del galpón: acá no hubo una entrada, hubo
/// una salida que nunca debió existir. Se revierte el movimiento y se le devuelven los kg al stock de
/// esa ubicación exacta.
/// </para>
/// </summary>
public static class DuplicadosValidacionCalculos
{
    /// <summary>
    /// La firma que hace duplicado a un movimiento. Ubicación incluida: el mismo día y el mismo ítem
    /// en dos galpones distintos son dos consumos legítimos, no una duplicación.
    /// </summary>
    private static (string, int, string, string, int, decimal) Firma(MovimientoDuplicable m) =>
        (m.Referencia ?? "", m.FarmId, Norm(m.NucleoId), Norm(m.GalponId), m.ItemId, m.CantidadKg);

    private static string Norm(string? v) => (v ?? "").Trim();

    /// <summary>
    /// Movimientos a revertir, uno por cada copia sobrante. Devuelve vacío si no hay duplicados —que
    /// es lo que tiene que pasar en cuanto la carrera esté cerrada—.
    /// </summary>
    public static IReadOnlyList<ReversionDuplicado> Reversiones(IEnumerable<MovimientoDuplicable> movimientos)
    {
        var salida = new List<ReversionDuplicado>();

        foreach (var grupo in movimientos.GroupBy(Firma))
        {
            var ordenados = grupo.OrderBy(m => m.Id).ToList();
            if (ordenados.Count < 2) continue;

            var conservado = ordenados[0];
            foreach (var sobrante in ordenados.Skip(1))
            {
                salida.Add(new ReversionDuplicado(
                    sobrante.Id, conservado.Id,
                    sobrante.FarmId, sobrante.NucleoId, sobrante.GalponId, sobrante.ItemId,
                    sobrante.CantidadKg));
            }
        }

        return salida.OrderBy(r => r.IdARevertir).ToList();
    }

    /// <summary>
    /// Kilos a devolver por ubicación e ítem. Es lo que hay que sumarle a <c>inventario_gestion_stock</c>:
    /// borrar el movimiento anula su fila del histórico por trigger, pero <b>no</b> devuelve el stock.
    /// </summary>
    public static IReadOnlyList<(int FarmId, string? NucleoId, string? GalponId, int ItemId, decimal KgADevolver)>
        KgPorUbicacion(IEnumerable<ReversionDuplicado> reversiones) =>
        reversiones
            .GroupBy(r => (r.FarmId, Nucleo: Norm(r.NucleoId), Galpon: Norm(r.GalponId), r.ItemId))
            .Select(g => (g.Key.FarmId,
                          NucleoId: g.First().NucleoId,
                          GalponId: g.First().GalponId,
                          g.Key.ItemId,
                          KgADevolver: g.Sum(r => r.KgADevolver)))
            .OrderBy(x => x.FarmId).ThenBy(x => x.GalponId).ThenBy(x => x.ItemId)
            .ToList();

    /// <summary>Total de kilos descontados de más. Para el reporte, no para la aritmética.</summary>
    public static decimal TotalKgDeMas(IEnumerable<ReversionDuplicado> reversiones) =>
        reversiones.Sum(r => r.KgADevolver);
}
