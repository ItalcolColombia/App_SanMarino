namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas PURAS de la reversión de stock al deshacer un movimiento de inventario.
///
/// <para>
/// <b>El defecto que arreglan.</b> <c>EliminarIngresoAsync</c> y <c>EliminarTrasladoAsync</c> borraban
/// el movimiento y marcaban su fila del histórico como anulada —con lo cual la tabla diaria dejaba de
/// contarlo, que es lo correcto— pero <b>no tocaban <c>inventario_gestion_stock</c></b>. El resultado
/// es un descuadre permanente: el invariante del cuadre de alimento
/// (<c>saldo == stock − movimientos posteriores</c>) queda roto y <b>no hay forma de cerrarlo desde
/// la pantalla</b>, porque nada de lo que haga el usuario vuelve a tocar ese stock.
/// </para>
///
/// <para>
/// Medido el 25-ago-2026 sobre la copia de producción: Sacachún 3A / 685062 / G0044, ítem 5 —
/// <c>Σ movimientos = 7.720,000</c> contra <c>stock = 12.720,000</c>. La diferencia, 5.000,000 kg
/// exactos, es el ingreso duplicado de la remisión 63705 que la operación borró el 19-ago.
/// </para>
///
/// <para>
/// <b>Por qué la regla vive acá y no adentro del service.</b> El signo de la reversión depende
/// ÚNICAMENTE del <c>movement_type</c>, y equivocarlo no revienta: deja el stock movido para el lado
/// contrario, o sea el doble del error original, en silencio. Es exactamente la clase de decisión que
/// necesita tests propios. El service se queda con lo que no es puro: la transacción, el
/// <c>UPDATE</c> atómico y el rechazo cuando no hay saldo.
/// </para>
/// </summary>
public static class ReversionMovimientoInventarioCalculos
{
    /// <summary>Qué hay que hacerle al stock de la ubicación del movimiento para deshacerlo.</summary>
    public enum EfectoReversion
    {
        /// <summary>El movimiento no movió stock: deshacerlo tampoco debe moverlo.</summary>
        Ninguno = 0,

        /// <summary>El movimiento RESTÓ stock (una salida): revertirlo lo devuelve.</summary>
        Devolver = 1,

        /// <summary>El movimiento SUMÓ stock (una entrada): revertirlo lo descuenta.</summary>
        Descontar = 2,

        /// <summary>
        /// No se puede revertir automáticamente. Hoy solo <c>AjusteStock</c> y su hermano: el
        /// movimiento guarda <c>Math.Abs(delta)</c>, así que <b>perdió el signo</b> y la cantidad sola
        /// no alcanza para saber si el ajuste subió o bajó el stock.
        /// </summary>
        NoSoportado = 3
    }

    /// <summary>Tipos que SUMARON stock en la ubicación propia de su fila.</summary>
    private static readonly HashSet<string> Entradas = new(StringComparer.Ordinal)
    {
        "Ingreso",
        "TrasladoEntrada",
        "TrasladoInterGranjaEntrada"
    };

    /// <summary>Tipos que RESTARON stock en la ubicación propia de su fila.</summary>
    private static readonly HashSet<string> Salidas = new(StringComparer.Ordinal)
    {
        "Consumo",
        "TrasladoSalida",
        "TrasladoInterGranjaSalida",
        "EliminacionStock"
    };

    /// <summary>
    /// Tipos que NUNCA tocaron el stock.
    ///
    /// <para>
    /// 🔴 <c>TrasladoInterGranjaPendiente</c> es legado y es la trampa de esta tabla: <b>parece</b> una
    /// salida por el nombre, pero los registros con ese tipo descuentan el origen <b>al recibir</b>,
    /// no al crearse (ver <c>InventarioGestionService.Traslado.cs</c>, «Registros antiguos con
    /// movement_type TrasladoInterGranjaPendiente siguen descontando origen al recibir»). Devolverle
    /// stock al borrarlo <b>inventaría</b> alimento que nunca salió. <c>TrasladoInterGranjaRechazado</c>
    /// es un Pendiente rechazado, así que hereda lo mismo.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> SinEfecto = new(StringComparer.Ordinal)
    {
        "TrasladoInterGranjaPendiente",
        "TrasladoInterGranjaRechazado"
    };

