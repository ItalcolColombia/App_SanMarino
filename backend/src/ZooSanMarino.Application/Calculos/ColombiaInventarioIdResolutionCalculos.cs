namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Combina, sin tocar BD, el mapeo de ids "mixtos" → item_inventario_ecuador.id que usa
/// <c>ColombiaInventarioConsumoService</c>. Dos caminos: (1) catalogItemId (modelo A histórico) →
/// codigo → item_inventario_ecuador por código (mapeo del backfill A→B); (2) ids que nunca
/// existieron en catalogo_items pero sí son un item_inventario_ecuador.id válido de Colombia
/// (ítems creados directamente en el inventario nuevo, sin fila espejo en catalogo_items).
/// Un id que existe en catalogo_items pero no tiene equivalente por código NO cae al camino 2
/// (evita interpretar mal un id de catalogo_items como si fuera de item_inventario_ecuador).
/// </summary>
public static class ColombiaInventarioIdResolutionCalculos
{
    public static Dictionary<int, int> Resolver(
        IReadOnlyCollection<(int Id, string Codigo)> catalogItemsEncontrados,
        IReadOnlyCollection<(int Id, string Codigo)> itemsBPorCodigoEncontrados,
        IReadOnlyCollection<int> itemsBDirectosValidos)
    {
        var map = new Dictionary<int, int>();

        var itemBPorCodigo = itemsBPorCodigoEncontrados
            .GroupBy(e => e.Codigo.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var ci in catalogItemsEncontrados)
        {
            var key = ci.Codigo.Trim().ToLowerInvariant();
            if (itemBPorCodigo.TryGetValue(key, out var itemBId))
                map[ci.Id] = itemBId;
        }

        var idsEnCatalogoItems = catalogItemsEncontrados.Select(c => c.Id).ToHashSet();
        foreach (var id in itemsBDirectosValidos)
        {
            if (!idsEnCatalogoItems.Contains(id))
                map[id] = id;
        }

        return map;
    }
}
