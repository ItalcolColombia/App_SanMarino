// Reglas PURAS del ciclo de vida de un lote de postura: cuándo se puede reabrir un lote de LEVANTE
// ya liquidado y cuándo un lote de PRODUCCIÓN está cerrado.
// Sin EF, sin estado, sin I/O: clasificación de filas, estado de cierre y armado de mensajes.
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Proyección mínima de una fila de <c>seguimiento_diario_produccion</c>, con lo justo para decidir
/// si la registró el USUARIO o la generó el SISTEMA al cerrar el levante. La consulta proyecta solo
/// estas columnas (el resto de la fila no participa de la decisión).
/// </summary>
/// <param name="Id">PK de la fila (para el detalle del mensaje y el borrado).</param>
/// <param name="Fecha">Fecha del registro (para el rango que se muestra al usuario).</param>
/// <param name="TipoAlimento">
/// Alimento de la fila. Las filas de sistema lo llevan en <see cref="CicloVidaPosturaCalculos.TipoAlimentoSistema"/>.
/// </param>
/// <param name="ConsKgH">Consumo de hembras (kg).</param>
/// <param name="ConsKgM">Consumo de machos (kg).</param>
/// <param name="MortalidadH">
/// Mortalidad de hembras. El traslado de sistema NO la toca (escribe en <c>SelH</c> y
/// <c>MortalidadM</c>), así que un valor positivo acá es captura del usuario.
/// </param>
/// <param name="SelM">
/// Selección de machos. Misma razón que <paramref name="MortalidadH"/>: el sistema no la toca.
/// </param>
/// <param name="HuevoTot">Huevos totales de la fila.</param>
/// <param name="HuevoTotArrastrado">
/// Huevos totales que el arrastre del levante declara haber volcado en esta fila
/// (<c>metadata.arrastreHuevosLevante.aplicado</c>, 0 si no hay marca). Todo excedente sobre este
/// número lo capturó el usuario.
/// </param>
public readonly record struct RegistroProduccionResumen(
    int Id,
    DateTime Fecha,
    string? TipoAlimento,
    decimal ConsKgH,
    decimal ConsKgM,
    int MortalidadH,
    int SelM,
    int HuevoTot,
    int HuevoTotArrastrado);

/// <summary>
/// Reglas puras del ciclo de vida Levante → Producción.
/// <para>
/// <b>Por qué existe:</b> al liquidar el levante, el sistema crea por su cuenta filas en
/// <c>seguimiento_diario_produccion</c> (el arrastre de huevos del levante y el traslado de aves del
/// cierre). Por eso "¿el lote de producción tiene seguimiento?" no se puede responder contando
/// filas: hay que distinguir las que generó el cierre —regenerables— de las que capturó el usuario,
/// que son datos reales y no se pueden perder en una reapertura.
/// </para>
/// <para>
/// <b>Criterio fail-closed:</b> solo se considera "de sistema" la fila que encaja EXACTAMENTE en el
/// patrón que producen el arrastre y el traslado. Cualquier otra cosa cuenta como del usuario, de
/// modo que ante la duda se bloquea la reapertura en vez de borrar captura.
/// </para>
/// </summary>
public static class CicloVidaPosturaCalculos
{
    /// <summary>
    /// Marcador de <c>tipo_alimento</c> de las filas que crea el sistema. La columna es NOT NULL y
    /// esas filas no tienen consumo; lo escriben literal <c>ArrastreHuevosLevanteService</c> y
    /// <c>MovimientoAvesService</c>. El formulario de producción, en cambio, siempre manda un
    /// alimento real (el control es <c>Validators.required</c> con default <c>"Standard"</c>).
    /// </summary>
    public const string TipoAlimentoSistema = "N/A";

