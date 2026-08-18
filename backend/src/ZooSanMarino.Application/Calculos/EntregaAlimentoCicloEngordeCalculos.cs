using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Un ciclo del galpón, con lo único que la atribución necesita saber de él.</summary>
/// <param name="LoteId">Id del lote de engorde.</param>
/// <param name="FechaEncaset">Día del encasetamiento. Ordena los ciclos entre sí.</param>
/// <param name="SegMin">Primer día con seguimiento cargado. <c>null</c> = el ciclo todavía no arrancó
/// operativamente. ⚠️ Puede ser ANTERIOR al encaset (lote 175: encaset 17-jul, primer seg 16-jul).</param>
/// <param name="SegMax">Último día con seguimiento cargado.</param>
/// <param name="Congelado">Tiene liquidación congelada vigente. Una foto congelada no se reescribe.</param>
public readonly record struct CicloGalponEngorde(
    int LoteId,
    DateTime FechaEncaset,
    DateTime? SegMin,
    DateTime? SegMax,
    bool Congelado);

/// <summary>El movimiento marcado que hay que atribuir.</summary>
/// <param name="Fecha">Fecha de operación (se compara por día).</param>
/// <param name="Kg">Kg del movimiento.</param>
/// <param name="EsEntrada">Es <c>INV_INGRESO</c> o <c>INV_TRASLADO_ENTRADA</c>.</param>
/// <param name="Anulado">El movimiento origen está anulado.</param>
/// <param name="TieneGalpon">Trae galpón. Sin galpón no hay ciclo al que atribuir.</param>
public readonly record struct MovimientoMarcadoEngorde(
    DateTime Fecha,
    decimal Kg,
    bool EsEntrada,
    bool Anulado,
    bool TieneGalpon);

/// <summary>El veredicto: qué se escribe en <see cref="AlimentoEntregaCicloEngorde"/>.</summary>
public readonly record struct AtribucionEntregaEngorde(
    string Estado,
    int? LoteCedenteId,
    int? LoteDestinoId,
    DateTime? FechaEntrega,
    decimal KgEntregados,
    decimal KgNoDiferible,
    string Motivo);

