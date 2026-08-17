namespace ZooSanMarino.Application.Calculos;

/// <summary>Una línea de alimento separada (reservada) por un seguimiento sin validar.</summary>
/// <param name="Item">Ítem y su origen (inventario unificado o catálogo), más el silo si aplica.</param>
/// <param name="Kg">Kilos separados. Siempre &gt; 0: las líneas en cero no se persisten.</param>
public readonly record struct ReservaAlimentoLinea(ItemConsumoKey Item, decimal Kg);

/// <summary>Aves separadas (dadas de baja pero todavía no descontadas) por un seguimiento sin validar.</summary>
/// <param name="Hembras">Bajas de hembras en un lote con sexos.</param>
/// <param name="Machos">Bajas de machos en un lote con sexos.</param>
/// <param name="Mixtas">Bajas en un lote mixto, donde el saldo es uno solo y no está sexado.</param>
public readonly record struct ReservaAvesLineas(int Hembras, int Machos, int Mixtas)
{
    /// <summary>True si no hay nada que separar (día sin bajas): no se persiste ninguna fila.</summary>
    public bool EstaVacia => Hembras <= 0 && Machos <= 0 && Mixtas <= 0;

    /// <summary>Total de aves separadas, sin distinguir el pool del que salen.</summary>
    public int Total => Math.Max(0, Hembras) + Math.Max(0, Machos) + Math.Max(0, Mixtas);
}

