// src/ZooSanMarino.Infrastructure/Services/Email/SmtpEmailSender.cs
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Transporte SMTP clásico (usuario + contraseña). Es el código que vivía dentro de
/// <c>EmailQueueProcessorService</c>, trasladado sin cambios de comportamiento: mismos mensajes de
/// diagnóstico, mismos <c>error_type</c> y el mismo timeout de 60 s.
///
/// ⚠️ Contra Office 365 ya NO funciona: Exchange Online retiró la autenticación básica para SMTP
/// Client Submission (<c>550 5.7.30</c>). Se conserva para desarrollo local, para servidores SMTP
/// propios y como rollback inmediato (<c>Email:Provider=smtp</c>).
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;

    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly bool _smtpEnableSsl;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public string Nombre => "smtp";

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;

        _smtpHost = configuration["Email:Smtp:Host"] ?? string.Empty;
        var portStr = configuration["Email:Smtp:Port"];
        _smtpPort = int.TryParse(portStr, out var port) ? port : 587;
        _smtpUsername = configuration["Email:Smtp:Username"] ?? string.Empty;
        _smtpPassword = configuration["Email:Smtp:Password"] ?? string.Empty;
        var sslStr = configuration["Email:Smtp:EnableSsl"];
        _smtpEnableSsl = bool.TryParse(sslStr, out var ssl) ? ssl : true;
        _fromEmail = configuration["Email:From:Address"] ?? _smtpUsername;
        _fromName = configuration["Email:From:Name"] ?? "ItalGranja";
    }

    public async Task<EnvioCorreoResultado> EnviarAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        try
        {
            // Configuración mejorada para Office 365
            // Para puerto 587, Office 365 requiere STARTTLS (EnableSsl = true)
            // Para puerto 465, Office 365 requiere SSL directo
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _smtpEnableSsl, // Debe ser true para puerto 587 (STARTTLS)
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 60000, // 60 segundos (aumentado para Office 365)
                UseDefaultCredentials = false // Importante: no usar credenciales por defecto
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.Normal
            };

            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message, cancellationToken);
            return EnvioCorreoResultado.Ok();
        }
        catch (SmtpException ex)
        {
            // Capturar información detallada del error SMTP
            var smtpDetails = BuildSmtpExceptionDetails(ex, toEmail);

            _logger.LogError(ex, "Error SMTP al enviar correo a {ToEmail}: {Message} | Details: {Details}",
                toEmail, ex.Message, smtpDetails);

            // Log detallado para diagnóstico según el tipo de error
            var statusCodeName = ex.StatusCode.ToString();

            if (statusCodeName.Contains("MustIssueStartTlsFirst") || ex.Message.Contains("MustIssueStartTlsFirst"))
            {
                _logger.LogError("🔴 ERROR CRÍTICO: Office 365 requiere STARTTLS antes de autenticarse");
                _logger.LogError("   Solución:");
                _logger.LogError("   1. Verificar que EnableSsl esté en 'true' en la configuración");
                _logger.LogError("   2. Para puerto 587: EnableSsl debe ser true (usa STARTTLS)");
                _logger.LogError("   3. Para puerto 465: EnableSsl debe ser true (usa SSL directo)");
                _logger.LogError("   4. Configuración actual: Host={Host}, Port={Port}, EnableSsl={Ssl}",
                    _smtpHost, _smtpPort, _smtpEnableSsl);
            }
            else if (ex.Message.Contains("535") || ex.Message.Contains("Authentication") ||
                     ex.Message.Contains("5.7.139") || ex.Message.Contains("Client not authenticated") ||
                     ex.Message.Contains("5.7.57") || ex.Message.Contains("5.7.30"))
            {
                _logger.LogError("🔴 OFICE 365 RECHAZÓ LA AUTENTICACIÓN (5.7.139 / 5.7.57)");
                _logger.LogError("   ⚠️ Verificado el 05-ago-2026: las credenciales de {User} son VÁLIDAS y este", _smtpUsername);
                _logger.LogError("   mismo código envía correctamente desde otras redes. Un rechazo acá significa");
                _logger.LogError("   que una POLÍTICA DEL TENANT bloquea la autenticación desde el origen del");
                _logger.LogError("   servidor, no que la contraseña esté mal. Cambiarla NO lo resuelve.");
                _logger.LogError("   Qué pedirle al administrador de Microsoft 365:");
                _logger.LogError("   1. Conditional Access / Security Defaults: ¿hay una política que bloquee la");
                _logger.LogError("      autenticación heredada (legacy auth) por ubicación o por IP de origen?");
                _logger.LogError("   2. SMTP AUTH habilitado para el buzón Y a nivel de organización:");
                _logger.LogError("      Get-CASMailbox '{User}' | Select SmtpClientAuthenticationDisabled", _smtpUsername);
                _logger.LogError("      Get-TransportConfig | Select SmtpClientAuthenticationDisabled");
                _logger.LogError("   3. Si la política no se puede levantar, el camino sancionado es OAuth:");
                _logger.LogError("      Email:Provider=graph + Email:Graph:* (ya implementado).");
                _logger.LogError("      Ver: backend/documentacion/MIGRACION_CORREO_GRAPH_API.md");
                _logger.LogError("   Configuración actual: Host={Host}, Port={Port}, SSL={Ssl}, User={User}",
                    _smtpHost, _smtpPort, _smtpEnableSsl, _smtpUsername);
            }

            return EnvioCorreoResultado.Error(ClasificarPorDetalle(smtpDetails), smtpDetails);
        }
        catch (Exception ex)
        {
            var stackTraceLength = ex.StackTrace?.Length ?? 0;
            var stackTracePreview = stackTraceLength > 0
                ? ex.StackTrace?.Substring(0, Math.Min(500, stackTraceLength)) ?? ""
                : "";
            var errorDetails = $"Type: {ex.GetType().Name}, Message: {ex.Message}, StackTrace: {stackTracePreview}";

            _logger.LogError(ex, "Error inesperado al enviar correo a {ToEmail}: {Message} | Details: {Details}",
                toEmail, ex.Message, errorDetails);

            return EnvioCorreoResultado.Error(ClasificarPorDetalle(errorDetails), errorDetails);
        }
    }

    /// <summary>
    /// Clasificación por palabras clave del texto de diagnóstico. Se conserva tal cual estaba en el
    /// procesador de la cola para que los <c>error_type</c> históricos de la tabla no cambien.
    /// </summary>
    private static string ClasificarPorDetalle(string? errorDetails)
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

    private string BuildSmtpExceptionDetails(SmtpException ex, string toEmail)
    {
        var details = new StringBuilder();
        details.AppendLine($"SMTP Error Details:");
        details.AppendLine($"  Status Code: {ex.StatusCode}");
        details.AppendLine($"  Message: {ex.Message}");
        details.AppendLine($"  To Email: {toEmail}");
        details.AppendLine($"  SMTP Host: {_smtpHost}");
        details.AppendLine($"  SMTP Port: {_smtpPort}");
        details.AppendLine($"  SSL Enabled: {_smtpEnableSsl}");
        details.AppendLine($"  From Email: {_fromEmail}");

        if (ex.InnerException != null)
        {
            details.AppendLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
            details.AppendLine($"  Inner Message: {ex.InnerException.Message}");
        }

        // Agregar información específica según el código de estado y mensaje
        var statusCode = (int)ex.StatusCode;
        var statusCodeName = ex.StatusCode.ToString();

        if (statusCodeName.Contains("MustIssueStartTlsFirst") || ex.Message.Contains("MustIssueStartTlsFirst"))
        {
            details.AppendLine($"  Diagnosis: Office 365 requiere STARTTLS antes de autenticarse.");
            details.AppendLine($"  Solución: Verificar que EnableSsl=true en configuración para puerto 587.");
            details.AppendLine($"  Configuración actual: EnableSsl={_smtpEnableSsl}, Port={_smtpPort}");
            if (!_smtpEnableSsl)
            {
                details.AppendLine($"  ⚠️ ACCIÓN REQUERIDA: Cambiar Email:Smtp:EnableSsl a 'true' en appsettings.json");
            }
        }
        else if (ex.Message.Contains("5.7.30") || ex.Message.Contains("Basic authentication is not supported"))
        {
            details.AppendLine($"  Diagnosis: Exchange Online RETIRÓ la autenticación básica para SMTP Client Submission.");
            details.AppendLine($"  Este error NO se resuelve cambiando la contraseña ni habilitando SMTP AUTH.");
            details.AppendLine($"  Solución: migrar a Microsoft Graph (Email:Provider=graph + Email:Graph:*).");
            details.AppendLine($"  Ver: backend/documentacion/MIGRACION_CORREO_GRAPH_API.md");
        }
        else if (ex.Message.Contains("535") || ex.Message.Contains("5.7.139") ||
                 ex.Message.Contains("5.7.57") ||
                 ex.Message.Contains("Authentication unsuccessful") ||
                 ex.Message.Contains("Client not authenticated"))
        {
            details.AppendLine($"  Diagnosis: Office 365 rechazó la autenticación (535 5.7.139 / 5.7.57).");
            details.AppendLine($"  ⚠️ NO es la contraseña. Verificado el 05-ago-2026 contra este mismo tenant:");
            details.AppendLine($"     las credenciales de {_smtpUsername} autentican correctamente (235) y este");
            details.AppendLine($"     mismo código envía sin problema desde otra red. El rechazo depende del ORIGEN.");
            details.AppendLine($"  Causa esperada: una política del tenant bloquea la autenticación desde donde");
            details.AppendLine($"     corre el servidor. El propio mensaje lo dice: 'did not meet the criteria to be");
            details.AppendLine($"     authenticated successfully. Contact your administrator'.");
            details.AppendLine($"  Qué pedirle al administrador de Microsoft 365:");
            details.AppendLine($"    1. Conditional Access / Security Defaults: ¿bloquea la autenticación heredada");
            details.AppendLine($"       (legacy auth) por ubicación o IP de origen? Excluir el origen del servidor.");
            details.AppendLine($"    2. SMTP AUTH por buzón y por organización:");
            details.AppendLine($"       Get-CASMailbox '{_smtpUsername}' | Select SmtpClientAuthenticationDisabled");
            details.AppendLine($"       Get-TransportConfig | Select SmtpClientAuthenticationDisabled");
            details.AppendLine($"    3. Si la política no se puede levantar, usar OAuth (ya implementado):");
            details.AppendLine($"       Email:Provider=graph + Email:Graph:* — ver");
            details.AppendLine($"       backend/documentacion/MIGRACION_CORREO_GRAPH_API.md");
        }
        else
        {
            switch (statusCode)
            {
                case 421:
                case 454:
                    details.AppendLine($"  Diagnosis: Servicio SMTP no disponible. Verificar conectividad de red.");
                    break;
                case 550:
                    details.AppendLine($"  Diagnosis: Buzón de correo no disponible. Verificar dirección de email.");
                    break;
                default:
                    details.AppendLine($"  Diagnosis: Error SMTP con código {statusCode} ({statusCodeName})");
                    break;
            }
        }

        return details.ToString();
    }
}