    /// <summary>
    /// true si la fila la capturó el USUARIO (⇒ bloquea la reapertura del levante); false si la
    /// generó el cierre y por lo tanto se puede regenerar.
    /// <para>
    /// Basta con que se cumpla UNA de las cuatro señales de captura manual. Nótese que si el usuario
    /// registra el día sobre una fila de sistema, <c>ProduccionService</c> sobrescribe
    /// <c>tipo_alimento</c> con el alimento real, así que la fila pasa a contar como suya sola.
    /// </para>
    /// </summary>
    public static bool EsRegistroDeUsuario(RegistroProduccionResumen fila)
    {
        // 1) Alimento elegido por el usuario (el sistema deja el marcador).
        var alimento = (fila.TipoAlimento ?? string.Empty).Trim();
        if (alimento.Length > 0 &&
            !alimento.Equals(TipoAlimentoSistema, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2) Consumo capturado: las filas de sistema van siempre en cero.
        if (fila.ConsKgH + fila.ConsKgM > 0m) return true;

        // 3) Mortalidad de hembras o selección de machos: el traslado de sistema escribe en las
        //    OTRAS dos (SelH y MortalidadM), así que estas dos solo las mueve el usuario.
        if (fila.MortalidadH > 0 || fila.SelM > 0) return true;

        // 4) Huevos por encima de los que el arrastre declara haber volcado en esta fila.
        if (fila.HuevoTot > fila.HuevoTotArrastrado) return true;

        return false;
    }

    /// <summary>
    /// Filas capturadas por el usuario, en orden de fecha. Las demás son regenerables por el cierre.
    /// </summary>
    public static IReadOnlyList<RegistroProduccionResumen> FiltrarRegistrosDeUsuario(
        IEnumerable<RegistroProduccionResumen> filas) =>
        (filas ?? Enumerable.Empty<RegistroProduccionResumen>())
            .Where(EsRegistroDeUsuario)
            .OrderBy(f => f.Fecha)
            .ToList();

    /// <summary>
    /// <c>estado_cierre</c> cerrado. Cubre <c>"Cerrado"</c> (levante) y <c>"Cerrada"</c>
    /// (producción), case-insensitive y tolerante a espacios. Mismo criterio que REQ-006
    /// (<c>SeguimientoProduccionService.EstaCerrado</c>), centralizado acá para que los dos módulos
    /// no puedan divergir.
    /// </summary>
    public static bool EstaCerrado(string? estadoCierre) =>
        !string.IsNullOrWhiteSpace(estadoCierre) &&
        estadoCierre.Trim().StartsWith("Cerrad", StringComparison.OrdinalIgnoreCase);

    /// <summary>Complemento de <see cref="EstaCerrado"/> (un estado nulo/vacío se considera abierto).</summary>
    public static bool EstaAbierto(string? estadoCierre) => !EstaCerrado(estadoCierre);

    /// <summary>
    /// Mensaje de bloqueo de la reapertura del levante: dice cuántos registros hay, de qué fechas, y
    /// exactamente qué tiene que hacer el usuario y qué va a pasar después.
    /// </summary>
    /// <param name="loteProduccionNombre">Nombre del lote de producción (p. ej. <c>P-L001</c>).</param>
    /// <param name="registros">Registros del usuario que bloquean (se asume no vacío).</param>
    public static string ConstruirMensajeBloqueoReapertura(
        string? loteProduccionNombre,
        IReadOnlyList<RegistroProduccionResumen> registros)
    {
        var cantidad = registros?.Count ?? 0;
        var nombre = string.IsNullOrWhiteSpace(loteProduccionNombre)
            ? "asociado"
            : $"«{loteProduccionNombre.Trim()}»";

        var detalle = cantidad == 1
            ? $"1 registro de seguimiento diario ({FormatearFecha(registros![0].Fecha)})"
            : $"{cantidad} registros de seguimiento diario ({DescribirRango(registros!)})";

        return
            $"No se puede reabrir el lote de levante: el lote de producción {nombre} tiene {detalle}. " +
            "Elimine esos registros desde Seguimiento Diario de Producción y vuelva a intentarlo. " +
            "Al reabrir, el lote de producción se elimina y se vuelve a crear actualizado cuando " +
            "cierre el levante nuevamente.";
    }

    /// <summary>
    /// Mensaje de bloqueo cuando el lote de producción está CERRADO: hay que reabrirlo primero,
    /// porque reabrir el levante lo eliminaría.
    /// </summary>
    public static string ConstruirMensajeBloqueoProduccionCerrada(string? loteProduccionNombre)
    {
        var nombre = string.IsNullOrWhiteSpace(loteProduccionNombre)
            ? "asociado"
            : $"«{loteProduccionNombre.Trim()}»";

        return
            $"No se puede reabrir el lote de levante: el lote de producción {nombre} está cerrado. " +
            "Reabra primero el lote de producción desde Seguimiento Diario de Producción.";
    }

    /// <summary>
    /// Aviso que se muestra cuando la reapertura SÍ se puede hacer, para que el usuario sepa qué
    /// implica antes de confirmar.
    /// </summary>
    public static string ConstruirAvisoReaperturaPermitida(string? loteProduccionNombre)
    {
        if (string.IsNullOrWhiteSpace(loteProduccionNombre))
            return "Al reabrir, el lote de levante vuelve a quedar editable.";

        return
            $"Al reabrir, el lote de producción «{loteProduccionNombre.Trim()}» se eliminará junto " +
            "con los datos que generó el cierre (arrastre de huevos y traslado de aves). Se volverá " +
            "a crear, actualizado, cuando cierre el levante nuevamente.";
    }

    /// <summary>Rango de fechas "del dd/MM/yyyy al dd/MM/yyyy" (o una sola si todas coinciden).</summary>
    private static string DescribirRango(IReadOnlyList<RegistroProduccionResumen> registros)
    {
        var primera = registros.Min(r => r.Fecha);
        var ultima = registros.Max(r => r.Fecha);

        return primera.Date == ultima.Date
            ? FormatearFecha(primera)
            : $"del {FormatearFecha(primera)} al {FormatearFecha(ultima)}";
    }

    /// <summary>
    /// Fecha en dd/MM/yyyy con cultura invariante: el mensaje viaja al front como texto y no debe
    /// depender de la cultura del servidor.
    /// </summary>
    private static string FormatearFecha(DateTime fecha) =>
        fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
