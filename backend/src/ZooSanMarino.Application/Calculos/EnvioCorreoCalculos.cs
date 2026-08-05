namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin I/O ni configuración) del envío de correo por SMTP: clasificación del error
/// que se guarda en <c>email_queue.error_type</c> y validación de que la configuración esté completa.
///
/// Viven aparte del servicio porque el envío falla en SILENCIO: si la clasificación miente, el
/// operador lee un motivo equivocado y persigue el problema donde no está — que es exactamente lo
/// que pasó entre junio y agosto de 2026, cuando un rechazo por política del tenant se venía
/// leyendo como "contraseña incorrecta".
/// </summary>
public static class EnvioCorreoCalculos
{
    /// <summary>La configuración SMTP está completa (host, usuario y contraseña).</summary>
    public static bool HayConfiguracionSmtp(string? host, string? username, string? password) =>
        !string.IsNullOrWhiteSpace(host)
        && !string.IsNullOrWhiteSpace(username)
        && !string.IsNullOrWhiteSpace(password);

    /// <summary>
    /// Clasifica el texto de diagnóstico SMTP en el <c>error_type</c> que se guarda en la cola.
    /// Los valores son los históricos de la tabla: no se renombran para no romper consultas viejas.
    /// </summary>
    public static string ClasificarErrorSmtp(string? errorDetails)
    {
        if (string.IsNullOrEmpty(errorDetails))
            return "unknown";

        if (errorDetails.Contains("Authentication") || errorDetails.Contains("535"))
            return "smtp_auth";
        if (errorDetails.Contains("network") || errorDetails.Contains("timeout") || errorDetails.Contains("connection"))
            return "network";
        if (errorDetails.Contains("invalid") || errorDetails.Contains("format") || errorDetails.Contains("address"))
            return "invalid_email";

        return "unknown";
    }

    /// <summary>
    /// Indica si el rechazo viene de una POLÍTICA del tenant y no de las credenciales.
    ///
    /// Verificado el 05-ago-2026 contra el tenant de sanmarino: las credenciales autentican (235) y
    /// el mismo código envía correctamente desde otra red. Cuando Exchange responde 5.7.139 / 5.7.57
    /// el problema es administrativo (Conditional Access o SMTP AUTH deshabilitado), y cambiar la
    /// contraseña no lo resuelve.
    /// </summary>
    public static bool EsRechazoPorPolitica(string? mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return false;

        return mensaje.Contains("5.7.139")
            || mensaje.Contains("5.7.57")
            || mensaje.Contains("5.7.30")
            || mensaje.Contains("Client not authenticated")
            || mensaje.Contains("Authentication unsuccessful");
    }

    /// <summary>Mensaje que se guarda cuando falta configuración SMTP: el correo queda en cola.</summary>
    public static string DiagnosticoSinConfiguracion() =>
        string.Join(Environment.NewLine, new[]
        {
            "No hay configuración SMTP utilizable: el correo queda en cola sin enviarse.",
            "  Hacen falta: Email:Smtp:Host, Email:Smtp:Username y Email:Smtp:Password.",
            "  En ECS son las variables Email__Smtp__Host, Email__Smtp__Username y Email__Smtp__Password."
        });
}
