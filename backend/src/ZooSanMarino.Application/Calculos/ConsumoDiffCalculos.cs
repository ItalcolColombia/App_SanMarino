// src/ZooSanMarino.Application/Calculos/ConsumoDiffCalculos.cs
// F1 del plan `fase_de_desarrollo/descuento_inventario_movil_plan.md`: subir a Calculos el diff
// old/new de ítems de consumo que hoy vive inline en tres services de Infrastructure.
// Puro (sin EF, sin estado, sin async): el service resuelve viejos/nuevos y delega.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Un movimiento de inventario que hay que emitir para que el stock refleje la edición de un
/// seguimiento diario.
///
/// <para>
/// Se modela como el <b>diff con signo</b> y no como dos listas separadas porque así se lee igual que
/// el bucle que reemplaza (<c>if (diff &gt; 0) RegistrarConsumo(diff) else if (diff &lt; 0)
/// RegistrarIngreso(-diff)</c>) y no hay forma de emitir las dos cosas para la misma clave.
/// </para>
/// </summary>
/// <param name="Clave">Ítem afectado, con su tabla de origen y su silo (ver <see cref="ItemConsumoKey"/>).</param>
/// <param name="Diff">
/// <c>nuevo − viejo</c>. Siempre distinto de cero: las claves sin cambio no generan movimiento.
/// Positivo = hay que consumir más; negativo = hay que devolver.
/// </param>
public readonly record struct MovimientoConsumo(ItemConsumoKey Clave, decimal Diff)
{
    /// <summary>El registro subió: se descuenta <see cref="Cantidad"/> del stock («ajuste»).</summary>
    public bool EsConsumo => Diff > 0;

    /// <summary>El registro bajó o el ítem desapareció: se reingresa <see cref="Cantidad"/> («devolución»).</summary>
    public bool EsDevolucion => Diff < 0;

    /// <summary>Magnitud del movimiento, siempre positiva. Es lo que espera el request de inventario.</summary>
    public decimal Cantidad => Diff > 0 ? Diff : -Diff;
}