/// <summary>
/// Dueño ÚNICO de la atribución del alimento marcado «para el próximo ciclo» (FASE B del plan
/// <c>fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md</c>).
/// <para>
/// <b>La inversión respecto de los 4 intentos anteriores.</b> Antes SQL era dueño de la atribución y la
/// recalculaba en cada lectura; ahora esto la decide <b>una sola vez</b>, el escritor la persiste y
/// <c>fn_seguimiento_diario_engorde</c> pasa a ser <b>lectora</b>. Por eso congelar un extremo del
/// handoff ya no puede cambiar lo que ve el otro: no queda nada que recalcular.
/// </para>
/// <para>
/// <b>Fail-closed (D3b).</b> Cualquier condición que no se pueda resolver termina en
/// <c>PENDIENTE</c> o <c>INERTE</c>, <b>nunca</b> en <c>VIGENTE</c>. Los dos son inocuos: la fn no hace
/// nada con ellos y el resultado es idéntico al de HEAD, que es el único estado validado en producción.
/// La marca <b>suma</b> atribución cuando puede; nunca <b>resta</b> visibilidad.
/// </para>
/// <para>
/// Puro: sin EF, sin estado, sin <c>DateTime.Now</c>. El tope y la fecha de entrega salen de datos que
/// el llamador consulta a la fn (una sola fórmula por número).
/// </para>
/// <para>
/// 🔴 <b>HALLAZGO DEL GATE (18-ago-2026): con los datos de hoy esto devuelve <c>INERTE</c> SIEMPRE, y
/// está bien que así sea.</b> Medido sobre los 53 pares secuenciales con hueco de la BD local:
/// <b>0</b> tienen un cedente cuya grilla llegue al día de la entrega, y sólo <b>2</b> terminan con
/// saldo &gt; 0. Motivo: <c>rango_final.fecha_max</c> se cierra apenas <c>saldo_close</c> encuentra la
/// primera fecha ≥ último seguimiento con saldo ≈ 0, y todo ciclo bien operado termina en 0 — es la
/// propia regla R2 («al liquidar trasladan el sobrante fuera del galpón»). Cuando el alimento llega al
/// hueco, el cedente <b>ya vació su bodega</b>: no hay kilos que entregar ni día donde escribir la
/// entrega, y <see cref="AplicarTope"/> degrada a <c>INERTE</c> por el tope 0.
/// </para>
/// <para>
/// La consecuencia es de diseño, no de código: el alimento del hueco <b>no es del ciclo anterior</b> en
/// ningún sentido contable, así que no hay handoff que modelar. Lo que necesita es que la apertura del
/// DESTINO alcance más atrás (<c>dias_alimento_previo_encaset</c>, la ventana D4), que el plan excluye
/// como «otro feature». Está en el tracker esperando decisión de producto. Este cálculo se conserva
/// porque es correcto y fail-closed: mientras la decisión no exista, no mueve un solo kilo.
/// </para>
/// </summary>
public static class EntregaAlimentoCicloEngordeCalculos
{
    // Motivos: son el texto que la operación lee en la bandeja para entender por qué su marca hizo o
    // no hizo algo. Constantes para que el test y la UI hablen del mismo string.
    public const string MotivoSinGalpon = "El movimiento no tiene galpón: sin galpón no hay ciclo al que atribuir el alimento.";
    public const string MotivoAnulado = "El movimiento origen está anulado.";
    public const string MotivoNoEsEntrada = "Sólo se puede atribuir una entrada de alimento; una salida se comporta como siempre.";
    public const string MotivoSinCedente = "Ningún ciclo del galpón había sido encasetado cuando llegó el alimento.";
    public const string MotivoCedenteSinSeguimiento = "El ciclo que ocupaba el galpón todavía no cargó seguimiento: no hay día donde escribir la entrega.";
    public const string MotivoCedenteCongelado = "El ciclo que entrega ya está liquidado: su foto congelada no se reescribe.";
    public const string MotivoDentroDelCedente = "El alimento llegó mientras el ciclo anterior seguía en seguimiento, así que es suyo y lo está consumiendo.";
    public const string MotivoSinDestino = "Todavía no hay un ciclo posterior en el galpón. El alimento queda reservado.";
    public const string MotivoDestinoSinSeguimiento = "El ciclo destino todavía no cargó seguimiento. El alimento queda reservado hasta que arranque.";
    public const string MotivoConvivencia = "Los dos ciclos comparten bodega en el galpón: el alimento es de los dos y no hay nada que repartir.";
    public const string MotivoDentroDelDestino = "El ciclo destino ya venía cargando seguimiento cuando llegó el alimento: ya lo ve como fila propia.";
    public const string MotivoDestinoCongelado = "El ciclo destino ya está liquidado: su foto congelada no se reescribe.";
    public const string MotivoYaVisibleEnDestino = "El ciclo destino ya lo toma en su apertura por fecha: diferirlo lo contaría dos veces.";
    public const string MotivoSinRespaldo = "El ciclo que entrega ya consumió ese alimento: no queda saldo para entregar.";

    /// <summary>
    /// El ciclo en posesión del galpón cuando llegó el alimento: el de <b>máxima</b>
    /// <c>fecha_encaset</c> anterior o igual al día del movimiento. Desempate: el de id mayor.
    /// <para>
    /// Definición ESTRUCTURAL a propósito —depende del encaset, no del rango de seguimiento— para que
    /// no haya circularidad: el rango del cedente es justamente lo que la entrega modifica.
    /// </para>
    /// </summary>
    public static CicloGalponEngorde? ResolverCedente(
        IEnumerable<CicloGalponEngorde> ciclosDelGalpon,
        DateTime fechaMovimiento)
    {
        var d = fechaMovimiento.Date;
        CicloGalponEngorde? mejor = null;
        foreach (var c in ciclosDelGalpon)
        {
            if (c.FechaEncaset.Date > d) continue;
            if (mejor is null
                || c.FechaEncaset.Date > mejor.Value.FechaEncaset.Date
                || (c.FechaEncaset.Date == mejor.Value.FechaEncaset.Date && c.LoteId > mejor.Value.LoteId))
                mejor = c;
        }
        return mejor;
    }

    /// <summary>
    /// El ciclo que recibe: el de <b>mínima</b> <c>fecha_encaset</c> <b>estrictamente posterior</b> al
    /// día del movimiento. Desempate: el de id menor.
    /// <para>
    /// Criterio ya probado en la ronda 2 del intento anterior. Es lo que evita la multiplicación de la
    /// ronda 1, donde el predicado «¿existe un lote con primer seguimiento posterior?» no desempataba
    /// entre lotes SIN seguimiento y el mismo ingreso se veía en 4 lotes.
    /// </para>
    /// </summary>
    public static CicloGalponEngorde? ResolverDestino(
        IEnumerable<CicloGalponEngorde> ciclosDelGalpon,
        DateTime fechaMovimiento)
    {
        var d = fechaMovimiento.Date;
        CicloGalponEngorde? mejor = null;
        foreach (var c in ciclosDelGalpon)
        {
            if (c.FechaEncaset.Date <= d) continue;
            if (mejor is null
                || c.FechaEncaset.Date < mejor.Value.FechaEncaset.Date
                || (c.FechaEncaset.Date == mejor.Value.FechaEncaset.Date && c.LoteId < mejor.Value.LoteId))
                mejor = c;
        }
        return mejor;
    }

