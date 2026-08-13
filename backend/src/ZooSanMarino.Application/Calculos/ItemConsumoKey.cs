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
/// <param name="SiloId">
/// Silo o bodega (<c>farm_silos.id</c>) del que sale el consumo, en las empresas que ubican el
/// inventario por silo. <c>null</c> = comportamiento previo (stock a nivel granja).
/// <para>
/// <b>Es parte de la clave a propósito.</b> El mismo ítem cargado desde dos silos distintos son dos
/// consumos distintos —cada uno descuenta su propia fila de <c>inventario_gestion_stock</c>—, así que
/// aplanarlos a una sola clave sumaría los kg y descontaría todo del primero. Con <c>null</c> el hash
/// y la agrupación quedan idénticos a los de antes de la Fase C.
/// </para>
/// </param>
public readonly record struct ItemConsumoKey(int Id, bool EsItemInventario, int? SiloId = null);
