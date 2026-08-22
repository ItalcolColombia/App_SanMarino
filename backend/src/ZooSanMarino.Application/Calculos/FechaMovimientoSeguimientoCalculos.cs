namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Con qué día nace el movimiento de inventario que dispara un seguimiento diario.
///
/// <para>
/// <b>El problema que existe hoy.</b> El kardex se fecha, en varios caminos, en el día en que el
/// registro llegó al servidor y no en el día del galpón. El caso canónico es
/// <c>ResolveMovimientoCreatedAt</c> (<c>InventarioGestionService.cs</c>): cuando el llamador le pasa
/// una fecha, ancla ese día a mediodía UTC; cuando le pasa <c>null</c> —que es lo que hacen los
/// caminos que nunca tuvieron dónde poner la fecha— devuelve <c>DateTimeOffset.UtcNow</c>, o sea el
/// instante del guardado. Medido en la BD local el 22-ago-2026: <b>814 de 817</b> consumos de levante
/// y <b>4.536 de 6.555</b> de engorde tienen el movimiento fechado en un día distinto al del
/// seguimiento que lo originó, con desfases de hasta 565 días.
/// </para>
///
/// <para>
/// <b>Por qué la app móvil lo vuelve estructural.</b> Con red, «día del guardado» y «día del galpón»
/// suelen coincidir y el desfase pasa desapercibido. <c>zootecnicoapp</c> captura sin señal y encola:
/// el galponero registra el lunes y el push sale el viernes cuando vuelve la cobertura, en lote y
/// todo junto. Fechar por el guardado le pone a los cinco días de campo la misma fecha —la del
/// sync— y deja el kardex ilegible justo en la semana que hubo que recuperar a mano.
/// </para>
///
/// <para>
/// <b>Por qué el reloj del dispositivo no sirve para arreglarlo.</b> <c>/api/Sync/push</c> ya recibe y
/// persiste <c>capturadoAtDispositivo</c> (ver <c>DTOs/Sync/SyncPushDtos.cs</c> y la entidad
/// <c>SyncOperacion</c>, cuyo doc dice literalmente que es informativa y <b>no</b> autoritativa). Ese
/// valor sale del reloj del teléfono, que el usuario cambia a mano —a propósito o por tener la zona
/// horaria mal—. Sirve para auditar cuándo dice el dispositivo que capturó; no puede decidir con qué
/// día se descuenta stock, porque un teléfono adelantado movería kilos a un día que todavía no pasó.
/// </para>
///
/// <para>
/// <b>La regla, entonces:</b> manda la fecha que el usuario escribió en el formulario del seguimiento.
/// Es la única de las tres que describe el día del galpón, es la que ya se muestra en pantalla y es
/// la que el operario puede corregir si se equivocó.
/// </para>
///
/// <para>
/// <b>Alcance.</b> Esto resuelve <i>qué día</i>, no <i>a qué hora dentro del día</i>. El empate de las
/// 12:00Z entre un ingreso y un consumo del mismo día —que puede hacer cerrar el saldo corriente en
/// rojo porque <c>fn_seguimiento_diario_engorde</c> ordena intra-día por <c>created_at</c>— es un
/// problema aparte, con su propio testigo, y se resuelve en la fase que conecta este cálculo a los
/// services. Acá no se elige ninguna hora.
/// </para>
/// </summary>
public static class FechaMovimientoSeguimientoCalculos
{
    /// <summary>
    /// Día con el que se fecha el movimiento de inventario de un seguimiento: <b>el del formulario</b>.
    ///
    /// <para>
    /// Los otros dos parámetros entran <b>a propósito sin usarse</b>. No son un descuido ni una firma
    /// a medio terminar: son la especificación ejecutable de que ni el reloj del teléfono ni el del
    /// servidor son autoritativos sobre la fecha del kardex. Están en la firma —y sus tests fijan que
    /// el resultado no cambia con ellos— para que conectarlos sea un cambio visible que alguien tenga
    /// que justificar, en vez de una línea que se cuela en un refactor.
    /// </para>
    ///
    /// <para>
    /// <b>Se descarta la hora, no la zona.</b> Se devuelve <c>.Date</c> porque el kardex es por día
    /// (la tabla diaria agrupa por <c>DATE(fecha_operacion)</c>) y porque el consumidor natural,
    /// <c>ResolveMovimientoCreatedAt</c>, sólo lee año/mes/día para anclar el instante. Deliberadamente
    /// <b>no</b> se hace <c>ToUniversalTime()</c>: convertir una fecha local de la tarde a UTC la
    /// empuja al día siguiente, que es exactamente el corrimiento que este cálculo existe para evitar.
    /// El <c>Kind</c> del valor recibido se conserva tal cual.
    /// </para>
    /// </summary>
    /// <param name="fechaRegistro">Fecha del seguimiento, la que cargó el usuario. Única que decide.</param>
    /// <param name="capturadoAtDispositivo">
    /// Momento declarado por el teléfono en el push offline. Se registra para auditoría y se ignora
    /// acá: su reloj es del usuario, no del sistema.
    /// </param>
    /// <param name="ahoraServidorUtc">
    /// Reloj del servidor al procesar. Es el valor que hoy se cuela como fecha del movimiento cuando
    /// no hay otra; se ignora porque describe el sync, no la operación del galpón.
    /// </param>
    public static DateTime Resolver(
        DateTime fechaRegistro,
        DateTime? capturadoAtDispositivo,
        DateTime ahoraServidorUtc)
        => fechaRegistro.Date;