    /// <summary>
    /// ¿Los dos ciclos comparten bodega? Solape de rangos de seguimiento — el mismo predicado que la fn
    /// usa desde v10 (<c>consumo_galpon_por_fecha</c>) y v11 (<c>lotes_ajenos</c>, su complemento).
    /// <para>Un ciclo SIN seguimiento nunca convive: todavía no tiene rango.</para>
    /// </summary>
    public static bool Conviven(CicloGalponEngorde a, CicloGalponEngorde b)
        => a.SegMin.HasValue && a.SegMax.HasValue && b.SegMin.HasValue && b.SegMax.HasValue
           && a.SegMin.Value.Date <= b.SegMax.Value.Date
           && a.SegMax.Value.Date >= b.SegMin.Value.Date;

    /// <summary>
    /// El día donde se escribe la salida sintética del cedente: el <b>último día visible</b> que le
    /// queda, o sea el anterior al arranque del ciclo destino (corte <c>corte_ciclo_siguiente</c> de
    /// v14).
    /// <para>
    /// <b>Por qué ahí y no en el día del movimiento.</b> Un único delta negativo en el último día no
    /// deja ninguna fila posterior que pueda quedar negativa: la ausencia de negativos pasa de ser una
    /// esperanza a una consecuencia. Y no puede ser ANTES del movimiento —el saldo bajaría antes de
    /// subir— pero eso está garantizado: <c>VIGENTE</c> exige <c>d &lt; destino.SegMin</c>, así que
    /// <c>d ≤ destino.SegMin − 1</c>.
    /// </para>
    /// </summary>
    public static DateTime FechaEntrega(CicloGalponEngorde destino)
        => destino.SegMin!.Value.Date.AddDays(-1);

    /// <summary>
    /// Los kg que el cedente puede entregar de verdad: <c>LEAST(kg marcados, saldo del cedente a la
    /// fecha de entrega)</c>. El resto es el residuo de R2 — <b>no se compensa ni se esconde</b>: se
    /// SEÑALA.
    /// <para>
    /// El saldo lo aporta el llamador consultándolo a la fn, que es su dueña. Acá sólo se lo topa.
    /// Negativos se tratan como 0: no se puede entregar lo que no hay.
    /// </para>
    /// </summary>
    public static (decimal Entregados, decimal NoDiferible) TopeEntrega(decimal kgMarcados, decimal saldoCedente)
    {
        var disponible = saldoCedente > 0 ? saldoCedente : 0m;
        var entregados = kgMarcados <= disponible ? kgMarcados : disponible;
        if (entregados < 0) entregados = 0m;
        return (entregados, kgMarcados - entregados);
    }

    /// <summary>
    /// Clasifica el movimiento SIN mirar kg: devuelve el estado, los dos extremos y la fecha de
    /// entrega. El tope se aplica después, con <see cref="TopeEntrega"/>, porque necesita un saldo que
    /// sólo la fn conoce.
    /// <para>
    /// El ORDEN de las guardas es parte del contrato: cada una supone que las anteriores ya pasaron.
    /// </para>
    /// </summary>
    /// <param name="mov">El movimiento marcado.</param>
    /// <param name="ciclosDelGalpon">TODOS los ciclos vivos del mismo (granja, núcleo, galpón),
    /// incluido el cedente. Un ciclo borrado no debe llegar acá.</param>
    /// <param name="diasAlimentoPrevioEncaset">Ventana de alimento previo al encaset del destino (D4).
    /// Define desde cuándo el destino ya toma el movimiento por fecha, sin necesidad de la marca.</param>
    public static AtribucionEntregaEngorde Clasificar(
        MovimientoMarcadoEngorde mov,
        IEnumerable<CicloGalponEngorde> ciclosDelGalpon,
        int diasAlimentoPrevioEncaset)
    {
        static AtribucionEntregaEngorde Fin(string estado, string motivo, CicloGalponEngorde? ced = null, CicloGalponEngorde? des = null)
            => new(estado, ced?.LoteId, des?.LoteId, null, 0m, 0m, motivo);

        var d = mov.Fecha.Date;

        // 0. El movimiento ni siquiera es marcable. El endpoint ya lo rechaza, pero la carga masiva y
        //    el espejo escriben por otros caminos: acá no se puede confiar en el llamador.
        if (!mov.TieneGalpon) return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoSinGalpon);
        if (mov.Anulado) return Fin(EstadoEntregaAlimentoCiclo.Anulada, MotivoAnulado);