    private static readonly HashSet<string> Ajustes = new(StringComparer.Ordinal)
    {
        "AjusteStock"
    };

    /// <summary>
    /// Mensaje de rechazo cuando el stock de la ubicación ya no alcanza para descontar la entrada que
    /// se quiere deshacer: los kilos ya se los llevó un consumo o un traslado posterior.
    ///
    /// <para>
    /// <b>Rechazar es lo correcto, no un límite.</b> Descontar igual dejaría el stock en negativo —el
    /// defecto que <c>DescontarStockAtomicoAsync</c> existe para impedir— y borrar sin descontar es
    /// justamente lo que produjo el descuadre de G0044. El camino sano es corregir primero el
    /// movimiento que consumió esos kilos.
    /// </para>
    /// </summary>
    public const string MensajeStockInsuficienteParaRevertir =
        "No se puede eliminar este movimiento: los kilos ya salieron de la ubicación " +
        "(un consumo o un traslado posterior se los llevó). Corrija primero ese movimiento.";

    /// <summary>Mensaje de rechazo para los tipos que no se pueden revertir automáticamente.</summary>
    public const string MensajeTipoNoReversible =
        "Este tipo de movimiento no se puede deshacer automáticamente porque no conserva el signo del " +
        "ajuste. Corrija el stock desde la pantalla de Stock.";

    /// <summary>
    /// Qué hacerle al stock para deshacer un movimiento de este tipo.
    ///
    /// <para>
    /// Un tipo <b>desconocido</b> devuelve <see cref="EfectoReversion.NoSoportado"/>, no
    /// <see cref="EfectoReversion.Ninguno"/>: si mañana aparece un <c>movement_type</c> nuevo que
    /// mueve stock, el fail-closed lo hace fallar ruidosamente en vez de repetir en silencio el
    /// defecto que este cálculo vino a cerrar.
    /// </para>
    /// </summary>
    public static EfectoReversion EfectoSobreStock(string? movementType)
    {
        var t = (movementType ?? string.Empty).Trim();
        if (t.Length == 0) return EfectoReversion.NoSoportado;
        if (Entradas.Contains(t)) return EfectoReversion.Descontar;
        if (Salidas.Contains(t)) return EfectoReversion.Devolver;
        if (SinEfecto.Contains(t)) return EfectoReversion.Ninguno;
        if (Ajustes.Contains(t)) return EfectoReversion.NoSoportado;
        return EfectoReversion.NoSoportado;
    }

    /// <summary>
    /// ¿La reversión de este movimiento necesita que haya stock disponible para poder aplicarse?
    /// Solo cuando hay que DESCONTAR: devolver stock nunca puede fallar por saldo.
    /// </summary>
    public static bool RequiereStockDisponible(string? movementType) =>
        EfectoSobreStock(movementType) == EfectoReversion.Descontar;

    /// <summary>
    /// Delta con signo que la reversión aplica al stock de la ubicación del movimiento. Positivo
    /// devuelve, negativo descuenta, cero no toca nada.
    ///
    /// <para>
    /// Se expone además del enum porque los tests de equivalencia y el resumen que ve el usuario
    /// razonan en kilos con signo, no en nombres de tipo.
    /// </para>
    /// </summary>
    public static decimal DeltaStock(string? movementType, decimal cantidad) =>
        EfectoSobreStock(movementType) switch
        {
            EfectoReversion.Devolver => Math.Abs(cantidad),
            EfectoReversion.Descontar => -Math.Abs(cantidad),
            _ => 0m
        };
}
