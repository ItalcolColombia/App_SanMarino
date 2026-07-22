namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Combina, sin tocar BD, el mapeo de ids del seguimiento → item_inventario_ecuador.id que usa
/// <c>ColombiaInventarioConsumoService</c>. El origen de cada id viaja explícito en
/// <see cref="ItemConsumoKey"/> (contrato camino-1/2 del front), así que ya NO se adivina la
/// tabla por existencia en catalogo_items (heurístico anterior que fallaba cuando los rangos
/// numéricos de ambas tablas colisionaban):
///   camino 1 (EsItemInventario=false): catalogItemId (modelo A histórico) → codigo →
///     item_inventario_ecuador por código (mapeo del backfill A→B, empresa efectiva).
///   camino 2 (EsItemInventario=true): id directo de item_inventario_ecuador, aceptado solo si
///     figura entre los válidos de la empresa efectiva (pass-through controlado).
/// Las claves que no resuelven por su camino NO figuran en el diccionario (el servicio lanza).
/// </summary>
public static class ColombiaInventarioIdResolutionCalculos
{
    public static Dictionary<ItemConsumoKey, int> Resolver(
        IReadOnlyCollection<(int Id, string Codigo)> catalogItemsEncontrados,
        IReadOnlyCollection<(int Id, string Codigo)> itemsBPorCodigoEncontrados,
        IReadOnlyCollection<int> itemsBDirectosValidos)
    {
        var map = new Dictionary<ItemConsumoKey, int>();

        var itemBPorCodigo = itemsBPorCodigoEncontrados
            .GroupBy(e => e.Codigo.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var ci in catalogItemsEncontrados)
        {
            var key = ci.Codigo.Trim().ToLowerInvariant();
            if (itemBPorCodigo.TryGetValue(key, out var itemBId))
                map[new ItemConsumoKey(ci.Id, EsItemInventario: false)] = itemBId;
        }

        foreach (var id in itemsBDirectosValidos)
            map[new ItemConsumoKey(id, EsItemInventario: true)] = id;

        return map;
    }
}