    /// <summary>
    /// Hora UTC a la que se ancla un movimiento de <b>ingreso</b>. Es la hora histórica
    /// (<c>ResolveMovimientoCreatedAt</c> siempre anclaba a mediodía) y sigue siéndolo: nada cambia
    /// para quien sólo ingresa alimento.
    /// </summary>
    public const int AnclaIngresoUtc = 12;

    /// <summary>
    /// Hora UTC a la que se ancla un movimiento de <b>consumo</b>.
    ///
    /// <para>
    /// <b>Por qué no comparte la hora del ingreso.</b> <c>fn_seguimiento_diario_engorde</c> ordena el
    /// saldo corriente intra-día por <c>created_at</c> (<c>SUM(delta) OVER (ORDER BY DATE(fecha), ts
    /// ROWS UNBOUNDED PRECEDING)</c>). Antes de F2 casi ningún consumo fechaba: caía a
    /// <c>DateTimeOffset.UtcNow</c>, que es más tarde que cualquier ingreso anclado a las 12:00 y el
    /// orden —entra primero, se come después— salía bien por accidente. Fechar el consumo con la MISMA
    /// ancla del ingreso (12:00) los empataría exactamente, y el orden pasaría a depender del
    /// desempate no documentado de la ventana SQL: el saldo corriente de un galpón podría cerrar el
    /// día en rojo (<c>filas_negativas</c>, la señal que CLAUDE.md distingue de <c>descuadre_kg</c>)
    /// sin que ningún kilo real faltara.
    /// </para>
    ///
    /// <para>
    /// Se elige una ancla POSTERIOR y determinista —nunca una hora aleatoria ni la del reloj del
    /// servidor— porque es el orden físicamente correcto (primero entra el alimento, después se come)
    /// y no exige tocar <c>fn_seguimiento_diario_engorde</c>, que es cálculo compartido multipaís con
    /// su propio gate.
    /// </para>
    /// </summary>
    public const int AnclaConsumoUtc = 18;

    /// <summary>
    /// Ancla un día a una hora UTC exacta. Es el mismo cálculo que hacía <c>ResolveMovimientoCreatedAt</c>
    /// inline; se sube acá para que la elección de hora (ingreso vs. consumo) tenga un solo dueño y un
    /// test que la fije.
    /// </summary>
    public static DateTimeOffset Anclar(DateTime dia, int horaUtc) =>
        new(dia.Year, dia.Month, dia.Day, horaUtc, 0, 0, TimeSpan.Zero);
}
