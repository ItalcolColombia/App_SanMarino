// src/ZooSanMarino.Application/Calculos/ItemConsumoCalculos.cs
// F1 del plan `fase_de_desarrollo/descuento_inventario_movil_plan.md`: sube a Application, sin tocar
// resultados, el acumulador de ítems de consumo que vivía inline en
// Infrastructure/Services/ProduccionService.cs (AcumularItemsRequestPorOrigen).
// Motivo: ZooSanMarino.Application.Tests NO referencia Infrastructure, así que mientras la lógica
// viviera ahí no había forma de cubrirla y el gate de tests del repo no podía verla.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Kilos por ítem de consumo tomados <b>del request</b> del seguimiento diario (los DTOs que manda el
/// formulario), sin re-parsear el metadata jsonb que después se persiste.
///
/// <para>
/// <b>Por qué existe una ruta desde el request y no se lee siempre el metadata.</b> El descuento de
/// inventario ocurre en la misma operación que guarda el seguimiento; volver a parsear el JSON recién
/// serializado para saber qué descontar es un viaje de ida y vuelta que además abre la puerta a que
/// las dos rutas diverjan. La regla del repo —<i>una sola fórmula por número</i>— se sostiene acá
/// haciendo que este acumulador use la MISMA prioridad de id, la MISMA clave y la MISMA conversión de
/// unidad que <see cref="MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen"/>, que es la ruta
/// que lee del metadata al editar o borrar.
/// </para>
///
/// <para>
/// <b>No hay un <c>AgruparPorSilo</c> acá a propósito.</b> El silo no es un nivel de agrupación
/// posterior: viaja DENTRO de <see cref="ItemConsumoKey"/> desde el momento en que se arma la clave
/// (ver Fase C del plan de silos). Agregar un agrupador por silo sugeriría que existe una vista
/// «aplanada sin silo» que hoy nadie produce, y aplanarla es justamente el error que la clave evita:
/// sumaría los kg de dos silos y los descontaría todos del primero.
/// </para>
/// </summary>
public static class ItemConsumoCalculos
{
    /// <summary>
    /// Acumula por CLAVE TIPADA de ítem (<see cref="ItemConsumoKey"/>) los kg de los ítems del
    /// request: primero el bloque de hembras y después el de machos, sumando cuando la clave se
    /// repite. TODOS los tipos de ítem entran (alimento + medicamento + insumo): el filtro por tipo,
    /// si aplica, lo hace el llamador.
    ///
    /// <para>
    /// <b>Prioridad del id.</b> Si el ítem trae <c>ItemInventarioEcuadorId &gt; 0</c> ese id manda y
    /// la clave queda marcada como <c>EsItemInventario = true</c> (camino 2, pass-through al
    /// inventario unificado); si no, cae a <c>CatalogItemId</c> con la marca en <c>false</c> (camino
    /// 1, catálogo legacy). La marca no es decorativa: en Colombia los dos rangos de id conviven y
    /// colisionan, así que sin ella el mismo número apuntaría a dos ítems distintos.
    /// </para>
    ///
    /// <para>
    /// <b>Un ítem sin id se ignora en silencio.</b> Con los dos campos en cero o negativo no hay nada
    /// que descontar: es la fila vacía que el formulario deja abierta por defecto y que el usuario
    /// nunca completó. Reventar ahí haría fallar el guardado de un día perfectamente válido.
    /// </para>
    ///
    /// <para>
    /// <b>El silo entra en la clave sólo si es positivo.</b> Con <c>null</c> —toda empresa sin
    /// inventario por silo— el hash y la agrupación son exactamente los de antes de la Fase C, que es
    /// lo que garantiza que Colombia, Ecuador y Panamá no noten el cambio.
    /// </para>
    /// </summary>
    /// <param name="itemsHembras">Bloque «hembras» del formulario. <c>null</c> = bloque sin cargar.</param>
    /// <param name="itemsMachos">Bloque «machos» del formulario. <c>null</c> = bloque sin cargar.</param>
    /// <returns>
    /// Kg por clave. El mismo ítem cargado en hembras y en machos devuelve UNA entrada con la suma:
    /// el descuento es contra una sola fila de stock, no dos.
    /// </returns>
    public static Dictionary<ItemConsumoKey, decimal> AcumularPorOrigen(
        IEnumerable<ItemSeguimientoDto>? itemsHembras,
        IEnumerable<ItemSeguimientoDto>? itemsMachos)
    {
        var byItem = new Dictionary<ItemConsumoKey, decimal>();

        void Acumular(IEnumerable<ItemSeguimientoDto>? items)
        {
            if (items == null) return;
            foreach (var i in items)
            {
                var id = i.ItemInventarioEcuadorId.GetValueOrDefault();
                var esItemInventario = id > 0;
                if (id <= 0) id = i.CatalogItemId;
                if (id <= 0) continue;
                // El silo entra en la clave igual que en ParseMetadataItemsToKgPorOrigen: sin él, dos
                // filas del mismo alimento en silos distintos se sumarían y descontarían del primero.
                var key = new ItemConsumoKey(id, esItemInventario, i.SiloId is > 0 ? i.SiloId : null);
                byItem[key] = byItem.GetValueOrDefault(key) + MetadataEngordeCalculos.ToKg(i.Cantidad, i.Unidad);
            }
        }

        // El orden hembras → machos se conserva del original. Hoy no cambia el resultado (la suma es
        // conmutativa y decimal no pierde precisión acá), pero sí fija el orden de inserción del
        // diccionario, y ese orden es el que termina viéndose en las filas de movimiento de inventario.
        Acumular(itemsHembras);
        Acumular(itemsMachos);
        return byItem;
    }

