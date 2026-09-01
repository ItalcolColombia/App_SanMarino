// src/ZooSanMarino.Infrastructure/Services/Email/SmtpEmailSender.cs
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Transporte SMTP (usuario + contraseña). Es el código que vivía dentro de
/// <c>EmailQueueProcessorService</c>, trasladado sin cambios de comportamiento: mismos parámetros
/// de conexión, mismos <c>error_type</c> y el mismo timeout de 60 s.
///
/// Verificado el 05-ago-2026: con las credenciales de producción este código **envía
/// correctamente** contra <c>smtp.office365.com:587</c> con STARTTLS. Si en algún entorno responde
/// <c>5.7.139</c> / <c>5.7.57</c>, el rechazo es de una política del tenant según el origen de la
/// conexión, no del código ni de la contraseña — el diagnóstico que se guarda lo explica.
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

            // Un solo log, con la clase que decide EmailErrorCalculos. Antes había acá la MISMA
            // cadena de if/else que en BuildSmtpExceptionDetails -con el mismo orden equivocado-, así
            // que el log y lo guardado en la cola podían contradecirse entre sí.
            var claseLog = EmailErrorCalculos.Clasificar(ex.Message, ex.StatusCode.ToString());
            _logger.LogError(
                "🔴 Correo rechazado [{Clase}] (reintentable: {Reintentable}). {Diagnostico} | Config: Host={Host}, Port={Port}, SSL={Ssl}, User={User}",
                EmailErrorCalculos.TipoParaLaCola(claseLog),
                EmailErrorCalculos.ValeLaPenaReintentar(claseLog),
                EmailErrorCalculos.Diagnostico(claseLog, _smtpUsername),
                _smtpHost, _smtpPort, _smtpEnableSsl, _smtpUsername);

            return EnvioCorreoResultado.Error(EnvioCorreoCalculos.ClasificarErrorSmtp(smtpDetails), smtpDetails);
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

            return EnvioCorreoResultado.Error(EnvioCorreoCalculos.ClasificarErrorSmtp(errorDetails), errorDetails);
        }
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

        // 🔴 El diagnóstico sale de EmailErrorCalculos, que clasifica por el MENSAJE del servidor y
        // NO por ex.StatusCode.
        //
        // Acá había una cadena de if/else que evaluaba `MustIssueStartTlsFirst` PRIMERO, y .NET mapea
        // a ese StatusCode el 530 que Office 365 devuelve en el MAIL FROM posterior a un AUTH fallido:
        // todos los fallos de autenticación caían en esa rama y la del 535 -la correcta- era
        // inalcanzable. Lo que quedaba guardado decía «verificá que EnableSsl sea true» y en la línea
        // siguiente informaba que ya era true, mientras escondía que la cuenta estaba bloqueada. El
        // orden de evaluación es parte del contrato y hoy lo fijan los tests del cálculo.
        var clase = EmailErrorCalculos.Clasificar(ex.Message, ex.StatusCode.ToString());

        details.AppendLine($"  Clase: {EmailErrorCalculos.TipoParaLaCola(clase)}");
        details.AppendLine($"  Reintentable: {(EmailErrorCalculos.ValeLaPenaReintentar(clase) ? "sí" : "no")}");
        details.AppendLine($"  Diagnóstico: {EmailErrorCalculos.Diagnostico(clase, _smtpUsername)}");

        return details.ToString();
    }
}
