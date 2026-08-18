using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA del rechazo por falta de stock al capturar un consumo de alimento.
///
/// <para>
/// <b>Qué defecto cierra.</b> La misma regla tenía dos tratamientos según el modelo de inventario.
/// Colombia (<c>ModeloBNivelGranja</c>) valida ANTES de persistir y hace rollback, así que bloquea.
/// Ecuador y Panamá (<c>ModeloB</c>, con núcleo y galpón) guardaban el seguimiento PRIMERO y aplicaban
/// el consumo después, dentro de un <c>try/catch</c> que se comía la excepción: se podía cargar un día
/// de consumo de un alimento del que no había un solo kilo, el registro quedaba con sus kg y el
/// inventario intacto. La diferencia aparecía más tarde como descuadre.
/// </para>
///
/// <para>
/// <b>Por qué el motivo se arma acá y no en el service.</b> Porque es lo que lee el usuario, y el
/// mensaje tiene que decirle QUÉ ítem falta y CUÁNTO — no un genérico. El texto es la parte que un
/// refactor rompe sin que nada falle, así que tiene tests propios. El
/// <see cref="StockAtomicoCalculos.MensajeStockInsuficiente"/> se conserva tal cual donde está: es el
/// rechazo de bajo nivel del <c>UPDATE</c> atómico, no el que se le muestra a quien carga el día.
/// </para>
/// </summary>
public static class StockConsumoValidacionCalculos
{
    /// <summary>Un ítem de alimento que el seguimiento quiere consumir.</summary>
    /// <param name="ItemId">Id del ítem de inventario.</param>
    /// <param name="Nombre">Nombre legible; si viene vacío el mensaje cae al id.</param>
    /// <param name="Kg">Kilos pedidos. Los valores ≤ 0 no se validan: no consumen nada.</param>
    public readonly record struct ItemPedido(int ItemId, string? Nombre, decimal Kg);

    /// <summary>
    /// Motivo por el que el consumo NO se puede aplicar, o <c>null</c> si alcanza para todos los
    /// ítems.
    ///
    /// <para>
    /// Un ítem <b>sin fila de stock</b> en esa ubicación y uno <b>con saldo insuficiente</b> se
    /// tratan igual —los dos son «no hay»—, pero el mensaje los distingue: «no tiene stock» contra
    /// «hay 120 kg». Es la diferencia entre «elegiste el alimento equivocado» y «cargá el ingreso
    /// primero», que es lo que la persona necesita saber para resolverlo.
    /// </para>
    /// </summary>
    /// <param name="pedidos">Ítems y kilos que el registro quiere consumir.</param>
    /// <param name="disponiblePorItem">Saldo por ítem en la ubicación. La ausencia de la clave es «sin fila de stock».</param>
    /// <param name="ubicacion">Galpón o granja, para que el mensaje diga DÓNDE falta. Opcional.</param>
    public static string? MotivoStockInsuficiente(
        IEnumerable<ItemPedido> pedidos,
        IReadOnlyDictionary<int, decimal> disponiblePorItem,
        string? ubicacion = null)
    {
        var faltantes = new List<string>();

        foreach (var p in pedidos)
        {
            if (p.Kg <= 0) continue;                     // no consume: no hay nada que validar

            var etiqueta = string.IsNullOrWhiteSpace(p.Nombre)
                ? $"el ítem #{p.ItemId}"
                : $"«{p.Nombre.Trim()}»";

            if (!disponiblePorItem.TryGetValue(p.ItemId, out var disponible))
            {
                faltantes.Add($"{etiqueta} no tiene stock registrado (se piden {Kg(p.Kg)})");
                continue;
            }

            if (disponible < p.Kg)
                faltantes.Add($"{etiqueta}: se piden {Kg(p.Kg)} y hay {Kg(disponible)}");
        }

        if (faltantes.Count == 0) return null;

        var donde = string.IsNullOrWhiteSpace(ubicacion) ? "" : $" en {ubicacion.Trim()}";
        return $"No hay stock suficiente{donde}: {string.Join("; ", faltantes)}. " +
               "Registrá el ingreso del alimento antes de cargar el consumo.";
    }

    /// <summary>
    /// Kilos como los escribe el resto del sistema: hasta 3 decimales, sin ceros de relleno y con
    /// cultura invariante (el mensaje se compara en tests y viaja igual desde cualquier servidor).
    /// </summary>
    private static string Kg(decimal kg) =>
        kg.ToString("0.###", CultureInfo.InvariantCulture) + " kg";
}
