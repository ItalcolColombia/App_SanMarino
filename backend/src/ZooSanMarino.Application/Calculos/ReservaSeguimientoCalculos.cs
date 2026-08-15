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
    /// </summary>
    public static int DisponibleAves(int saldo, int reservadoActivo) => saldo - reservadoActivo;
}
