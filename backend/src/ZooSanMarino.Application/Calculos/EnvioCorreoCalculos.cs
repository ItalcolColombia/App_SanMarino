using System.Text.Json.Serialization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Transporte de correo saliente resuelto a partir de la configuración.</summary>
public enum ProveedorCorreo
{
    /// <summary>Falta configuración utilizable: no se puede enviar (los correos quedan en cola).</summary>
    NoConfigurado = 0,
    /// <summary>SMTP clásico (usuario + contraseña). Sirve para desarrollo local y como rollback.</summary>
    Smtp = 1,
    /// <summary>Microsoft Graph API con client credentials (OAuth 2.0).</summary>
    Graph = 2
}

/// <summary>
/// Cálculos puros (sin I/O ni configuración) del envío de correo: elección del transporte,
/// clasificación de los errores de Microsoft Graph, armado del payload de <c>sendMail</c> y
/// vigencia del token cacheado.
///
/// Existen aparte del servicio porque son la parte que puede equivocarse en silencio: si el
/// proveedor se resuelve mal, la app arranca igual y los correos simplemente no salen. Acá son
/// deterministas y testeables.
/// </summary>
public static class EnvioCorreoCalculos
{
    /// <summary>Margen con el que se renueva el token antes de su vencimiento real.</summary>
    public static readonly TimeSpan MargenRenovacionToken = TimeSpan.FromMinutes(5);

    // ─────────────────────────────────────────────────────────────
    // Selección de transporte
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Indica si la configuración de Graph está COMPLETA. Parcial equivale a ausente: es preferible
    /// caer al SMTP conocido que intentar Graph con un tenant sin secreto y fallar en cada correo.
    /// </summary>
    public static bool HayConfiguracionGraph(
        string? tenantId, string? clientId, string? clientSecret, string? buzonRemitente) =>
        !string.IsNullOrWhiteSpace(tenantId)
        && !string.IsNullOrWhiteSpace(clientId)
        && !string.IsNullOrWhiteSpace(clientSecret)
        && !string.IsNullOrWhiteSpace(buzonRemitente);

    /// <summary>Indica si la configuración SMTP está completa.</summary>
    public static bool HayConfiguracionSmtp(string? host, string? username, string? password) =>
        !string.IsNullOrWhiteSpace(host)
        && !string.IsNullOrWhiteSpace(username)
        && !string.IsNullOrWhiteSpace(password);

