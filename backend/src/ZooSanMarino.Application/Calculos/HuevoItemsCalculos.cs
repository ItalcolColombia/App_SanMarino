// Cálculo PURO de la clasificación de huevos por ÍTEMS del catálogo (Primera/Pnc).
// Sin EF, sin estado, sin I/O: solo suma, validación y (des)serialización del desglose que viaja
// en seguimiento_diario_produccion.metadata → huevoItems.
using System.Text.Json;
using ZooSanMarino.Application.DTOs.Produccion;
using System.Linq;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas puras de la clasificación de huevos POR ÍTEMS (empresas con
/// <c>companies.clasificacion_huevo_por_items = true</c>).
/// <para>
/// Semántica del campo <c>huevoItems</c> del request de seguimiento de producción:
/// <list type="bullet">
///   <item><b><c>null</c></b> = "no tocar la clasificación por ítems" (en creación equivale a no
///   usarla; en edición conserva la ya persistida).</item>
///   <item><b><c>[]</c> (lista vacía)</b> = "quitar la clasificación por ítems": se borra la clave
///   del metadata y los totales vuelven a salir de los campos sueltos del DTO.</item>
///   <item><b>lista con elementos</b> = reemplaza el desglose; <c>huevo_tot</c> = suma de cantidades,
///   <c>huevo_inc</c> y las 11 columnas fijas quedan en 0.</item>
/// </list>
/// Tanto <c>null</c> como <c>[]</c> son entradas VÁLIDAS para <see cref="Validar"/>.
/// </para>
/// </summary>
public static class HuevoItemsCalculos
{
    /// <summary>Clave del desglose dentro del jsonb <c>metadata</c> del seguimiento de producción.</summary>
    public const string MetadataKey = "huevoItems";

    /// <summary>
    /// Total de huevos del día = suma de las cantidades de los ítems. <c>null</c> o lista vacía → 0.
    /// Es el valor que se persiste en <c>huevo_tot</c>.
    /// </summary>
    public static int SumarTotal(IEnumerable<HuevoItemSeguimientoDto>? items)
    {
        if (items is null) return 0;
        var total = 0;
        foreach (var i in items) total += i.Cantidad;
        return total;
    }

    /// <summary>
    /// Valida el desglose. Devuelve <c>null</c> si es válido, o el mensaje de error (apto para
    /// mostrarle al usuario) del PRIMER problema encontrado:
    /// <list type="number">
    ///   <item><c>CatalogItemId &lt;= 0</c> (ítem sin seleccionar);</item>
    ///   <item><c>Cantidad &lt; 0</c>;</item>
    ///   <item><c>CatalogItemId</c> repetido (debe consolidarse en una sola fila).</item>
    /// </list>
    /// <c>null</c> y lista vacía se consideran VÁLIDAS (= sin clasificación por ítems).
    /// </summary>
    public static string? Validar(IReadOnlyCollection<HuevoItemSeguimientoDto>? items)
    {
        if (items is null || items.Count == 0) return null;

        var vistos = new HashSet<int>();
        foreach (var item in items)
        {
            if (item.CatalogItemId <= 0)
                return "Clasificación de huevos: hay una fila sin ítem seleccionado (catalogItemId debe ser mayor a 0).";

            if (item.Cantidad < 0)
                return $"Clasificación de huevos: la cantidad del ítem {item.CatalogItemId} no puede ser negativa.";

            if (!vistos.Add(item.CatalogItemId))
                return $"Clasificación de huevos: el ítem {item.CatalogItemId} está repetido; consolide las cantidades en una sola fila.";
        }

        return null;
    }

    /// <summary>Mensaje cuando el lote todavía no declaró qué tipos de huevo produce.</summary>
    public const string MensajeLoteSinItemsDeclarados =
        "Este lote no tiene tipos de huevo asignados. Editá el lote y declará qué tipos produce " +
        "antes de registrar la clasificación.";

    /// <summary>
    /// F7.3 — ¿todos los ítems del desglose están entre los que el LOTE declaró producir?
    /// Devuelve <c>null</c> si es válido, o el mensaje del primer problema.
    ///
    /// <para>
    /// <b>Fail-closed, y es el corazón de la feature.</b> Un lote sin ítems declarados
    /// (<paramref name="permitidos"/> vacío) rechaza cualquier clasificación: NO se cae al catálogo
    /// completo. Es la decisión explícita del cliente (21-ago-2026) — «si no tiene asignado no
    /// aparece, el usuario tiene que editar el lote». Caer al catálogo completo sería exactamente
    /// el control que se pidió eliminar.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué es una función aparte y no un parámetro de <see cref="Validar"/>.</b> A
    /// <c>Validar</c> la comparten tres caminos de escritura (seguimiento, traslado de huevos y
    /// carga masiva) y solo el seguimiento y la carga masiva tienen lote con lista blanca: un
    /// traslado mueve lo que YA se produjo, así que restringirlo por la lista de HOY dejaría huevos
    /// reales atrapados si la lista cambió después. Agregarle el parámetro habría cambiado el
    /// comportamiento de los tres a la vez.
    /// </para>
    ///
    /// <para>
    /// <c>null</c> o lista vacía en <paramref name="items"/> ⇒ válido: no hay nada que clasificar,
    /// y es el caso «no tocar» de la edición. Un lote sin declarar sigue pudiendo guardar un
    /// seguimiento SIN huevos.
    /// </para>
    /// </summary>
    /// <param name="items">Desglose que llega en el request.</param>
    /// <param name="permitidos">Ids de <c>catalogo_items</c> que el lote declaró producir.</param>
    /// <param name="nombrePorItem">Nombre legible por id, para que el mensaje diga QUÉ ítem sobra.</param>
    public static string? ValidarPermitidos(
        IReadOnlyCollection<HuevoItemSeguimientoDto>? items,
        IReadOnlyCollection<int> permitidos,
        IReadOnlyDictionary<int, string>? nombrePorItem = null)
    {
        if (items is null || items.Count == 0) return null;

        if (permitidos.Count == 0) return MensajeLoteSinItemsDeclarados;

        var set = permitidos as HashSet<int> ?? new HashSet<int>(permitidos);

        foreach (var item in items)
        {
            if (set.Contains(item.CatalogItemId)) continue;

            var nombre = nombrePorItem is not null && nombrePorItem.TryGetValue(item.CatalogItemId, out var n)
                ? $"«{n}»"
                : $"(id {item.CatalogItemId})";

            return $"El ítem de huevo {nombre} no está entre los tipos que este lote produce. " +
                   "Editá el lote para agregarlo, o quitá la fila.";
        }

        return null;
    }

    /// <summary>
    /// Orden de presentación de los grupos de huevo: los tipos conocidos primero y en este orden, el
    /// resto después. Espeja <c>ORDEN_TIPOS_HUEVO</c> del frontend
    /// (<c>models/huevo-clasificacion.model.ts</c>) — si cambian, cambian los dos.
    /// </summary>
    public static readonly string[] OrdenTiposHuevo = { "Primera", "Pnc" };

    /// <summary>
    /// Peso de ordenamiento de un tipo de huevo: menor va primero. Un tipo desconocido o nulo va al
    /// final, nunca intercalado — así una categoría nueva del catálogo no se cuela entre Primera y
    /// Pnc sin que nadie lo decida.
    /// </summary>
    public static int PesoTipoHuevo(string? tipoHuevo)
    {
        if (string.IsNullOrWhiteSpace(tipoHuevo)) return OrdenTiposHuevo.Length;
        var idx = Array.FindIndex(OrdenTiposHuevo,
            t => t.Equals(tipoHuevo.Trim(), StringComparison.OrdinalIgnoreCase));
        return idx == -1 ? OrdenTiposHuevo.Length : idx;
    }

    /// <summary>
    /// Reconstruye el desglose persistido en el metadata del seguimiento (clave <c>huevoItems</c>).
    /// Devuelve lista vacía si la clave no existe, no es un array o el elemento no es objeto.
    /// Las filas sin <c>catalogItemId</c> válido se descartan (guarda defensiva).
    /// </summary>
    public static List<HuevoItemSeguimientoDto> LeerDeMetadata(JsonElement root)
    {
        var result = new List<HuevoItemSeguimientoDto>();
        if (root.ValueKind != JsonValueKind.Object) return result;
        if (!root.TryGetProperty(MetadataKey, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;

        foreach (var e in arr.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;

            var id = LeerInt(e, "catalogItemId");
            if (id <= 0) continue;

            result.Add(new HuevoItemSeguimientoDto(
                CatalogItemId: id,
                Codigo: LeerString(e, "codigo"),
                Nombre: LeerString(e, "nombre"),
                TipoHuevo: LeerString(e, "tipoHuevo"),
                Cantidad: LeerInt(e, "cantidad"),
                Um: LeerString(e, "um")
            ));
        }

        return result;
    }

    /// <summary>
    /// Escribe (o quita) la clave <c>huevoItems</c> dentro del jsonb <c>metadata</c> del seguimiento,
    /// CONSERVANDO verbatim todas las demás claves ya presentes (<c>itemsHembras</c>,
    /// <c>itemsMachos</c>, <c>consumoOriginal*</c>, <c>tipoItem*</c>, <c>tipoAlimento*</c>…).
    /// <list type="bullet">
    ///   <item><paramref name="huevoItems"/> con elementos → se agrega/reemplaza la clave;</item>
    ///   <item><paramref name="huevoItems"/> <c>null</c> o vacío → se quita la clave (si existía);</item>
    ///   <item>si al final no queda ninguna clave, devuelve <c>null</c> (metadata NULL en BD, igual que hoy).</item>
    /// </list>
    /// </summary>
    public static JsonDocument? EscribirEnMetadata(JsonDocument? metadata, IReadOnlyCollection<HuevoItemSeguimientoDto>? huevoItems)
    {
        var dict = new Dictionary<string, object?>();

        if (metadata != null && metadata.RootElement.ValueKind == JsonValueKind.Object)
            foreach (var prop in metadata.RootElement.EnumerateObject())
                if (!string.Equals(prop.Name, MetadataKey, StringComparison.Ordinal))
                    dict[prop.Name] = prop.Value.Clone();

        if (huevoItems is { Count: > 0 })
            dict[MetadataKey] = AMetadataJson(huevoItems);

        if (dict.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    /// <summary>
    /// Proyecta el desglose al shape camelCase que se serializa dentro del jsonb <c>metadata</c>:
    /// <c>{ catalogItemId, codigo, nombre, tipoHuevo, cantidad, um }</c>.
    /// </summary>
    private static List<Dictionary<string, object?>> AMetadataJson(IEnumerable<HuevoItemSeguimientoDto> items) =>
        items.Select(i => new Dictionary<string, object?>
        {
            ["catalogItemId"] = i.CatalogItemId,
            ["codigo"] = i.Codigo,
            ["nombre"] = i.Nombre,
            ["tipoHuevo"] = i.TipoHuevo,
            ["cantidad"] = i.Cantidad,
            ["um"] = i.Um
        }).ToList();

    private static int LeerInt(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;

    private static string? LeerString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>
    /// Disponibilidad por ítem = producido (suma de <paramref name="producidos"/> por
    /// <c>CatalogItemId</c>) menos ya transferido (suma de <paramref name="transferidos"/>, típicamente
    /// traslados de huevos ya <c>Completado</c>), sin bajar de 0. Ítems presentes solo en
    /// <paramref name="transferidos"/> (catálogo desactivado tras el traslado) también se incluyen,
    /// con disponible 0 — nunca negativo, nunca se inventa un producido que no existió.
    /// <para>
    /// Código/nombre/tipo/UM del resultado: se toman del primer <paramref name="producidos"/> que
    /// tenga ese <c>CatalogItemId</c>; si el ítem sólo aparece en <paramref name="transferidos"/>, de
    /// ahí. Puramente informativo (igual que en <see cref="HuevoItemSeguimientoDto"/>).
    /// </para>
    /// </summary>
    public static List<HuevoItemSeguimientoDto> CalcularDisponibilidad(
        IEnumerable<HuevoItemSeguimientoDto> producidos,
        IEnumerable<HuevoItemSeguimientoDto> transferidos)
    {
        var producidoPorItem = new Dictionary<int, int>();
        var infoPorItem = new Dictionary<int, HuevoItemSeguimientoDto>();
        foreach (var p in producidos ?? Enumerable.Empty<HuevoItemSeguimientoDto>())
        {
            if (p.CatalogItemId <= 0) continue;
            producidoPorItem[p.CatalogItemId] = producidoPorItem.GetValueOrDefault(p.CatalogItemId) + p.Cantidad;
            infoPorItem.TryAdd(p.CatalogItemId, p);
        }

        var transferidoPorItem = new Dictionary<int, int>();
        foreach (var t in transferidos ?? Enumerable.Empty<HuevoItemSeguimientoDto>())
        {
            if (t.CatalogItemId <= 0) continue;
            transferidoPorItem[t.CatalogItemId] = transferidoPorItem.GetValueOrDefault(t.CatalogItemId) + t.Cantidad;
            infoPorItem.TryAdd(t.CatalogItemId, t);
        }

        var idsOrdenados = producidoPorItem.Keys.Union(transferidoPorItem.Keys).OrderBy(id => id);

        var resultado = new List<HuevoItemSeguimientoDto>();
        foreach (var id in idsOrdenados)
        {
            var producido = producidoPorItem.GetValueOrDefault(id);
            var transferido = transferidoPorItem.GetValueOrDefault(id);
            var disponible = Math.Max(0, producido - transferido);
            var info = infoPorItem[id];
            resultado.Add(info with { Cantidad = disponible });
        }

        return resultado;
    }
}
