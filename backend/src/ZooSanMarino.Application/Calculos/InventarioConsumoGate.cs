// Gate PURO (sin EF/estado) para decidir si un seguimiento debe descontar del
// inventario "modelo B" (inventario_gestion_stock / item_inventario_ecuador).
//
// Contexto del bug (Fase 1 / S1): el consumo desde seguimientos usa el parser
// MetadataEngordeCalculos.ParseMetadataItemsToKg, que hace fallback
// catalogItemId -> itemInventarioEcuadorId. Para lotes de Colombia (modelo A) los
// ítems traen SOLO catalogItemId; si ese id colisiona con un item_inventario_ecuador.id
// real con stock, se descuenta del inventario del país equivocado (Ecuador) en silencio.
//
// El fix se aplica AGUAS ARRIBA (a nivel de servicio): solo se invoca
// RegistrarConsumoAsync/RegistrarIngresoAsync cuando el país del LOTE es Ecuador o
// Panamá (los países que operan sobre el modelo B). En Ecuador/Panamá el fallback
// catalogItemId==itemInventarioEcuadorId es correcto, por eso NO se toca el parser
// (que tiene un test verde fijando ese comportamiento).
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Modelo de inventario al que debe descontar un seguimiento, según el país del lote.
/// </summary>
public enum ModeloInventarioConsumo
{
    /// <summary>El país no descuenta de ningún inventario automáticamente.</summary>
    Ninguno = 0,
    /// <summary>Modelo B (Ecuador/Panamá): inventario_gestion / item_inventario_ecuador, con ubicación núcleo/galpón (alimento exige galpón).</summary>
    ModeloB = 1,
    /// <summary>Modelo A (Colombia, Fase 2 — histórico): farm_product_inventory / farm_inventory_movements por catalogItemId. Fase 3 dejó de usarlo para Colombia.</summary>
    ModeloA = 2,
    /// <summary>
    /// Modelo B a NIVEL GRANJA (Colombia, Fase 3 paso 2): inventario_gestion / item_inventario_ecuador
    /// unificado, pero con stock/movimientos por (granja, ítem) sin núcleo/galpón. El ítem se resuelve
    /// desde el catalogItemId (id-mapping A→B por código). Unifica con Ecuador/Panamá SIN exigir galpón.
    /// </summary>
    ModeloBNivelGranja = 3
}

public static class InventarioConsumoGate
{
    /// <summary>Id de país Colombia en la tabla <c>paises</c>.</summary>
    public const int PaisColombia = 1;

    /// <summary>Id de país Ecuador en la tabla <c>paises</c>.</summary>
    public const int PaisEcuador = 2;

    /// <summary>Id de país Panamá en la tabla <c>paises</c>.</summary>
    public const int PaisPanama = 3;

    /// <summary>
    /// Devuelve <c>true</c> solo cuando el país del lote opera sobre el inventario
    /// modelo B (Ecuador o Panamá). Para Colombia (o país desconocido/null) devuelve
    /// <c>false</c> → NO se descuenta del modelo B, cerrando el descuento cross-país silencioso.
    /// </summary>
    /// <param name="paisIdDelLote">
    /// País efectivo del lote. Debe resolverse aguas arriba por la fuente más robusta:
    /// <c>lote.PaisId</c> si está poblado; si no, derivado desde la granja del lote
    /// (farm.DepartamentoId → departamentos.PaisId), la misma cadena que usa el inventario.
    /// </param>
    public static bool DebeDescontarModeloB(int? paisIdDelLote)
        => paisIdDelLote is PaisEcuador or PaisPanama;

    /// <summary>
    /// Despacho por país: a qué modelo de inventario descuenta el seguimiento.
    /// Ecuador/Panamá → modelo B con núcleo/galpón (sin cambios).
    /// Colombia → modelo B a NIVEL GRANJA (Fase 3 paso 2): unificado con Ecuador/Panamá sobre
    ///   inventario_gestion / item_inventario_ecuador, resolviendo el ítem desde catalogItemId
    ///   (id-mapping A→B por código) y descontando por (granja, ítem) sin exigir galpón. Antes
    ///   (Fase 2) Colombia usaba <see cref="ModeloInventarioConsumo.ModeloA"/>; ese path quedó sin uso.
    /// cualquier otro país / null → ninguno.
    /// </summary>
    public static ModeloInventarioConsumo ResolverModelo(int? paisIdDelLote) => paisIdDelLote switch
    {
        PaisEcuador or PaisPanama => ModeloInventarioConsumo.ModeloB,
        PaisColombia              => ModeloInventarioConsumo.ModeloBNivelGranja,
        _                         => ModeloInventarioConsumo.Ninguno
    };
}