    /// <summary>
    /// Resuelve el transporte a usar. <paramref name="provider"/> vacío o "auto" auto-detecta:
    /// Graph si está configurado, si no SMTP (comportamiento histórico del proyecto).
    /// Un <paramref name="provider"/> explícito manda, y si su configuración está incompleta
    /// devuelve <see cref="ProveedorCorreo.NoConfigurado"/> en vez de caer al otro por su cuenta:
    /// un fallback silencioso a SMTP en producción volvería a chocar con el retiro de auth básica
    /// y el síntoma sería idéntico al que motivó esta migración.
    /// </summary>
    public static ProveedorCorreo ResolverProveedor(string? provider, bool hayGraph, bool haySmtp)
    {
        var elegido = (provider ?? string.Empty).Trim().ToLowerInvariant();

        return elegido switch
        {
            "graph" => hayGraph ? ProveedorCorreo.Graph : ProveedorCorreo.NoConfigurado,
            "smtp" => haySmtp ? ProveedorCorreo.Smtp : ProveedorCorreo.NoConfigurado,
            _ when hayGraph => ProveedorCorreo.Graph,
            _ when haySmtp => ProveedorCorreo.Smtp,
            _ => ProveedorCorreo.NoConfigurado
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Token de aplicación (client credentials)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Indica si un token cacheado sigue sirviendo. Se renueva con
    /// <see cref="MargenRenovacionToken"/> de anticipación para que un envío no arranque con un
    /// token que vence a mitad de la llamada.
    /// </summary>
    public static bool TokenVigente(DateTimeOffset expiraUtc, DateTimeOffset ahoraUtc) =>
        TokenVigente(expiraUtc, ahoraUtc, MargenRenovacionToken);

    /// <inheritdoc cref="TokenVigente(DateTimeOffset, DateTimeOffset)"/>
    public static bool TokenVigente(DateTimeOffset expiraUtc, DateTimeOffset ahoraUtc, TimeSpan margen) =>
        expiraUtc - margen > ahoraUtc;

    /// <summary>Momento de vencimiento a partir del <c>expires_in</c> (segundos) que devuelve Entra ID.</summary>
    public static DateTimeOffset CalcularVencimientoToken(int expiresInSegundos, DateTimeOffset ahoraUtc) =>
        ahoraUtc.AddSeconds(expiresInSegundos <= 0 ? 0 : expiresInSegundos);

    // ─────────────────────────────────────────────────────────────
    // Payload de Microsoft Graph /sendMail
    // ─────────────────────────────────────────────────────────────

    public sealed record GraphEmailAddress(
        [property: JsonPropertyName("address")] string Address,
        [property: JsonPropertyName("name")] string? Name);

    public sealed record GraphRecipient(
        [property: JsonPropertyName("emailAddress")] GraphEmailAddress EmailAddress);

    public sealed record GraphBody(
        [property: JsonPropertyName("contentType")] string ContentType,
        [property: JsonPropertyName("content")] string Content);

    public sealed record GraphMessage(
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("body")] GraphBody Body,
        [property: JsonPropertyName("toRecipients")] IReadOnlyList<GraphRecipient> ToRecipients,
        [property: JsonPropertyName("from")] GraphRecipient From);

    public sealed record GraphSendMailPayload(
        [property: JsonPropertyName("message")] GraphMessage Message,
        [property: JsonPropertyName("saveToSentItems")] bool SaveToSentItems);

    /// <summary>
    /// Arma el cuerpo de <c>POST /v1.0/users/{buzon}/sendMail</c>.
    ///
    /// El campo <c>from</c> lleva SIEMPRE la dirección del buzón desde el que se envía (el de la URL):
    /// así se conserva el nombre visible del remitente que hoy da el SMTP sin necesitar el permiso
    /// <c>SendAs</c>, que sí haría falta para suplantar otra casilla.
    /// </summary>
    public static GraphSendMailPayload ConstruirPayloadSendMail(
        string buzonRemitente,
        string? nombreRemitente,
        string destinatario,
        string asunto,
        string cuerpoHtml,
        bool guardarEnEnviados) =>
        new(
            new GraphMessage(
                Subject: asunto,
                Body: new GraphBody("HTML", cuerpoHtml),
                ToRecipients: new[] { new GraphRecipient(new GraphEmailAddress(destinatario, null)) },
                From: new GraphRecipient(new GraphEmailAddress(
                    buzonRemitente,
                    string.IsNullOrWhiteSpace(nombreRemitente) ? null : nombreRemitente))),
            guardarEnEnviados);

    // ─────────────────────────────────────────────────────────────
    // Clasificación y diagnóstico de errores
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Traduce el estado HTTP de Graph al <c>error_type</c> que se guarda en <c>email_queue</c>.
    /// Los valores son estables: se consultan para saber si un fallo es de credenciales, de
    /// permisos o pasajero, sin leer el texto largo.
    /// </summary>
    public static string ClasificarErrorGraph(int httpStatus) => httpStatus switch
    {
        401 => "graph_auth",
        403 => "graph_permisos",
        404 => "graph_buzon",
        429 => "graph_throttling",
        >= 500 => "graph_transitorio",
        _ => $"graph_http_{httpStatus}"
    };

    /// <summary>Indica si conviene reintentar: sólo throttling y fallas transitorias del servicio.</summary>
    public static bool EsErrorTransitorioGraph(int httpStatus) =>
        httpStatus == 429 || httpStatus >= 500;

    /// <summary>
    /// Texto accionable para <c>email_queue.error_message</c> y los logs. Dice QUÉ hacer, porque
    /// quien lo lee en producción no tiene el código a mano.
    /// </summary>
    public static string DiagnosticoGraph(
        int httpStatus, string? codigoGraph, string? mensajeGraph, string buzonRemitente, string clientId)
    {
        var detalle = httpStatus switch
        {
            401 =>
                "Entra ID rechazó el token. Verificar Email:Graph:ClientSecret (los secretos vencen: " +
                "revisar 'Certificates & secrets' de la app registration) y que TenantId/ClientId sean los correctos.",
            403 =>
                "La aplicación no tiene permiso para enviar desde este buzón. Verificar que en la app " +
                "registration esté el permiso de APLICACIÓN Mail.Send de Microsoft Graph CON consentimiento " +
                "de administrador otorgado. Si existe una Application Access Policy, confirmar que incluya " +
                $"el buzón {buzonRemitente}.",
            404 =>
                $"Graph no encuentra el buzón {buzonRemitente}. Verificar que Email:Graph:SenderMailbox sea " +
                "una casilla real de Exchange Online del tenant (no un alias ni un grupo) y esté licenciada.",
            429 =>
                "Microsoft Graph está limitando el envío (throttling). El correo se reintenta solo; " +
                "si persiste, bajar la frecuencia o el lote del procesador de cola.",
            >= 500 =>
                "Error temporal del servicio de Microsoft. El correo se reintenta automáticamente.",
            400 =>
                "Graph rechazó la solicitud. Causa habitual: dirección de destino inválida o cuerpo del " +
                "mensaje mal formado.",
            _ => "Respuesta inesperada de Microsoft Graph."
        };

        var lineas = new List<string>
        {
            "Error de Microsoft Graph al enviar el correo:",
            $"  HTTP Status: {httpStatus}",
            $"  Código Graph: {(string.IsNullOrWhiteSpace(codigoGraph) ? "(no informado)" : codigoGraph)}",
            $"  Mensaje Graph: {(string.IsNullOrWhiteSpace(mensajeGraph) ? "(no informado)" : mensajeGraph)}",
            $"  Buzón remitente: {buzonRemitente}",
            $"  Client ID: {clientId}",
            $"  Diagnóstico: {detalle}"
        };

        return string.Join(Environment.NewLine, lineas);
    }

    /// <summary>Diagnóstico del fallo al PEDIR el token, que ni siquiera llega a Graph.</summary>
    public static string DiagnosticoTokenGraph(
        int httpStatus, string? errorCode, string? errorDescription, string tenantId, string clientId)
    {
        var detalle = (errorCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "invalid_client" =>
                "El client secret es inválido o venció. Generar uno nuevo en 'Certificates & secrets' de la " +
                "app registration y actualizar Email:Graph:ClientSecret.",
            "unauthorized_client" =>
                "La aplicación no está habilitada para el flujo client_credentials en este tenant.",
            "invalid_request" =>
                "Faltan parámetros o el TenantId no corresponde. Verificar Email:Graph:TenantId.",
            _ => "No se pudo obtener el token de aplicación de Entra ID."
        };

        var lineas = new List<string>
        {
            "Error obteniendo el token de Entra ID (client credentials):",
            $"  HTTP Status: {httpStatus}",
            $"  Error: {(string.IsNullOrWhiteSpace(errorCode) ? "(no informado)" : errorCode)}",
            $"  Descripción: {(string.IsNullOrWhiteSpace(errorDescription) ? "(no informada)" : errorDescription)}",
            $"  Tenant ID: {tenantId}",
            $"  Client ID: {clientId}",
            $"  Diagnóstico: {detalle}"
        };

        return string.Join(Environment.NewLine, lineas);
    }

    /// <summary>
    /// Mensaje que se guarda cuando NO hay transporte utilizable. Es el caso que antes tumbaba el
    /// arranque de la app con una excepción en el constructor del BackgroundService.
    /// </summary>
    public static string DiagnosticoSinProveedor(string? providerSolicitado)
    {
        var solicitado = string.IsNullOrWhiteSpace(providerSolicitado) ? "(auto)" : providerSolicitado.Trim();

        return string.Join(Environment.NewLine, new[]
        {
            "No hay un transporte de correo configurado: el correo queda en cola sin enviarse.",
            $"  Email:Provider solicitado: {solicitado}",
            "  Para Microsoft Graph hacen falta: Email:Graph:TenantId, Email:Graph:ClientId, " +
            "Email:Graph:ClientSecret y Email:Graph:SenderMailbox.",
            "  Para SMTP hacen falta: Email:Smtp:Host, Email:Smtp:Username y Email:Smtp:Password.",
            "  Nota: Exchange Online retiró la autenticación básica de SMTP Client Submission " +
            "(550 5.7.30), por lo que SMTP con usuario/contraseña ya no sirve contra Office 365."
        });
    }
}