        // Una SALIDA marcada entraría a la apertura del destino como delta NEGATIVO. Defecto vivo de
        // la v15, que incluía INV_TRASLADO_SALIDA en el disyunto de la marca.
        if (!mov.EsEntrada) return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoNoEsEntrada);

        var ciclos = ciclosDelGalpon as IReadOnlyCollection<CicloGalponEngorde> ?? ciclosDelGalpon.ToList();

        // 1. El cedente: quién tenía el galpón cuando llegó el alimento.
        var cedente = ResolverCedente(ciclos, d);
        if (cedente is null) return Fin(EstadoEntregaAlimentoCiclo.Pendiente, MotivoSinCedente);
        var ced = cedente.Value;

        if (!ced.SegMin.HasValue || !ced.SegMax.HasValue)
            return Fin(EstadoEntregaAlimentoCiclo.Pendiente, MotivoCedenteSinSeguimiento, ced);

        // Una foto congelada no se reescribe: si el cedente no puede emitir la salida, el destino
        // recibiría kg sin contraparte y la suma dejaría de ser cero.
        if (ced.Congelado)
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoCedenteCongelado, ced);

        // 2. El destino: quién recibe. Se resuelve ANTES de las guardas que sólo miran al cedente
        //    porque R1 tiene precedencia: si los dos ciclos comparten bodega, ése es el motivo que la
        //    operación necesita leer, no «llegó dentro del ciclo anterior» (que también es cierto,
        //    porque conviven). Las dos ramas dan INERTE: lo que cambia es la explicación.
        var destino = ResolverDestino(ciclos, d);

        // 3. R1 — si comparten bodega el alimento es de LOS DOS y no hay nada que repartir. La marca
        //    sólo decide entre ciclos SECUENCIALES.
        if (destino is { } posible && Conviven(ced, posible))
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoConvivencia, ced, posible);

        // 4. 🔴 El alimento que llega MIENTRAS el cedente sigue en seguimiento es suyo y lo está
        //    consumiendo. Diferirlo descuadra el ciclo activo: el gate anterior lo midió en 43/G0055,
        //    donde el lote 86 «cierra con 1.100 kg de saldo» que son un fantasma contable (el stock
        //    físico del galpón coincide EXACTO con el saldo del ciclo siguiente). Entregarlos movía el
        //    cuadre de 1 a 2 galpones descuadrados: la firma exacta de la ronda 2.
        //    Consecuencia declarada: el feature cubre el alimento que cae en el HUECO entre ciclos.
        //    Va antes que «sin destino» a propósito: un movimiento que cae dentro del cedente no se va
        //    a diferir nunca, así que llamarlo PENDIENTE prometería algo que no va a pasar.
        if (d <= ced.SegMax.Value.Date)
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoDentroDelCedente, ced);

        if (destino is null) return Fin(EstadoEntregaAlimentoCiclo.Pendiente, MotivoSinDestino, ced);
        var des = destino.Value;

        if (!des.SegMin.HasValue)
            return Fin(EstadoEntregaAlimentoCiclo.Pendiente, MotivoDestinoSinSeguimiento, ced, des);

        // 5. `SegMin` puede PRECEDER al encaset, así que un movimiento posterior al arranque real del
        //    destino ya es una fila propia suya: diferirlo lo haría desaparecer de esa fila.
        if (d >= des.SegMin.Value.Date)
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoDentroDelDestino, ced, des);

        if (des.Congelado)
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoDestinoCongelado, ced, des);

        // 6. 🔑 Lo que mantiene la conservación exacta en 0,00: si el movimiento ya entra a la apertura
        //    NATURAL del destino (ventana previa al encaset de v9/D4), diferirlo lo contaría DOS veces.
        //    El corte de v12 no hace falta comprobarlo: la guarda 2 ya garantiza d > cedente.SegMax.
        var abreVentana = des.FechaEncaset.Date.AddDays(-Math.Abs(diasAlimentoPrevioEncaset));
        if (d >= abreVentana)
            return Fin(EstadoEntregaAlimentoCiclo.Inerte, MotivoYaVisibleEnDestino, ced, des);

        // 7. El caso que el feature existe para resolver: llegó en el hueco entre ciclos y demasiado
        //    antes del encaset como para que la ventana lo alcance. Sin la marca, esos kg se quedan en
        //    el cedente para siempre y el ciclo nuevo arranca en negativo.
        return new AtribucionEntregaEngorde(
            EstadoEntregaAlimentoCiclo.Vigente,
            ced.LoteId,
            des.LoteId,
            FechaEntrega(des),
            0m,   // los kg los pone TopeEntrega: necesitan el saldo del cedente, que es de la fn
            0m,
            $"Se entrega al lote {des.LoteId} el {FechaEntrega(des):yyyy-MM-dd}.");
    }

    /// <summary>
    /// Aplica el tope a una clasificación <c>VIGENTE</c> y devuelve el hecho definitivo. Si el cedente
    /// no tiene nada que entregar, la entrega <b>no se escribe</b>: degrada a <c>INERTE</c> con el
    /// residuo señalado (R2). Para cualquier otro estado devuelve la clasificación tal cual.
    /// </summary>
    /// <param name="clasificacion">Lo que devolvió <see cref="Clasificar"/>.</param>
    /// <param name="kgMarcadosHaciaEseDestino">Σ kg de TODOS los movimientos marcados del galpón que
    /// van al mismo destino. Se topan en conjunto: el saldo del cedente es uno solo.</param>
    /// <param name="saldoCedenteEnFechaEntrega">Saldo de alimento del cedente ese día, leído de la fn.</param>
    public static AtribucionEntregaEngorde AplicarTope(
        AtribucionEntregaEngorde clasificacion,
        decimal kgMarcadosHaciaEseDestino,
        decimal saldoCedenteEnFechaEntrega)
    {
        if (clasificacion.Estado != EstadoEntregaAlimentoCiclo.Vigente)
            return clasificacion;

        var (entregados, noDiferible) = TopeEntrega(kgMarcadosHaciaEseDestino, saldoCedenteEnFechaEntrega);

        if (entregados <= 0)
            return clasificacion with
            {
                Estado = EstadoEntregaAlimentoCiclo.Inerte,
                FechaEntrega = null,
                KgEntregados = 0m,
                KgNoDiferible = noDiferible,
                Motivo = MotivoSinRespaldo,
            };

        var parcial = noDiferible > 0;
        return clasificacion with
        {
            KgEntregados = entregados,
            KgNoDiferible = noDiferible,
            Motivo = parcial
                ? $"Se entregan {entregados:0.###} kg al lote {clasificacion.LoteDestinoId} el "
                  + $"{clasificacion.FechaEntrega:yyyy-MM-dd}; {noDiferible:0.###} kg ya los había consumido el ciclo anterior."
                : $"Se entregan {entregados:0.###} kg al lote {clasificacion.LoteDestinoId} el {clasificacion.FechaEntrega:yyyy-MM-dd}.",
        };
    }

    /// <summary>
    /// ¿Se puede tocar (anular o re-materializar) un hecho ya escrito?
    /// <para>
    /// <b>No, si está sellado.</b> Una entrega cuyo cedente o destino tiene liquidación congelada
    /// vigente es inmutable: sin esta regla, deshacerla después de congelar un extremo reabre
    /// exactamente el agujero del NO-GO —la foto congelada seguiría diciendo «entregué», y el otro
    /// lado ya no lo recibiría—. Tampoco se toca una que ya está anulada.
    /// </para>
    /// </summary>
    public static bool PuedeAnular(AlimentoEntregaCicloEngorde entrega)
        => !entrega.Sellada && entrega.Estado != EstadoEntregaAlimentoCiclo.Anulada;

    /// <summary>
    /// ¿Un hecho ya escrito quedó <b>sellado</b>? Se sella cuando CUALQUIERA de los dos extremos tiene
    /// liquidación congelada vigente: el handoff sólo es simétrico mientras los dos lados se puedan
    /// reescribir.
    /// </summary>
    public static bool DebeSellarse(int? loteCedenteId, int? loteDestinoId, IReadOnlySet<int> lotesCongelados)
        => (loteCedenteId.HasValue && lotesCongelados.Contains(loteCedenteId.Value))
        || (loteDestinoId.HasValue && lotesCongelados.Contains(loteDestinoId.Value));
}
