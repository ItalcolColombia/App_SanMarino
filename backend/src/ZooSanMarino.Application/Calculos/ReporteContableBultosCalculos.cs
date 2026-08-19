// src/ZooSanMarino.Application/Calculos/ReporteContableBultosCalculos.cs
// Cálculo PURO de la columna BULTO del Reporte Contable: qué fechas generan fila y cómo se acumula
// el saldo. Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas de la sección BULTO del Reporte Contable (postura).
/// <para>
/// Los movimientos de alimento son de GRANJA, no de lote: el reporte los imputa al lote padre. Hasta
/// ago-2026 una fila del detalle diario solo nacía si el lote tenía dato propio ese día (levante,
/// producción, entradas de aves o ventas/traslados); las fechas con movimiento de bultos y sin dato
/// del lote se descartaban con un <c>continue</c> y ese alimento <b>desaparecía</b> del reporte y del
/// saldo acumulado. Ese es exactamente el caso del alimento que llega días ANTES del encasetamiento
/// (y también el de cualquier ingreso hecho un día sin seguimiento), y es lo que empuja a la
/// operación a re-fechar el ingreso al primer día de consumo para que "aparezca"
/// (plan <c>fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md</c> §0.5, C1).
/// </para>
/// <para>
/// Acá viven las dos decisiones: <see cref="GeneraFilaSoloBultos"/> (esta fecha genera fila aunque el
/// lote no tenga dato propio) y <see cref="AcumularSaldos"/> (integración cronológica del saldo, que
/// es el mismo algoritmo que ya corría en el service — se extrajo sin cambiarlo).
/// </para>
/// </summary>
public static class ReporteContableBultosCalculos
{
    /// <summary>
    /// Movimientos de alimento de UNA fecha, ya consolidados y convertidos a bultos. Son de granja:
    /// el reporte los imputa al lote padre.
    /// </summary>
    public readonly record struct MovimientoBultosDia(
        DateTime Fecha,
        decimal Traslados,
        decimal Entradas,
        decimal Retiros);

    /// <summary>Deltas de bultos que aporta UNA fila del detalle diario.</summary>
    public readonly record struct DeltaBultosFila(
        decimal Entradas,
        decimal Traslados,
        decimal Retiros,
        decimal ConsumoHembras,
        decimal ConsumoMachos);

    /// <summary>Saldo de bultos resuelto para una fila del detalle diario.</summary>
    public readonly record struct SaldoBultosFila(
        decimal SaldoAnterior,
        decimal Saldo);

    /// <summary>
    /// Una fecha "tiene movimiento" si alguna de las tres columnas del kardex de bultos es distinta
    /// de cero. Una fecha presente en el kardex pero con las tres en cero (movimientos de un tipo que
    /// el reporte no clasifica) NO genera fila: sería una fila de ceros, ruido puro.
    /// </summary>
    public static bool TieneMovimiento(MovimientoBultosDia mov) =>
        mov.Entradas != 0m || mov.Traslados != 0m || mov.Retiros != 0m;

    /// <summary>
    /// Ventana de fechas en la que un movimiento de bultos puede generar fila propia.
    /// <para>
    /// Arranca <c>N</c> días antes de la primera llegada del lote —la misma ventana de alimento previo
    /// al encaset que usa engorde (<see cref="VentanaAlimentoPrevioCalculos"/>,
    /// <c>companies.dias_alimento_previo_encaset</c>)— para que el alimento que llegó ANTES del
    /// encasetamiento aparezca con su fecha real, y termina donde termina el reporte. El límite existe
    /// porque los movimientos son de granja: sin él, el reporte de un lote se llenaría de movimientos
    /// de otros ciclos del mismo galpón.
    /// </para>
    /// Si el usuario pidió un rango, su fecha inicio recorta la ventana (nunca la extiende): el
    /// detalle del lote ya se filtra con ese mismo rango.
    /// </summary>
    public static (DateTime Desde, DateTime Hasta) Ventana(
        DateTime fechaPrimeraLlegada,
        int? diasPreviosEmpresa,
        DateTime? fechaInicioFiltro,
        DateTime fechaFinReporte)
    {
        var desde = fechaPrimeraLlegada.Date
            .AddDays(-VentanaAlimentoPrevioCalculos.NormalizarDias(diasPreviosEmpresa));

        if (fechaInicioFiltro.HasValue && fechaInicioFiltro.Value.Date > desde)
            desde = fechaInicioFiltro.Value.Date;

        return (desde, fechaFinReporte.Date);
    }

