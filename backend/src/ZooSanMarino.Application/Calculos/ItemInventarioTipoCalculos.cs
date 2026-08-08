// src/ZooSanMarino.Application/Calculos/ItemInventarioTipoCalculos.cs
// Criterio ÚNICO de "qué tipo de ítem de inventario es esto". Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Tipo de un ítem de inventario (alimento, vacuna, medicamento, …) y cómo se resuelve.
/// <para>
/// <b>Dónde vive el dato.</b> La fuente de verdad es la COLUMNA <c>catalogo_items.item_type</c>
/// (<c>NOT NULL</c>, default <c>alimento</c>, con los índices <c>ix_catalogo_items_item_type</c>,
/// <c>ix_catalogo_items_company_type</c> y <c>ix_catalogo_items_company_type_activo</c>). La creó
/// <c>backend/sql/add_item_type_catalogo.sql</c> precisamente para reemplazar a la clave jsonb
/// <c>metadata-&gt;&gt;'type_item'</c>, copiando los valores y poniéndola <c>NOT NULL</c>.
/// </para>
/// <para>
/// ⚠️ <b>El jsonb quedó VESTIGIAL: no lo leas.</b> <c>CatalogItemService</c> escribe la columna y NO
/// escribe el metadata, así que todo ítem creado desde la UI moderna tiene el jsonb vacío — hoy está
/// NULL en el 80 % del catálogo. El Reporte Contable se quedó leyéndolo hasta ago-2026 y por eso no
/// veía 257 movimientos de alimento (la granja 20 entera, 236, más 19 de la granja 5): sus ítems
/// tenían el tipo correcto en la columna y el jsonb vacío.
/// </para>
/// </summary>
public static class ItemInventarioTipoCalculos
{
    /// <summary>Tipo canónico del alimento, en la capitalización con la que lo escribe el service.</summary>
    public const string TipoAlimento = "alimento";

    /// <summary>
    /// ¿Este tipo es alimento? Compara sin distinguir mayúsculas y tolerando espacios, porque el
    /// catálogo tiene filas cargadas como <c>"Alimento"</c> y el resto del sistema
    /// (<c>InventarioGastoService</c>, <c>InventarioGestionService</c>, los DTOs de seguimiento) ya
    /// compara así. Es una comparación de LECTURA: no normaliza ni escribe nada, así que no puede
    /// crear duplicados de catálogo.
    /// </summary>
    public static bool EsTipoAlimento(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) &&
        string.Equals(tipo.Trim(), TipoAlimento, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tipo efectivo de un movimiento de inventario: manda el grabado en el propio movimiento y el
    /// catálogo es el respaldo.
    /// <para>
    /// Es el mismo criterio que ya aplica <c>FarmInventoryMovementService</c> al proyectar sus DTOs
    /// (<c>m.ItemType ?? m.CatalogItem.ItemType</c>). Que mande el del movimiento preserva la
    /// historia: si un ítem del catálogo cambia de tipo, los movimientos viejos conservan el que
    /// tenían cuando ocurrieron.
    /// </para>
    /// </summary>
    public static string? TipoEfectivo(string? tipoDelMovimiento, string? tipoDelCatalogo) =>
        !string.IsNullOrWhiteSpace(tipoDelMovimiento) ? tipoDelMovimiento : tipoDelCatalogo;

    /// <summary>
    /// ¿Este movimiento es de alimento? Combina <see cref="TipoEfectivo"/> con
    /// <see cref="EsTipoAlimento"/>.
    /// </summary>
    public static bool EsMovimientoDeAlimento(string? tipoDelMovimiento, string? tipoDelCatalogo) =>
        EsTipoAlimento(TipoEfectivo(tipoDelMovimiento, tipoDelCatalogo));
}