/// <summary>
/// Reglas PURAS de la <b>separación</b> (reserva) de alimento y aves que produce un seguimiento
/// diario mientras está pendiente de validar.
///
/// <para>
/// <b>Qué problema resuelve la separación.</b> El usuario lo planteó así: «el mismo galpón que tiene
/// el alimento puede utilizarse en dos lotes y necesito separar lo que se consumió». Hasta ahora el
/// disponible que ve el formulario es el stock crudo, así que dos lotes que comen del mismo galpón
/// ven los mismos kilos y los dos creen tenerlos. Con la reserva, el disponible pasa a ser
/// <c>stock − reservas activas</c> y el segundo lote ve el galpón ya comprometido.
/// </para>
///
/// <para>
/// <b>Por qué separar en vez de descontar.</b> Porque el registro todavía se puede editar y borrar.
/// Si se descontara, cada corrección obligaría a un movimiento de devolución y a recalcular saldos —
/// que es exactamente el trabajo manual «por debajo» que esta funcionalidad viene a eliminar—. Al
/// separar, editar es <b>reescribir la reserva</b>: no hay nada que devolver porque nunca se
/// descontó. Ver <see cref="ReescribirEnEdicion"/>.
/// </para>
/// </summary>
public static class ReservaSeguimientoCalculos
{
    // ─── Alimento ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Convierte el consumo parseado de la metadata en las líneas a separar. Se descartan los ítems
    /// en cero o negativos: una reserva de 0 kg no compromete nada y solo ensucia el índice único.
    /// </summary>
    public static IReadOnlyList<ReservaAlimentoLinea> LineasDeAlimento(
        IReadOnlyDictionary<ItemConsumoKey, decimal> consumoPorItem)
    {
        if (consumoPorItem is null || consumoPorItem.Count == 0)
            return Array.Empty<ReservaAlimentoLinea>();

        return consumoPorItem
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key.Id).ThenBy(kv => kv.Key.SiloId ?? 0)
            .Select(kv => new ReservaAlimentoLinea(kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>
    /// Líneas que quedan vigentes al EDITAR un registro pendiente: son <b>exactamente las nuevas</b>.
    ///
    /// <para>
    /// La firma recibe las viejas aunque no las use, y eso es a propósito: documenta el invariante que
    /// distingue a la reserva del descuento. En el modelo viejo había que calcular
    /// <c>nuevo − viejo</c> y emitir consumos o devoluciones según el signo (ver el bloque de diff de
    /// <c>SeguimientoLoteLevanteService.UpdateAsync</c>); acá las viejas simplemente se liberan y se
    /// escriben las nuevas. Un test fija que el resultado no depende de las viejas.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ReservaAlimentoLinea> ReescribirEnEdicion(
        IReadOnlyList<ReservaAlimentoLinea> vigentes,
        IReadOnlyDictionary<ItemConsumoKey, decimal> consumoNuevo)
    {
        _ = vigentes; // ver el remarks: la reserva no hace diff, reescribe.
        return LineasDeAlimento(consumoNuevo);
    }

    // ─── Aves ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aves a separar por las bajas del día (mortalidad + selección + error de sexaje).
    ///
    /// <para>
    /// La suma delega en <see cref="RetiroAvesEngordeCalculos.BajasDelDia"/> para no tener una segunda
    /// definición de «baja del día» — es el mismo criterio que aplica el descuento real, y si las dos
    /// divergen la reserva liberaría una cantidad distinta a la que se descuenta al validar.
    /// </para>
    ///
    /// <para>
    /// En un lote <b>mixto</b> el saldo no está sexado: el formulario colapsa todo en una columna y la
    /// captura llega en los campos de hembras. Por eso el total va a <see cref="ReservaAvesLineas.Mixtas"/>
    /// y los otros dos quedan en cero — el disponible contra el que hay que restar es el de mixtas.
    /// </para>
    /// </summary>
    public static ReservaAvesLineas LineasDeAves(
        int mortalidadHembras, int selHembras, int errorSexajeHembras,
        int mortalidadMachos, int selMachos, int errorSexajeMachos,
        bool loteEsMixto)
    {
        var (hembras, machos) = RetiroAvesEngordeCalculos.BajasDelDia(
            mortalidadHembras, selHembras, errorSexajeHembras,
            mortalidadMachos, selMachos, errorSexajeMachos);

        return loteEsMixto
            ? new ReservaAvesLineas(0, 0, hembras + machos)
            : new ReservaAvesLineas(hembras, machos, 0);
    }

    // ─── Disponible ───────────────────────────────────────────────────────────

    /// <summary>
    /// Cantidad realmente disponible de un ítem en una ubicación: el stock menos lo que las reservas
    /// activas ya comprometieron.
    ///
    /// <para>
    /// <b>No se recorta a cero.</b> Un disponible negativo significa que se separó más de lo que hay
    /// —dos lotes cargando sobre el mismo galpón— y esconderlo detrás de un 0 borra justamente la
    /// señal que hay que ver. El formulario lo muestra en rojo; la validación de tope lo rechaza.
    /// </para>
    /// </summary>
    public static decimal DisponibleAlimento(decimal stock, decimal reservadoActivo) => stock - reservadoActivo;

    /// <summary>
    /// Aves realmente disponibles: el saldo del lote menos las que un seguimiento sin validar ya dio
    /// de baja. Sin esto, un traslado o una venta pueden despachar aves que ya están muertas en un
    /// registro pendiente. Tampoco se recorta a cero, por el mismo motivo.
    ///
    /// <para>
    /// ⚠️ <b>Aplicar SOLO cuando el saldo sale del MAESTRO</b> (<c>lote_postura_levante.aves_h_actual</c>,
    /// <c>lote_postura_produccion.aves_h_actual</c>), que con doble validación no se descontó.
    /// </para>
    ///
    /// <para>
    /// <b>NO aplicar donde el saldo ya se calcula desde las filas del seguimiento</b>, porque ahí las
    /// bajas sin validar ya están adentro y restarlas otra vez las cuenta DOS VECES. Eso pasa en:
    /// <list type="bullet">
    /// <item><c>LoteService.GetMortalidadResumenAsync</c> (levante con lote base): el saldo es
    /// <c>base − mortCaja − mort − sel − err + trasIn − trasOut</c> sumando <c>seguimiento_diario</c>.</item>
    /// <item><c>AvesDisponiblesEngordeCalculos.DisponiblesPorSexo</c> (engorde y reproductora): resta
    /// <c>registradas − aplicadas</c>, y un registro sin validar es registrado-pero-no-aplicado.</item>
    /// </list>
    /// El doble descuento no es hipotético: <c>AvesDisponiblesEngordeCalculos</c> existe porque ya
    /// ocurrió, bloqueando despachos de lotes que sí tenían aves.
    /// </para>
    /// </summary>
    public static int DisponibleAves(int saldo, int reservadoActivo) => saldo - reservadoActivo;

    // ─── Aplicabilidad de la reserva ──────────────────────────────────────────

    /// <summary>
    /// Motivo por el que una reserva de alimento <b>no se puede aplicar</b>, o <c>null</c> si sí.
    ///
    /// <para>
    /// Existe porque el modo de fallar importaba más que el fallo. La aplicación recorría los grupos de
    /// reserva y hacía <c>continue</c> sobre <see cref="ModeloInventarioConsumo.Ninguno"/>: el registro
    /// quedaba <c>validado = true</c>, las reservas <c>APLICADA</c> y el inventario intacto —y el
    /// endpoint devolvía igual el total de kilos, porque se sumaba antes del bucle—. Un descuento que no
    /// ocurre y encima se reporta como ocurrido no se descubre nunca; solo aparece meses después en el
    /// cuadre.
    /// </para>
    ///
    /// <para>
    /// <c>Ninguno</c> no es un caso legítimo: la tabla <c>paises</c> tiene tres filas y las tres mapean
    /// a un modelo (Colombia a nivel granja, Ecuador y Panamá con núcleo/galpón). Que el gate devuelva
    /// <c>Ninguno</c> significa siempre <b>país sin resolver</b> — la reserva se guardó con
    /// <c>pais_id</c> 0 o nulo—, y contra eso lo correcto es no validar.
    /// </para>
    /// </summary>
    /// <param name="modelo">Modelo resuelto desde el <c>pais_id</c> que la reserva persistió.</param>
    /// <param name="kg">Kilos del grupo. En cero no hay nada que aplicar y nada que reclamar.</param>
    /// <param name="paisId">País guardado, para nombrarlo en el mensaje.</param>
    /// <param name="loteRef">Lote legible, para que el mensaje diga sobre qué hay que actuar.</param>
    public static string? MotivoAlimentoNoAplicable(
        ModeloInventarioConsumo modelo, decimal kg, int paisId, string? loteRef)
    {
        if (kg <= 0) return null;
        if (modelo != ModeloInventarioConsumo.Ninguno) return null;

        var lote = string.IsNullOrWhiteSpace(loteRef) ? "" : $" del lote '{loteRef}'";
        return $"No se puede validar: los {kg:0.###} kg separados{lote} quedaron guardados con el país sin " +
               $"resolver (pais_id {paisId}), así que no hay inventario contra el cual descontarlos. " +
               "Corregí el país del lote o de su granja y volvé a guardar el registro.";
    }

    /// <summary>
    /// Motivo por el que las aves separadas <b>no se pueden descontar</b>, o <c>null</c> si sí.
    ///
    /// <para>
    /// El descuento de aves <b>recorta en cero</b> (<c>DescuentoAvesSeguimientoCalculos.AplicarDelta</c>
    /// y <c>RetiroAvesEngordeCalculos</c>), y ese clamp es histórico: cambiarlo movería saldos de todas
    /// las empresas, así que no se toca. Pero el clamp hace que la operación <b>no sea reversible</b>:
    /// si el saldo llega a 0 y se sigue descontando, des-validar suma de vuelta el número completo y
    /// deja el lote con MÁS aves de las que tenía. Verificado en runtime: un lote en 0 pasó a 5 tras
    /// validar y des-validar.
    /// </para>
    ///
    /// <para>
    /// Por eso la doble validación no valida a medias: si el saldo no alcanza, se rechaza — igual que
    /// el alimento, que ya se rechaza con <c>ValidarStockConsumoAsync</c> cuando falta stock. Así el
    /// clamp nunca se alcanza desde este camino y validar/des-validar es reversible por construcción.
    /// Con el flag apagado nada de esto corre: el comportamiento previo queda intacto.
    /// </para>
    /// </summary>
    /// <param name="disponibleTotal">Aves del maestro, sumando los tres buckets.</param>
    /// <param name="bajasTotal">Aves separadas por el registro, sumando los tres buckets.</param>
    /// <param name="loteRef">Lote legible, para que el mensaje diga sobre qué actuar.</param>
    public static string? MotivoAvesNoAplicable(int disponibleTotal, int bajasTotal, string? loteRef)
    {
        if (bajasTotal <= 0) return null;
        if (disponibleTotal >= bajasTotal) return null;

        var lote = string.IsNullOrWhiteSpace(loteRef) ? "" : $" del lote '{loteRef}'";
        return $"No se puede validar: el registro da de baja {bajasTotal} aves{lote} y el saldo tiene " +
               $"{disponibleTotal}. Corregí las bajas del registro, o el saldo del lote, antes de validar.";
    }

    /// <summary>
    /// Motivo por el que un despacho (venta o traslado) <b>no puede salir</b> del saldo MAESTRO de un
    /// lote de postura, o <c>null</c> si sí puede.
    ///
    /// <para>
    /// Es el mismo agujero que el traslado desde seguimiento ya cerró, del otro lado: con doble
    /// validación el maestro (<c>aves_h_actual</c>) <b>no se descontó</b>, así que las aves que un
    /// registro pendiente ya dio de baja siguen figurando como disponibles y se pueden vender. La
    /// venta después las resta con <c>Math.Max(0, …)</c>, o sea que el sobregiro no falla: se recorta
    /// en silencio y el lote queda mintiendo en cero.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Sólo mira lo RESERVADO.</b> Sin reservas activas devuelve <c>null</c> siempre, aunque el
    /// pedido exceda el saldo: ese caso es preexistente, lo cubre (mal) el chequeo contra
    /// <c>inventario_aves</c>, y meterlo acá cambiaría el comportamiento de las cinco empresas por un
    /// motivo que no es este. Con el flag apagado no hay reservas ⇒ resultado idéntico al de hoy.
    /// </para>
    ///
    /// <para>
    /// Se evalúa <b>por sexo</b>: 100 hembras reservadas no habilitan despachar 100 machos.
    /// </para>
    /// </summary>
    /// <param name="saldoHembras">Hembras del maestro del lote, antes de este movimiento.</param>
    /// <param name="saldoMachos">Machos del maestro del lote, antes de este movimiento.</param>
    /// <param name="reservadasHembras">Hembras que un seguimiento sin validar ya dio de baja.</param>
    /// <param name="reservadasMachos">Machos que un seguimiento sin validar ya dio de baja.</param>
    /// <param name="pedidasHembras">Hembras que el movimiento quiere sacar.</param>
    /// <param name="pedidasMachos">Machos que el movimiento quiere sacar.</param>
    /// <param name="loteRef">Lote legible, para que el mensaje diga sobre qué actuar.</param>
    public static string? MotivoDespachoNoDisponible(
        int saldoHembras, int saldoMachos,
        int reservadasHembras, int reservadasMachos,
        int pedidasHembras, int pedidasMachos,
        string? loteRef)
    {
        if (reservadasHembras <= 0 && reservadasMachos <= 0) return null;

        var faltaH = pedidasHembras > 0 && pedidasHembras > DisponibleAves(saldoHembras, reservadasHembras);
        var faltaM = pedidasMachos > 0 && pedidasMachos > DisponibleAves(saldoMachos, reservadasMachos);
        if (!faltaH && !faltaM) return null;

        var lote = string.IsNullOrWhiteSpace(loteRef) ? "" : $" del lote '{loteRef}'";
        var detalle = faltaH && faltaM
            ? $"hembras {pedidasHembras} sobre {DisponibleAves(saldoHembras, reservadasHembras)} y " +
              $"machos {pedidasMachos} sobre {DisponibleAves(saldoMachos, reservadasMachos)}"
            : faltaH
                ? $"hembras {pedidasHembras} sobre {DisponibleAves(saldoHembras, reservadasHembras)}"
                : $"machos {pedidasMachos} sobre {DisponibleAves(saldoMachos, reservadasMachos)}";

        return $"No hay aves disponibles{lote}: se piden {detalle}. " +
               $"El saldo es {saldoHembras} H / {saldoMachos} M, pero {reservadasHembras} H / " +
               $"{reservadasMachos} M ya están dadas de baja en registros de seguimiento sin validar. " +
               "Validá esos registros (o corregilos) antes de despachar.";
    }
}