    /// <summary>
    /// Traduce la ventana a un rango consultable contra <c>farm_inventory_movements.created_at</c>:
    /// <c>[Desde 00:00, Hasta+1día 00:00)</c>.
    /// <para>
    /// El corte superior es EXCLUSIVO al día siguiente para que el último día del reporte entre
    /// completo: <c>created_at</c> es <c>timestamptz</c> y las filas del kardex no están ancladas a
    /// medianoche, así que un <c>&lt;= Hasta</c> se comería todo lo registrado después de las 00:00 de
    /// ese día. Tampoco se recorta con <c>.Date</c> sobre la columna: <c>date_trunc</c> usaría la zona
    /// horaria de la SESIÓN, no la del reporte.
    /// </para>
    /// <para>
    /// Es el mismo rango que <see cref="GeneraFilaSoloBultos"/> ya exige aguas abajo; llevarlo a la
    /// consulta no cambia qué movimientos llegan al reporte, solo evita traer los que igual se iban a
    /// descartar (regla del repo: la BD filtra, el backend orquesta).
    /// </para>
    /// </summary>
    public static (DateTime Desde, DateTime HastaExclusivo) RangoConsulta(
        (DateTime Desde, DateTime Hasta) ventana) =>
        (ventana.Desde.Date, ventana.Hasta.Date.AddDays(1));

    // ── El consumo restado DOS VECES ────────────────────────────────────────────────────────────
    // El saldo suma dos fuentes que registran el MISMO hecho físico: el kardex de alimento (columna
    // RETIROS) y el consumo del seguimiento diario (columnas CONSUMO H/M). Pasa en las DOS ramas del
    // reporte, con firmas distintas, y en las dos el recorte a 0 de AcumularSaldos lo disfraza: el
    // acumulado real de los lotes 142 y 143 (MANGOS) llega a −2.599,6 y −2.644,7 bultos y se publica
    // como 0,0 — no como un negativo, sino como un galpón vacío.

    /// <summary><c>reason</c> con el que el seguimiento diario sellaba su salida de kardex legacy.</summary>
    public const string ReasonConsumoDiario = "Consumo diario";

    /// <summary><c>destination</c> con el que el seguimiento diario sellaba su salida de kardex legacy.</summary>
    public const string DestinoConsumo = "Consumo";

