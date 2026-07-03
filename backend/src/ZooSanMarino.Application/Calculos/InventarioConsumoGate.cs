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

public static class InventarioConsumoGate
{
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
}