    /// <summary>
    /// Bajo <see cref="ModeloInventarioConsumo.ModeloB"/> (Ecuador/Panamá) sólo existe UNA tabla de
    /// origen posible para un ítem: <c>item_inventario</c>. La marca <c>EsItemInventario =
    /// false</c> sólo tiene sentido donde los dos orígenes conviven de verdad (Colombia,
    /// <see cref="ModeloInventarioConsumo.ModeloBNivelGranja"/>) — bajo modelo B, si llega en
    /// <c>false</c> es porque el cliente sólo mandó <c>catalogItemId</c> (el bug medido en
    /// <c>modal-seguimiento-reproductora.component.ts</c>, que hoy sirve a Panamá — 121 lotes — y
    /// Ecuador — 3 lotes — en producción), no porque el id sea de <c>catalogo_items</c>: sigue siendo
    /// <c>item_inventario</c>, sólo que mal etiquetado.
    /// <para>
    /// F5.4 del plan (<c>descuento_inventario_movil_plan.md</c>): rechazar acá rompería un flujo que
    /// hoy funciona (la reserva ya resolvía bien contra <c>item_inventario</c>, ver
    /// <c>InventarioGestionService.ResolverUbicacionConsumoAsync</c>); dejarlo tal cual deja el
    /// registro de auditoría (<c>SeguimientoReservaAlimento.EsItemInventario</c>) mintiendo. Se
    /// normaliza a <c>true</c> — sin rechazo, sin cambiar qué fila de stock se descuenta.
    /// </para>
    /// </summary>
    public static Dictionary<ItemConsumoKey, decimal> NormalizarParaModeloB(
        Dictionary<ItemConsumoKey, decimal> porOrigen)
    {
        if (porOrigen.Count == 0) return porOrigen;

        var normalizado = new Dictionary<ItemConsumoKey, decimal>();
        foreach (var (key, kg) in porOrigen)
        {
            var claveNormal = key.EsItemInventario ? key : key with { EsItemInventario = true };
            normalizado[claveNormal] = normalizado.GetValueOrDefault(claveNormal) + kg;
        }
        return normalizado;
    }

    /// <summary>
    /// F7 — kg de ALIMENTO de un bloque de ítems (mismo filtro y misma conversión de unidad que
    /// <see cref="AcumularPorOrigen"/>, sin la clave tipada). La usa <c>SyncPushService</c> para
    /// recomponer el consumo escalar cuando reintenta un push sin ítems: si sólo se borra el array,
    /// el registro pierde el alimento entero y la validación de "alimento obligatorio" lo rechaza en
    /// vez de guardarlo para cuadre.
    /// </summary>
    public static double KgDeAlimento(IEnumerable<ItemSeguimientoDto>? items)
    {
        if (items is null) return 0;
        double total = 0;
        foreach (var i in items)
        {
            if ((i.TipoItem ?? string.Empty).Trim().ToLowerInvariant() != "alimento") continue;
            total += (double)MetadataEngordeCalculos.ToKg(i.Cantidad, i.Unidad);
        }
        return total;
    }

    /// <summary>
    /// F7 — nombres (sin repetir, en el orden en que aparecen) de los ítems de ALIMENTO de un bloque,
    /// para reconstruir <c>tipoAlimento</c> cuando el reintento sin ítems lo deja vacío.
    /// </summary>
    public static IReadOnlyList<string> NombresDeAlimento(IEnumerable<ItemSeguimientoDto>? items)
    {
        if (items is null) return Array.Empty<string>();
        var nombres = new List<string>();
        foreach (var i in items)
        {
            if ((i.TipoItem ?? string.Empty).Trim().ToLowerInvariant() != "alimento") continue;
            var nombre = i.Nombre?.Trim();
            if (!string.IsNullOrEmpty(nombre) && !nombres.Contains(nombre)) nombres.Add(nombre);
        }
        return nombres;
    }
}
