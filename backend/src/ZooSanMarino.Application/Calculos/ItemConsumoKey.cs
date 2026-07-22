namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Identifica un ítem de consumo del seguimiento SIN ambigüedad de tabla de origen.
/// Los ítems del metadata/request traen dos ids posibles (contrato camino-1/2 del front):
/// <c>itemInventarioEcuadorId</c> (inventario unificado, item_inventario_ecuador) o
/// <c>catalogItemId</c> (catálogo modelo A, catalogo_items). Ambos rangos numéricos se
/// SOLAPAN, así que aplanarlos a un <c>int</c> obliga a adivinar la tabla y produce
/// descuentos rechazados (o cruzados) por colisión de ids. Esta clave conserva el origen.
/// </summary>
/// <param name="Id">Id del ítem en su tabla de origen.</param>
/// <param name="EsItemInventario">
/// <c>true</c> → <paramref name="Id"/> es <c>item_inventario_ecuador.id</c> (camino 2, pass-through);
/// <c>false</c> → es <c>catalogo_items.id</c> (camino 1, id-mapping A→B por código).
/// </param>
public readonly record struct ItemConsumoKey(int Id, bool EsItemInventario);
