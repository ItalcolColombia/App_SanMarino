// src/ZooSanMarino.Infrastructure/Services/EmailService.cs
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Correos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Correos de cuenta (restablecimiento, alta de usuario). No habla con el servidor SMTP: encola en
/// <c>email_queue</c> y el envío lo hace <c>EmailQueueProcessorService</c>.
///
/// Los cuerpos viven en <see cref="CorreosCuenta"/> (capa Application) porque son funciones puras y
/// así quedan cubiertos por los tests del gate de CI. Acá solo se resuelve la configuración de marca
/// y se arma el asunto.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly string _applicationUrl;
    private readonly string _brandDisplayName;
    private readonly string _brandTagline;
    private readonly string _logoUrl;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IEmailQueueService emailQueue)
    {
        _configuration = configuration;
        _logger = logger;
        _emailQueue = emailQueue;
        _applicationUrl = _configuration["Email:ApplicationUrl"] ?? "http://localhost:4200";
        _brandDisplayName = _configuration["Email:BrandName"] ?? "ItalGranja";
        _brandTagline = _configuration["Email:Tagline"] ?? "Gestión de granjas avícolas · Italcol";
        _logoUrl = _configuration["Email:LogoUrl"] ?? string.Empty;
    }

    private string BrandLine => $"{_brandDisplayName} · {_brandTagline}";

    /// <summary>
    /// Encola el correo de "olvidé mi contraseña": lleva un enlace de un solo uso, nunca el token
    /// presentado como credencial.
    /// </summary>
    public async Task<int?> SendPasswordResetLinkEmailAsync(string toEmail, string resetToken, string? userName = null)
    {
        try
        {
            var subject = $"Restablecé tu contraseña · {_brandDisplayName}";
            var body = CorreosCuenta.RestablecerContrasena(
                _brandDisplayName, _brandTagline, _logoUrl, _applicationUrl, resetToken, userName);

            // El token NO va en la metadata: queda guardado en claro en email_queue y es un secreto vivo.
            var metadata = JsonSerializer.Serialize(new
            {
                userName,
                emailType = "password_reset_link",
                expiraEnMinutos = CorreosCuenta.MinutosVigencia
            });

            var emailQueueId = await _emailQueue.EnqueueEmailAsync(toEmail, subject, body, "password_recovery", metadata);

            _logger.LogInformation("Enlace de restablecimiento agregado a la cola: ID={EmailQueueId}, To={Email}", emailQueueId, toEmail);
            return emailQueueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar el enlace de restablecimiento a la cola: To={Email}", toEmail);
            return null;
        }
    }

    /// <summary>
    /// Encola el aviso de contraseña restablecida por un administrador (la contraseña ya quedó
    /// asignada en la cuenta, así que el usuario necesita conocerla).
    /// </summary>
    public async Task<int?> SendPasswordRecoveryEmailAsync(string toEmail, string newPassword, string? userName = null)
    {
        try
        {
            var subject = $"Tu contraseña fue restablecida · {_brandDisplayName}";
            var body = CorreosCuenta.ContrasenaRestablecidaPorAdmin(
                _brandDisplayName, _brandTagline, _logoUrl, _applicationUrl, newPassword, userName);

            var metadata = JsonSerializer.Serialize(new
            {
                userName,
                emailType = "password_reset_admin"
            });

            var emailQueueId = await _emailQueue.EnqueueEmailAsync(toEmail, subject, body, "password_recovery", metadata);

            _logger.LogInformation("Aviso de contraseña restablecida agregado a la cola: ID={EmailQueueId}, To={Email}", emailQueueId, toEmail);
            return emailQueueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar el aviso de contraseña restablecida a la cola: To={Email}", toEmail);
            return null;
        }
    }

    /// <summary>
    /// Envía un correo de bienvenida con credenciales (agrega a la cola)
    /// </summary>
    public async Task<int?> SendWelcomeEmailAsync(string toEmail, string password, string userName, string applicationUrl)
    {
        try
        {
            var subject = $"Bienvenido a {_brandDisplayName} · Tus credenciales de acceso";
            var url = string.IsNullOrWhiteSpace(applicationUrl) ? _applicationUrl : applicationUrl;
            var body = CorreosCuenta.Bienvenida(
                _brandDisplayName, _brandTagline, _logoUrl, url, toEmail, password, userName);

            // Crear metadata para el correo
            var metadata = JsonSerializer.Serialize(new
            {
                userName = userName,
                applicationUrl = url,
                emailType = "welcome"
            });

            // Agregar a la cola (no bloquea)
            var emailQueueId = await _emailQueue.EnqueueEmailAsync(toEmail, subject, body, "welcome", metadata);

            _logger.LogInformation("Correo de bienvenida agregado a la cola: ID={EmailQueueId}, To={Email}", emailQueueId, toEmail);
            return emailQueueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar correo de bienvenida a la cola: To={Email}", toEmail);
            return null;
        }
    }
}
