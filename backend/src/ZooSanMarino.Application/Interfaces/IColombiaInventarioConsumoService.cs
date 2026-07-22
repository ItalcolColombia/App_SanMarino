using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Fase 3 (paso 2) — descuento/devolución automáticos del inventario Colombia en el MODELO B
/// unificado (inventario_gestion_stock / item_inventario_ecuador), a NIVEL GRANJA.
///
/// Reemplaza a <see cref="IFarmInventoryConsumoService"/> (modelo A) para los lotes Colombia. La
/// interfaz recibe las claves <see cref="ItemConsumoKey"/> de los ítems del seguimiento, que
/// conservan el origen del id (contrato camino-1/2 del front): <c>catalogItemId</c> (modelo A)
/// se resuelve a <c>item_inventario_ecuador.id</c> por código, y <c>itemInventarioEcuadorId</c>
/// (inventario nuevo) se acepta directo. Ambos caminos se validan contra la EMPRESA EFECTIVA de
/// la granja del lote (farms.company_id) + país Colombia — soporta multi-empresa (Sanmarino,
/// Demo, etc.), no solo company 1. Descuenta el stock B (farm, item, nucleo=NULL, galpon=NULL)
/// mediante los métodos aditivos <c>RegistrarConsumoNivelGranjaAsync</c>/
/// <c>RegistrarIngresoNivelGranjaAsync</c> de <see cref="IInventarioGestionService"/> — SIN
/// romper la ruta Ecuador/Panamá (modelo B con núcleo/galpón).
///
/// Igual que FarmInventoryConsumoService: NO abre transacción propia; participa de la
/// <c>IDbContextTransaction</c> externa que abre el servicio de seguimiento (levante/producción)
/// para que el guardado del seguimiento + el descuento sean atómicos (todo-o-nada).
/// </summary>
public interface IColombiaInventarioConsumoService
{
    /// <summary>
    /// Valida que exista stock B suficiente (nivel granja) para TODOS los ítems ANTES de persistir
    /// (bloqueo atómico). Resuelve cada <see cref="ItemConsumoKey"/> → ítem B; lanza
    /// <see cref="InvalidOperationException"/> por ítem si el ítem no existe/no tiene mapeo o falta
    /// stock. NO muta nada. Llamar antes de guardar el seguimiento.
    /// </summary>
    /// <param name="byItem">clave del ítem (camino 1/2) → kg a consumir (solo &gt; 0 se validan).</param>
    Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, CancellationToken ct = default);

    /// <summary>
    /// Aplica el consumo en B nivel granja: descuenta stock e inserta un movimiento <c>Consumo</c>
    /// por ítem (cantidad &gt; 0). NO abre transacción propia; participa de la externa. Vuelve a
    /// validar stock (defensa) y lanza si insuficiente.
    /// </summary>
    Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, string reference, CancellationToken ct = default);

    /// <summary>
    /// Aplica una devolución en B nivel granja: repone stock e inserta un movimiento <c>Ingreso</c>
    /// por ítem (cantidad &gt; 0). NO abre transacción propia.
    /// </summary>
    Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, string reference, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Aplica el diff old/new de una edición (por clave de ítem): diff &gt; 0 → consumo adicional;
    /// diff &lt; 0 → devolución por |diff|. NO abre transacción propia. La validación previa de los
    /// diff positivos debe hacerse con <see cref="ValidarStockConsumoAsync"/> ANTES de persistir.
    /// </summary>
    Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> oldByItem, IReadOnlyDictionary<ItemConsumoKey, decimal> newByItem, string reference, CancellationToken ct = default);
}
