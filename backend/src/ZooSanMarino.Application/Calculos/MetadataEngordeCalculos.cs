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
    /// <para>
    /// Fase C (silos): si el ítem trae <c>siloId</c>, viaja en la clave. Dos filas del mismo ítem en
    /// silos distintos son DOS claves y se descuentan por separado; sin <c>siloId</c> la clave es
    /// exactamente la de antes (hash y agrupación idénticos).
    /// </para>
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
                var key = new ItemConsumoKey(id, esItemInventario, LeerSiloId(e));
                byItem[key] = byItem.GetValueOrDefault(key) + ToKg(cant, un);
            }
        }
        Acumular("itemsHembras");
        Acumular("itemsMachos");
        Acumular("itemsGenerales");
        return byItem;
    }

    /// <summary>
    /// Kilos por BLOQUE del formulario (hembras / machos / generales), sin acumular por ítem.
    ///
    /// <para>
    /// <see cref="ParseMetadataItemsToKg"/> suma los tres bloques en un solo total por ítem, que es lo
    /// correcto para descontar del inventario pero borra la información que necesita la regla de
    /// «alimento obligatorio»: esa regla no pregunta cuántos kilos hay, pregunta <b>en qué bloque</b>
    /// están —Mixto en engorde, hembras y/o machos en postura—. Un registro con el alimento cargado en
    /// el lugar equivocado tiene el mismo total y sigue siendo el error que se quiere frenar.
    /// </para>
    /// </summary>
    public static (decimal KgHembras, decimal KgMachos, decimal KgGenerales) ParseKgPorBloque(JsonElement root)
    {
        decimal Sumar(string propName)
        {
            if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return 0m;
            var total = 0m;
            foreach (var e in arr.EnumerateArray())
            {
                // Sin ítem seleccionado no hay alimento, aunque el usuario haya tipeado una cantidad:
                // es el caso de la fila vacía que el formulario deja abierta por defecto.
                var id = 0;
                if (e.TryGetProperty("itemInventarioEcuadorId", out var pid) && pid.ValueKind != JsonValueKind.Null)
                    id = pid.GetInt32();
                if (id <= 0 && e.TryGetProperty("catalogItemId", out var cid) && cid.ValueKind != JsonValueKind.Null)
                    id = cid.GetInt32();
                if (id <= 0) continue;

                var cant = e.TryGetProperty("cantidad", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetDouble() : 0;
                var un = e.TryGetProperty("unidad", out var u) ? u.GetString() : "kg";
                total += ToKg(cant, un);
            }
            return total;
        }

        return (Sumar("itemsHembras"), Sumar("itemsMachos"), Sumar("itemsGenerales"));
    }

    /// <summary>
    /// <c>siloId</c> de un ítem del metadata. Tolera que falte, que sea <c>null</c> o que venga como
    /// string (lo que manda un form a medio serializar); cualquier valor no positivo se trata como
    /// «sin silo», que es el comportamiento de todas las empresas sin el flag.
    /// </summary>
    private static int? LeerSiloId(JsonElement item)
    {
        if (!item.TryGetProperty("siloId", out var s)) return null;
        int? valor = s.ValueKind switch
        {
            JsonValueKind.Number => s.TryGetInt32(out var n) ? n : null,
            JsonValueKind.String => int.TryParse(s.GetString(), out var n) ? n : null,
            _ => null
        };
        return valor is > 0 ? valor : null;
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