/// <summary>
/// Diff de ítems de consumo al <b>EDITAR</b> un seguimiento diario: qué se descuenta de más y qué se
/// devuelve, comparando el mapa de ítems que tenía el registro contra el que trae la edición.
///
/// <para>
/// <b>Por qué existe.</b> Este bucle está escrito inline <b>tres veces</b> y ninguna es testeable:
/// <c>ZooSanMarino.Application.Tests</c> no referencia Infrastructure, así que mientras la lógica viva
/// dentro de un service con <c>_ctx</c> y <c>await</c> no hay forma de cubrirla y no pasa el gate de
/// CI del repo. Los tres call sites son:
/// </para>
/// <list type="bullet">
///   <item><c>Services/SeguimientoLoteLevante/Funciones/SeguimientoLoteLevanteService.Crud.cs</c></item>
///   <item><c>Services/SeguimientoAvesEngordeEcuador/Funciones/SeguimientoAvesEngordeEcuadorService.Crud.cs</c></item>
///   <item><c>Services/Funciones/ProduccionService.Seguimiento.cs</c></item>
/// </list>
///
/// <para>
/// <b>⚠️ Los tres call sites viejos NO se migran a esta clase</b> (lo prohíbe explícitamente F1 del
/// plan). Ver <see cref="ClavesOrdenadas"/>: hoy iteran un <c>HashSet</c>, cuyo orden no está
/// garantizado; ordenar la salida cambiaría el orden en que se escriben las filas de movimiento y eso
/// es un cambio de comportamiento observable (la tabla diaria de engorde desempata intra-día por
/// <c>created_at</c>). Esta clase la consume solamente el código NUEVO; migrar a los viejos es otra
/// fase, con su propio testigo antes/después.
/// </para>
///
/// <para>
/// <b>En qué difieren las tres copias del original</b> (medido al extraerlas, 22-ago-2026):
/// </para>
/// <list type="number">
///   <item>
///     <b>Tipo de clave.</b> Producción y las ramas Colombia de levante/engorde usan la clave TIPADA
///     (<see cref="ItemConsumoKey"/>, conserva la tabla de origen y el silo). La rama Ecuador/Panamá
///     de levante y engorde usa <c>Dictionary&lt;int, decimal&gt;</c>, que <b>aplana el origen</b>: un
///     <c>catalogItemId</c> y un <c>itemInventarioEcuadorId</c> con el mismo número colapsan en una
///     sola clave. Esta clase adopta la TIPADA, siguiendo la decisión de diseño del plan («NO se
///     aplana <c>ItemConsumoKey</c> a <c>int</c>»), respaldada por el doc-comment de
///     <see cref="ItemConsumoKey"/>: los dos rangos de ids se solapan y aplanarlos «produce descuentos
///     rechazados (o cruzados) por colisión de ids».
///   </item>
///   <item>
///     <b>Qué calculan.</b> Producción calcula <b>solo incrementos</b> (la aplicación del diff se la
///     delega a <c>IColombiaInventarioConsumoService.AplicarDiffAsync</c>, que recibe los dos mapas
///     enteros). Levante y engorde Ecuador calculan incrementos <i>y</i> además recorren la unión otra
///     vez para emitir los movimientos uno por uno. De ahí las dos operaciones de esta clase:
///     <see cref="Incrementos"/> y <see cref="Movimientos"/>.
///   </item>
///   <item>
///     <b>Cómo arman la unión.</b> El bucle de incrementos de levante/engorde usa
///     <c>new HashSet&lt;int&gt;(viejos.Keys.Concat(nuevos.Keys))</c>; el de movimientos usa
///     <c>new HashSet&lt;int&gt;(viejos.Keys)</c> + un <c>foreach</c> que agrega los nuevos. El
///     conjunto resultante es el mismo; el orden de recorrido no tiene por qué serlo.
///   </item>
///   <item>
///     <b>De dónde salen los mapas.</b> Levante y engorde parsean el <c>metadata</c> del registro
///     (<c>ParseMetadataItemsToKgPorOrigen</c>); producción acumula los ítems del request
///     (<c>AcumularItemsRequestPorOrigen(request.ItemsHembras, request.ItemsMachos)</c>). Eso queda
///     fuera de esta clase a propósito: acá entran los mapas ya armados, vengan de donde vengan.
///   </item>
///   <item>
///     <b>Guarda de entrada.</b> Levante y engorde saltean el bloque entero si
///     <c>dto.Metadata == null &amp;&amp; oldByItemId.Count == 0</c>; producción no tiene esa guarda.
///     Es equivalente: con los dos mapas vacíos esta clase devuelve vacío y nadie emite nada.
///   </item>
/// </list>
///
/// <para>
/// <b>Lo que NO se cambió al mover.</b> La comparación sigue siendo <c>diff &gt; 0</c> estricta
/// (un ítem que no cambió, o que llega en 0 y no existía, no genera nada), la resta se hace en
/// <c>decimal</c> sin redondear —los originales tampoco redondean; los kg ya vienen convertidos desde
/// gramos por el parser— y una cantidad ausente vale 0 vía <c>GetValueOrDefault</c>.
/// </para>
/// </summary>
public static class ConsumoDiffCalculos
{
    /// <summary>
    /// Unión de las claves de los dos mapas, en orden <b>estable y determinista</b>:
    /// <c>(Id, EsItemInventario, SiloId)</c>, con <c>SiloId</c> nulo primero.
    ///
    /// <para>
    /// <b>Por qué el orden es parte del contrato y no un detalle.</b> Los originales recorren un
    /// <c>HashSet</c>, que no garantiza orden de iteración. Mientras el diff sólo alimentaba una
    /// validación previa eso era invisible; en cuanto se emiten movimientos uno por uno, el orden del
    /// recorrido es el orden en que nacen las filas de <c>inventario_gestion_movimiento</c>, y el
    /// saldo corriente de la tabla diaria de engorde desempata intra-día por <c>created_at</c>. Un
    /// orden arbitrario ahí es exactamente lo que produce días que cierran en rojo con el total
    /// perfecto (<c>filas_negativas</c>, la señal que CLAUDE.md manda leer aparte de
    /// <c>descuadre_kg</c>).
    /// </para>
    ///
    /// <para>
    /// Y es, además, la razón por la que los tres call sites viejos se dejan como están: hacerlos
    /// delegar acá les cambiaría ese orden, o sea el comportamiento observable.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ItemConsumoKey> ClavesOrdenadas(
        IReadOnlyDictionary<ItemConsumoKey, decimal> viejos,
        IReadOnlyDictionary<ItemConsumoKey, decimal> nuevos)
    {
        var union = new HashSet<ItemConsumoKey>(viejos.Keys);
        foreach (var k in nuevos.Keys) union.Add(k);

        return union
            .OrderBy(k => k.Id)
            .ThenBy(k => k.EsItemInventario)   // false (catálogo) antes que true (inventario)
            .ThenBy(k => k.SiloId)             // Comparer<int?>.Default: null primero
            .ToArray();
    }

    /// <summary>
    /// Sólo los diff <b>positivos</b>: los kilos que la edición descuenta DE MÁS y que hay que validar
    /// contra el stock <i>antes</i> de persistir.
    ///
    /// <para>
    /// <b>Por qué sólo los positivos.</b> Es la regla que los tres originales repiten con el mismo
    /// comentario: una edición a la baja devuelve stock, y devolver nunca puede fallar por falta de
    /// stock. Validar también los negativos rechazaría correcciones legítimas.
    /// </para>
    ///
    /// <para>
    /// El <c>&gt; 0</c> es estricto: un ítem que no cambió (diff 0) no se valida ni se toca. Ese es el
    /// caso mayoritario en una edición real —se corrige la mortalidad y el alimento queda igual— y
    /// meterlo en el mapa haría revalidar stock que ya está descontado.
    /// </para>
    ///
    /// <para>
    /// Devuelve un <c>Dictionary</c> para poder pasarlo tal cual a
    /// <c>ValidarStockConsumoAsync(int, IReadOnlyDictionary&lt;ItemConsumoKey, decimal&gt;, ...)</c>.
    /// Las claves se insertan en el orden de <see cref="ClavesOrdenadas"/>, así que al recorrerlo el
    /// mensaje de «falta stock» siempre nombra el mismo ítem primero ante la misma entrada — hoy, con
    /// el <c>HashSet</c>, dos ejecuciones idénticas pueden culpar a ítems distintos.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<ItemConsumoKey, decimal> Incrementos(
        IReadOnlyDictionary<ItemConsumoKey, decimal> viejos,
        IReadOnlyDictionary<ItemConsumoKey, decimal> nuevos)
    {
        var incrementos = new Dictionary<ItemConsumoKey, decimal>();

        foreach (var clave in ClavesOrdenadas(viejos, nuevos))
        {
            var diff = nuevos.GetValueOrDefault(clave) - viejos.GetValueOrDefault(clave);
            if (diff > 0) incrementos[clave] = diff;
        }

        return incrementos;
    }

    /// <summary>
    /// El diff completo como lista de movimientos a emitir, en el orden de
    /// <see cref="ClavesOrdenadas"/>.
    ///
    /// <para>
    /// Equivale al segundo bucle de levante y engorde Ecuador: <c>diff &gt; 0</c> ⇒ consumo de
    /// ajuste, <c>diff &lt; 0</c> ⇒ ingreso de devolución por <c>-diff</c>, <c>diff == 0</c> ⇒ nada.
    /// Un ítem que desaparece de la edición aparece acá como devolución por el total que tenía: es el
    /// caso que hace que borrar una línea del formulario reponga el stock.
    /// </para>
    ///
    /// <para>
    /// La cantidad viaja como <see cref="MovimientoConsumo.Diff"/> con signo y el llamador usa
    /// <see cref="MovimientoConsumo.Cantidad"/> para el request; así el tipo del movimiento y su
    /// magnitud no pueden quedar desalineados por un <c>-</c> olvidado, que es un error fácil de
    /// cometer cuando el bucle se copia a un cuarto módulo.
    /// </para>
    /// </summary>
    public static IReadOnlyList<MovimientoConsumo> Movimientos(
        IReadOnlyDictionary<ItemConsumoKey, decimal> viejos,
        IReadOnlyDictionary<ItemConsumoKey, decimal> nuevos)
    {
        var movimientos = new List<MovimientoConsumo>();

        foreach (var clave in ClavesOrdenadas(viejos, nuevos))
        {
            var diff = nuevos.GetValueOrDefault(clave) - viejos.GetValueOrDefault(clave);
            if (diff != 0) movimientos.Add(new MovimientoConsumo(clave, diff));
        }

        return movimientos;
    }
}
