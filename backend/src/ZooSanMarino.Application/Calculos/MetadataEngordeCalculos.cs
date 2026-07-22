// Helpers puros de metadata del seguimiento de engorde, compartidos multi-país.
// Estaban duplicados (con formato distinto, misma lógica) en
// SeguimientoAvesEngordeService (Colombia) y SeguimientoAvesEngordeEcuadorService (Ecuador).
using System.Text.Json;

namespace ZooSanMarino.Application.Calculos;

public static class MetadataEngordeCalculos
{
    /// <summary>Convierte una cantidad a kg según la unidad declarada (g/gramos → /1000; resto se asume kg).</summary>
    public static decimal ToKg(double cantidad, string? unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }

    /// <summary>
    /// Acumula por ítem (item_inventario_ecuador_id o catalog_item_id) los kg de
    /// itemsHembras + itemsMachos + itemsGenerales del metadata del seguimiento.
    /// Propiedades que no sean arrays se ignoran (guarda defensiva).
    /// Nota Fase 2: itemsGenerales es ADITIVO (Ecuador no usa generales → sin impacto;
    /// Colombia sí, para descontar "todos los ítems"). Se lee SOLO del Metadata del
    /// seguimiento, nunca de ItemsAdicionales, para evitar doble descuento.
    /// </summary>
    public static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
    {
        var byItemId = new Dictionary<int, decimal>();
        void Acumular(string propName)
        {
            if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;
            foreach (var e in arr.EnumerateArray())
            {
                var id = 0;
                if (e.TryGetProperty("itemInventarioEcuadorId", out var pid) && pid.ValueKind != JsonValueKind.Null)
                    id = pid.GetInt32();
                if (id <= 0 && e.TryGetProperty("catalogItemId", out var cid))
                    id = cid.GetInt32();
                if (id <= 0) continue;
                var cant = e.TryGetProperty("cantidad", out var c) ? c.GetDouble() : 0;
                var un = e.TryGetProperty("unidad", out var u) ? u.GetString() : "kg";
                byItemId[id] = byItemId.GetValueOrDefault(id) + ToKg(cant, un);
            }
        }
        Acumular("itemsHembras");
        Acumular("itemsMachos");
        Acumular("itemsGenerales");
        return byItemId;
    }

    /// <summary>
    /// Igual que <see cref="ParseMetadataItemsToKg"/> pero CONSERVANDO el origen del id
    /// (<see cref="ItemConsumoKey"/>): un ítem con <c>itemInventarioEcuadorId&gt;0</c> es del
    /// inventario unificado (camino 2); si no, cae a <c>catalogItemId</c> (catálogo A, camino 1).
    /// Lo usan las ramas Colombia (IColombiaInventarioConsumoService), donde ambos tipos de id
    /// conviven y sus rangos colisionan — el parser plano sigue siendo el correcto para
    /// Ecuador/Panamá (allí ambos campos traen el mismo id de item_inventario_ecuador).
    /// </summary>
    public static Dictionary<ItemConsumoKey, decimal> ParseMetadataItemsToKgPorOrigen(JsonElement root)
    {
        var byItem = new Dictionary<ItemConsumoKey, decimal>();
        void Acumular(string propName)
        {
            if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;
            foreach (var e in arr.EnumerateArray())
            {
                var id = 0;
                if (e.TryGetProperty("itemInventarioEcuadorId", out var pid) && pid.ValueKind != JsonValueKind.Null)
                    id = pid.GetInt32();
                var esItemInventario = id > 0;
                if (id <= 0 && e.TryGetProperty("catalogItemId", out var cid))
                    id = cid.GetInt32();
                if (id <= 0) continue;
                var cant = e.TryGetProperty("cantidad", out var c) ? c.GetDouble() : 0;
                var un = e.TryGetProperty("unidad", out var u) ? u.GetString() : "kg";
                var key = new ItemConsumoKey(id, esItemInventario);
                byItem[key] = byItem.GetValueOrDefault(key) + ToKg(cant, un);
            }
        }
        Acumular("itemsHembras");
        Acumular("itemsMachos");
        Acumular("itemsGenerales");
        return byItem;
    }

    /// <summary>Mezcla un patch clave→valor sobre el metadata existente (el patch pisa claves).</summary>
    public static JsonDocument? MergeMetadataWithPatch(JsonDocument? existing, Dictionary<string, object?> patch)
    {
        if ((patch is null || patch.Count == 0) && existing is null) return null;
        if (patch is null || patch.Count == 0) return existing;
        Dictionary<string, object?> dict;
        if (existing != null)
            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(existing.RootElement.GetRawText())
                ?? new Dictionary<string, object?>();
        else
            dict = new Dictionary<string, object?>();
        foreach (var kv in patch) dict[kv.Key] = kv.Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }
}