    /// <summary>
    /// <b>Rama LEGACY</b> (<c>farm_inventory_movements</c>): ¿este <c>Exit</c> es el ESPEJO de un
    /// consumo que el reporte ya publica en sus columnas de consumo?
    ///
    /// <para>
    /// <b>De dónde salen estas filas.</b> Hasta el 10-jul-2026 el modal de seguimiento, al guardar,
    /// posteaba un <c>Exit</c> al kardex legacy por los MISMOS kg que grababa en
    /// <c>seguimiento_diario_levante.consumo_kg_hembras/machos</c>. El commit <c>8e9bbc1</c> borró ese
    /// escritor porque duplicaba el descuento del inventario nuevo, pero <b>no tocó este reporte</b>,
    /// que siguió sumando las dos fuentes. En la BD local quedaron <b>252</b> movimientos /
    /// <b>131.278,3 kg</b> en la empresa 1 y 1 movimiento / 500 kg en Demo, todos con esta firma.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué el arreglo es de LECTURA y no de datos.</b> Esas filas decrementaron
    /// <c>farm_inventory.quantity</c> de verdad: en el kardex legacy son el único registro de que ese
    /// alimento salió, así que borrarlas desincronizaría el stock.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué se exigen los DOS campos.</b> Ambos son texto libre en el formulario manual, así que
    /// ninguno por separado prueba el origen; juntos son la firma que sólo producía la ruta del
    /// seguimiento. Y hace falta discriminar: la granja 20 tiene un <c>Exit</c> REAL de 3.280 kg, con
    /// <c>reason</c> vacío y <c>destination</c> <c>"Devolución"</c>, que sí tiene que restar.
    /// </para>
    ///
    /// <para>
    /// Portado del trabajo del 8-ago-2026 (<c>b853e95</c>, rama <c>claude/heuristic-perlman-f10ea4</c>),
    /// que arregló esta rama con plan y tests y nunca llegó a <c>main</c>.
    /// </para>
    /// </summary>
    public static bool EsConsumoYaContabilizadoPorSeguimiento(string? reason, string? destination) =>
        !string.IsNullOrWhiteSpace(reason) &&
        !string.IsNullOrWhiteSpace(destination) &&
        string.Equals(reason.Trim(), ReasonConsumoDiario, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(destination.Trim(), DestinoConsumo, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <b>Rama UNIFICADA</b> (<c>inventario_gestion_movimiento</c>): qué deltas entran al SALDO.
    ///
    /// <para>
    /// 🔑 <b>Acá el problema es de GRANO, no de firma.</b> Los <c>retiros</c> de esta rama son los
    /// movimientos <c>Consumo</c>, que escribe el propio seguimiento diario de <b>todos</b> los lotes
    /// de la granja; <c>consumoHembras/Machos</c> es el consumo de <b>ESTE lote padre</b> nada más.
    /// Restar los dos descuenta el de este padre dos veces.
    /// </para>
    ///
    /// <para>
    /// <b>Y el arreglo no es «excluir el consumo del seguimiento de los retiros»</b> —lo que sí
    /// corresponde en la rama legacy—: acá eso dejaría fuera el consumo de los OTROS padres de la
    /// granja, que es justamente lo que <c>retiros</c> aporta. Medido: el saldo se dispara a
    /// 3.730,2 / 2.257,3 / 3.137,3 / 4.266,2 y deja de converger. La resta correcta al grano de la
    /// granja es <c>entradas − traslados − retiros</c> ⇒ <b>518,2</b> (LA ESMERALDA) y <b>376,4</b>
    /// (MANGOS), un solo saldo por granja.
    /// </para>
    ///
    /// <para>
    /// <b>Por eso la decisión es POR RAMA.</b> En la legacy <c>retiros = Exit</c> y el consumo del
    /// backend va a <c>InventoryMovementType.ConsumoSeguimiento</c>, excluido a propósito de los 4
    /// buckets ⇒ ahí restar <c>consumoH/M</c> es correcto y único, y esta función devuelve la fila
    /// intacta. Un cambio plano en <see cref="AcumularSaldos"/> habría roto una rama para arreglar la
    /// otra.
    /// </para>
    ///
    /// <para>
    /// Es la misma invariante que engorde ya enforcaba en
    /// <c>TipoEventoInventarioCalculos.AfectaSaldoAlimentoEngorde</c> («Contarlo acá lo descontaría dos
    /// veces»), resuelta del lado que el Contable tiene disponible: engorde puede restar el consumo del
    /// seguimiento porque su kardex es por GALPÓN, al mismo grano; el Contable no, porque el suyo es
    /// por GRANJA.
    /// </para>
    /// </summary>
    /// <param name="fila">Los deltas de la fila, tal como los trae el detalle diario.</param>
    /// <param name="retirosYaTraenElConsumo">
    /// <c>true</c> cuando el kardex viene del módulo unificado
    /// (<c>companies.reportes_alimento_desde_inventario_unificado</c>).
    /// </param>
    public static DeltaBultosFila DeltaDelSaldo(DeltaBultosFila fila, bool retirosYaTraenElConsumo) =>
        retirosYaTraenElConsumo
            ? fila with { ConsumoHembras = 0m, ConsumoMachos = 0m }
            : fila;

    /// <summary>
    /// ¿Esta fecha genera una fila "solo bultos" del lote padre? Sí cuando hay movimiento real, el
    /// padre no generó ya una fila propia ese día (si la generó, esa fila YA lleva los bultos: el
    /// comportamiento histórico queda intacto) y la fecha cae en la ventana del reporte.
    /// </summary>
    public static bool GeneraFilaSoloBultos(
        MovimientoBultosDia mov,
        bool elPadreYaTieneFila,
        DateTime ventanaDesde,
        DateTime ventanaHasta) =>
        !elPadreYaTieneFila
        && TieneMovimiento(mov)
        && mov.Fecha.Date >= ventanaDesde
        && mov.Fecha.Date <= ventanaHasta;

    /// <summary>
    /// Aviso de ALCANCE de la sección BULTO: los movimientos de alimento son de la GRANJA, y cuando la
    /// granja tiene más de un lote padre **los reportes de todos ellos muestran los mismos kilos**.
    ///
    /// <para>
    /// <b>Por qué existe.</b> El daño concreto es sumar: la auditoría de ago-2026 midió la granja 20
    /// (LA ESMERALDA), donde 4 lotes padres muestran los mismos 2.907 bultos y sumar los 4 reportes da
    /// 11.628 bultos que no existen. No es arreglable en la query —los movimientos de Sanmarino son de
    /// nivel granja (1.077 de 1.078 filas sin núcleo ni galpón) y los padres comparten núcleo—, así
    /// que lo que faltaba era decirlo donde se lee.
    /// </para>
    ///
    /// <para>
    /// Devuelve <c>null</c> cuando el padre es el ÚNICO de su granja: ahí el kardex sí es suyo y un
    /// aviso sería ruido. También devuelve <c>null</c> si el dato no vino (0 o negativo): nunca se
    /// inventa una advertencia.
    /// </para>
    /// </summary>
    public static string? AdvertenciaAlcance(int lotesPadreEnGranja, string? granjaNombre)
    {
        if (lotesPadreEnGranja <= 1) return null;

        var granja = string.IsNullOrWhiteSpace(granjaNombre) ? "esta granja" : $"«{granjaNombre.Trim()}»";
        var otros  = lotesPadreEnGranja - 1;

        return $"Estos movimientos de alimento son de la GRANJA {granja}, que hoy tiene "
             + $"{lotesPadreEnGranja} lotes padres: el reporte de {(otros == 1 ? "el otro" : $"los otros {otros}")} "
             + "muestra los mismos kilos. NO sumar los reportes entre sí.";
    }

    /// <summary>
    /// ¿Esta fila cae en esta semana contable? Es el filtro histórico (<c>inicio ≤ fecha ≤ fin</c>)
    /// más una excepción: la PRIMERA semana absorbe las filas solo-bultos anteriores a su inicio.
    /// Las semanas contables arrancan el día del encasetamiento, así que sin esa absorción una entrada
    /// fechada antes del encaset no caería en ninguna semana y volvería a desaparecer del reporte.
    /// La absorción se limita a las filas solo-bultos (las que antes no existían) para no mover de
    /// semana ninguna fila que el reporte ya venía mostrando.
    /// </summary>
    public static bool PerteneceASemana(
        DateTime fecha,
        DateTime semanaInicio,
        DateTime semanaFin,
        bool absorbeAnteriores) =>
        fecha <= semanaFin && (absorbeAnteriores || fecha >= semanaInicio);

    /// <summary>
    /// Saldo de bultos fila a fila. Réplica exacta del acumulador que vivía en
    /// <c>ReporteContableService.CalcularSaldosAcumulativos</c>:
    /// <list type="bullet">
    /// <item>el acumulado se procesa por grupos de fecha, en orden cronológico;</item>
    /// <item>al abrir un grupo se retoma el saldo del DÍA ANTERIOR si ese día existe; si no existe
    /// (hueco de fechas) se continúa con el acumulado que venía — no se reinicia;</item>
    /// <item>cada fila suma entradas y resta traslados, retiros y consumos;</item>
    /// <item>el saldo se publica con piso en 0, pero el acumulador interno conserva el negativo.</item>
    /// </list>
    /// La entrada debe venir agrupada por fecha y ordenada ascendentemente (es lo que produce el
    /// <c>GroupBy(fecha).OrderBy(key)</c> del reporte).
    /// </summary>
    public static List<SaldoBultosFila> AcumularSaldos(
        IEnumerable<(DateTime Fecha, DeltaBultosFila Delta)> filasAgrupadasPorFecha)
    {
        var saldoPorFecha = new Dictionary<DateTime, decimal>();
        var salida = new List<SaldoBultosFila>();

        decimal acumulado = 0m;
        decimal saldoAnteriorDelGrupo = 0m;
        DateTime? fechaDelGrupo = null;

        foreach (var (fecha, delta) in filasAgrupadasPorFecha)
        {
            if (fechaDelGrupo != fecha)
            {
                if (fechaDelGrupo.HasValue)
                    saldoPorFecha[fechaDelGrupo.Value] = Math.Max(0m, acumulado);

                fechaDelGrupo = fecha;

                if (saldoPorFecha.TryGetValue(fecha.AddDays(-1), out var saldoDiaAnterior))
                {
                    saldoAnteriorDelGrupo = saldoDiaAnterior;
                    acumulado = saldoDiaAnterior;
                }
                else
                {
                    saldoAnteriorDelGrupo = 0m;
                }
            }

            acumulado = acumulado
                + delta.Entradas
                - delta.Traslados
                - delta.Retiros
                - delta.ConsumoHembras
                - delta.ConsumoMachos;

            salida.Add(new SaldoBultosFila(saldoAnteriorDelGrupo, Math.Max(0m, acumulado)));
        }

        if (fechaDelGrupo.HasValue)
            saldoPorFecha[fechaDelGrupo.Value] = Math.Max(0m, acumulado);

        return salida;
    }
}
