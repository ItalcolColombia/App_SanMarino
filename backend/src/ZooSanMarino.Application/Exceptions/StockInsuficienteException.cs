namespace ZooSanMarino.Application.Exceptions;

/// <summary>
/// No hay stock suficiente para aplicar un consumo de inventario (validación previa o descuento
/// atómico — ver <c>InventarioGestionService</c>/<c>ColombiaInventarioConsumoService</c>).
///
/// <para>
/// Hereda de <see cref="InvalidOperationException"/> a propósito: cualquier <c>catch</c> existente de
/// ese tipo la sigue atrapando (los controllers la traducen a 400 igual que antes), así que
/// introducirla no cambia el comportamiento de nada por sí sola.
/// </para>
///
/// <para>
/// <b>Por qué existe un tipo dedicado en vez de comparar el mensaje.</b> F7 del plan
/// <c>descuento_inventario_movil_plan.md</c> necesita distinguir "no hay stock" (divergencia
/// recuperable: <c>SyncPushService</c> reintenta el push sin los ítems y marca
/// <c>requiere_cuadre</c>) de cualquier OTRA regla de negocio ("el lote está cerrado", "fecha
/// inválida") que sigue siendo un rechazo definitivo. El propio plan lo dice: <i>"la política no
/// puede viajar en la excepción... tiene que actuar en la decisión, antes del throw"</i> — un tipo
/// dedicado es esa decisión tomada en el punto exacto donde se sabe que falta stock, sin que
/// <c>SyncPushService</c> tenga que adivinar parseando texto en español.
/// </para>
/// </summary>
public class StockInsuficienteException : InvalidOperationException
{
    public StockInsuficienteException(string message) : base(message) { }
}
