namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Fase 3 (paso 2) — descuento/devolución automáticos del inventario Colombia en el MODELO B
/// unificado (inventario_gestion_stock / item_inventario_ecuador), a NIVEL GRANJA.
///
/// Reemplaza a <see cref="IFarmInventoryConsumoService"/> (modelo A) para los lotes Colombia. La
/// interfaz recibe los mismos <c>catalogItemId</c> (modelo A) que traen los ítems del seguimiento;
/// internamente los resuelve a <c>item_inventario_ecuador.id</c> (id-mapping por código, company 1/pais 1)
/// y descuenta el stock B (farm, item, nucleo=NULL, galpon=NULL) mediante los métodos aditivos
/// <c>RegistrarConsumoNivelGranjaAsync</c>/<c>RegistrarIngresoNivelGranjaAsync</c> de
/// <see cref="IInventarioGestionService"/> — SIN romper la ruta Ecuador/Panamá (modelo B con
/// núcleo/galpón).
///
/// Igual que FarmInventoryConsumoService: NO abre transacción propia; participa de la
/// <c>IDbContextTransaction</c> externa que abre el servicio de seguimiento (levante/producción)
/// para que el guardado del seguimiento + el descuento sean atómicos (todo-o-nada).
/// </summary>
public interface IColombiaInventarioConsumoService
{
    /// <summary>
    /// Valida que exista stock B suficiente (nivel granja) para TODOS los ítems ANTES de persistir
    /// (bloqueo atómico). Resuelve cada <c>catalogItemId</c> → ítem B; lanza
    /// <see cref="InvalidOperationException"/> por ítem si el ítem no existe/no tiene mapeo o falta
    /// stock. NO muta nada. Llamar antes de guardar el seguimiento.
    /// </summary>
    /// <param name="byCatalogItemId">catalogItemId (modelo A) → kg a consumir (solo &gt; 0 se validan).</param>
    Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, CancellationToken ct = default);

    /// <summary>
    /// Aplica el consumo en B nivel granja: descuenta stock e inserta un movimiento <c>Consumo</c>
    /// por ítem (cantidad &gt; 0). NO abre transacción propia; participa de la externa. Vuelve a
    /// validar stock (defensa) y lanza si insuficiente.
    /// </summary>
    Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, string reference, CancellationToken ct = default);

    /// <summary>
    /// Aplica una devolución en B nivel granja: repone stock e inserta un movimiento <c>Ingreso</c>
    /// por ítem (cantidad &gt; 0). NO abre transacción propia.
    /// </summary>
    Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, string reference, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Aplica el diff old/new de una edición (por catalogItemId): diff &gt; 0 → consumo adicional;
    /// diff &lt; 0 → devolución por |diff|. NO abre transacción propia. La validación previa de los
    /// diff positivos debe hacerse con <see cref="ValidarStockConsumoAsync"/> ANTES de persistir.
    /// </summary>
    Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<int, decimal> oldByCatalogItemId, IReadOnlyDictionary<int, decimal> newByCatalogItemId, string reference, CancellationToken ct = default);
}
