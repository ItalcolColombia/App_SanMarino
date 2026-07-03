namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Fase 2 — descuento/devolución automáticos del inventario Colombia (modelo A:
/// farm_product_inventory / farm_inventory_movements, por catalogItemId).
///
/// A diferencia de FarmInventoryMovementService (UI manual, abre su propia transacción),
/// este servicio NO abre transacción propia: participa de la <c>IDbContextTransaction</c>
/// externa que abre el servicio de seguimiento (levante/producción) para que el guardado
/// del seguimiento + el descuento sean atómicos (todo-o-nada). Comparte el mismo
/// <c>DbContext</c> scoped del request.
///
/// El stock del modelo A NO discrimina por galpón: es por (farm_id, catalog_item_id).
/// </summary>
public interface IFarmInventoryConsumoService
{
    /// <summary>
    /// Valida que exista stock suficiente para TODOS los ítems ANTES de persistir (bloqueo
    /// atómico). Lanza <see cref="InvalidOperationException"/> con mensaje POR ÍTEM si falta
    /// stock o el ítem no existe. NO muta nada. Llamar antes de guardar el seguimiento.
    /// </summary>
    /// <param name="byItemId">catalogItemId → kg a consumir (solo cantidades &gt; 0 se validan).</param>
    Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, CancellationToken ct = default);

    /// <summary>
    /// Aplica el consumo: decrementa farm_product_inventory.Quantity e inserta un movimiento
    /// <c>ConsumoSeguimiento</c> por ítem (cantidad &gt; 0). NO abre transacción propia; participa
    /// de la externa. Vuelve a validar stock por ítem (defensa) y lanza si insuficiente.
    /// </summary>
    Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, string reference, CancellationToken ct = default);

    /// <summary>
    /// Aplica una devolución (repone stock): incrementa farm_product_inventory.Quantity e inserta
    /// un movimiento <c>DevolucionSeguimiento</c> por ítem (cantidad &gt; 0). NO abre transacción propia.
    /// </summary>
    Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, string reference, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Aplica el diff old/new de una edición: por cada ítem, diff = new − old.
    /// diff &gt; 0 → consumo adicional (<c>ConsumoSeguimiento</c>); diff &lt; 0 → devolución
    /// (<c>DevolucionSeguimiento</c> por |diff|). NO abre transacción propia.
    /// La validación previa de los diff positivos debe hacerse con
    /// <see cref="ValidarStockConsumoAsync"/> ANTES de persistir.
    /// </summary>
    Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<int, decimal> oldByItemId, IReadOnlyDictionary<int, decimal> newByItemId, string reference, CancellationToken ct = default);
}
