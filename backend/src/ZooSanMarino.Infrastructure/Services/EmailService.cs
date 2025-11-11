// src/ZooSanMarino.Infrastructure/Services/EmailService.cs
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para envío de correos electrónicos usando cola asíncrona
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IEmailQueueService _emailQueue;
    private readonly string _applicationUrl;

    public EmailService(
        IConfiguration configuration, 
        ILogger<EmailService> logger,
        IEmailQueueService emailQueue)
    {
        _configuration = configuration;
        _logger = logger;
        _emailQueue = emailQueue;
        _applicationUrl = _configuration["Email:ApplicationUrl"] ?? "https://zootecnico.sanmarino.com.co";
    }

    /// <summary>
    /// Envía un correo de recuperación de contraseña (agrega a la cola)
    /// </summary>
    public async Task<int?> SendPasswordRecoveryEmailAsync(string toEmail, string newPassword, string? userName = null)
    {
        try
        {
            var subject = "Recuperación de Contraseña - ZooSanMarino";
            var body = GeneratePasswordRecoveryEmailBody(newPassword, userName);

            // Crear metadata para el correo
            var metadata = JsonSerializer.Serialize(new
            {
                userName = userName,
                emailType = "password_recovery"
            });

            // Agregar a la cola (no bloquea)
            var emailQueueId = await _emailQueue.EnqueueEmailAsync(toEmail, subject, body, "password_recovery", metadata);
            
            _logger.LogInformation("Correo de recuperación agregado a la cola: ID={EmailQueueId}, To={Email}", emailQueueId, toEmail);
            return emailQueueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar correo de recuperación a la cola: To={Email}", toEmail);
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
            var subject = "Bienvenido a ZooSanMarino - Tus Credenciales de Acceso";
            var body = GenerateWelcomeEmailBody(userName, toEmail, password, applicationUrl);

            // Crear metadata para el correo
            var metadata = JsonSerializer.Serialize(new
            {
                userName = userName,
                applicationUrl = applicationUrl,
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

    /// <summary>
    /// Genera el cuerpo HTML del correo de recuperación de contraseña
    /// </summary>
    private string GeneratePasswordRecoveryEmailBody(string newPassword, string? userName)
    {
        var displayName = string.IsNullOrWhiteSpace(userName) ? "Usuario" : userName;
        
        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Recuperación de Contraseña</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 10px;
            padding: 30px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 3px solid #f4b428;
        }}
        .logo {{
            font-size: 28px;
            font-weight: bold;
            color: #2b2b2b;
            margin-bottom: 10px;
        }}
        .subtitle {{
            color: #6b7280;
            font-size: 14px;
        }}
        .content {{
            margin: 30px 0;
        }}
        .greeting {{
            font-size: 18px;
            color: #2b2b2b;
            margin-bottom: 20px;
        }}
        .message {{
            font-size: 16px;
            color: #4b5563;
            margin-bottom: 25px;
            line-height: 1.8;
        }}
        .credentials-box {{
            background-color: #f9fafb;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            padding: 20px;
            margin: 25px 0;
        }}
        .credential-item {{
            margin: 15px 0;
            padding: 12px;
            background-color: #ffffff;
            border-radius: 6px;
            border-left: 4px solid #f4b428;
        }}
        .credential-label {{
            font-weight: 600;
            color: #374151;
            font-size: 14px;
            margin-bottom: 5px;
        }}
        .credential-value {{
            font-size: 16px;
            color: #1f2937;
            font-family: 'Courier New', monospace;
            word-break: break-all;
        }}
        .warning {{
            background-color: #fef3c7;
            border-left: 4px solid #f59e0b;
            padding: 15px;
            margin: 20px 0;
            border-radius: 6px;
        }}
        .warning-text {{
            color: #92400e;
            font-size: 14px;
            margin: 0;
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
        }}
        .button {{
            display: inline-block;
            background: linear-gradient(180deg, #f4b428, #e6a41c);
            color: #1a1a1a !important;
            padding: 14px 30px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 4px 6px rgba(244, 180, 40, 0.3);
            transition: transform 0.2s;
        }}
        .button:hover {{
            transform: translateY(-2px);
        }}
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #e5e7eb;
            text-align: center;
            color: #6b7280;
            font-size: 12px;
        }}
        .footer-text {{
            margin: 5px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>🐔 ZooSanMarino</div>
            <div class='subtitle'>Sistema Zootécnico</div>
        </div>
        
        <div class='content'>
            <div class='greeting'>Hola {displayName},</div>
            
            <div class='message'>
                Hemos recibido una solicitud para recuperar tu contraseña. Se ha generado una nueva contraseña temporal para tu cuenta.
            </div>
            
            <div class='credentials-box'>
                <div class='credential-item'>
                    <div class='credential-label'>Tu nueva contraseña es:</div>
                    <div class='credential-value'>{newPassword}</div>
                </div>
            </div>
            
            <div class='warning'>
                <p class='warning-text'>
                    <strong>⚠️ Importante:</strong> Por seguridad, te recomendamos cambiar esta contraseña temporal después de iniciar sesión.
                </p>
            </div>
            
            <div class='button-container'>
                <a href='{_applicationUrl}/login' class='button'>Iniciar Sesión</a>
            </div>
            
            <div class='message'>
                Si no solicitaste este cambio, por favor contacta inmediatamente al administrador del sistema.
            </div>
        </div>
        
        <div class='footer'>
            <p class='footer-text'>© {DateTime.Now.Year} Sanmarino Genética Avícola</p>
            <p class='footer-text'>Todos los derechos reservados</p>
            <p class='footer-text'>Este es un correo automático, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Genera el cuerpo HTML del correo de bienvenida
    /// </summary>
    private string GenerateWelcomeEmailBody(string userName, string email, string password, string applicationUrl)
    {
        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Bienvenido a ZooSanMarino</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 10px;
            padding: 30px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 3px solid #f4b428;
        }}
        .logo {{
            font-size: 32px;
            font-weight: bold;
            color: #2b2b2b;
            margin-bottom: 10px;
        }}
        .subtitle {{
            color: #6b7280;
            font-size: 14px;
        }}
        .content {{
            margin: 30px 0;
        }}
        .greeting {{
            font-size: 20px;
            color: #2b2b2b;
            margin-bottom: 20px;
            font-weight: 600;
        }}
        .message {{
            font-size: 16px;
            color: #4b5563;
            margin-bottom: 25px;
            line-height: 1.8;
        }}
        .credentials-box {{
            background-color: #f9fafb;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            padding: 20px;
            margin: 25px 0;
        }}
        .credential-item {{
            margin: 15px 0;
            padding: 12px;
            background-color: #ffffff;
            border-radius: 6px;
            border-left: 4px solid #f4b428;
        }}
        .credential-label {{
            font-weight: 600;
            color: #374151;
            font-size: 14px;
            margin-bottom: 5px;
        }}
        .credential-value {{
            font-size: 16px;
            color: #1f2937;
            font-family: 'Courier New', monospace;
            word-break: break-all;
        }}
        .info-box {{
            background-color: #eff6ff;
            border-left: 4px solid #3b82f6;
            padding: 15px;
            margin: 20px 0;
            border-radius: 6px;
        }}
        .info-text {{
            color: #1e40af;
            font-size: 14px;
            margin: 0;
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
        }}
        .button {{
            display: inline-block;
            background: linear-gradient(180deg, #f4b428, #e6a41c);
            color: #1a1a1a !important;
            padding: 14px 30px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 4px 6px rgba(244, 180, 40, 0.3);
            transition: transform 0.2s;
        }}
        .button:hover {{
            transform: translateY(-2px);
        }}
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #e5e7eb;
            text-align: center;
            color: #6b7280;
            font-size: 12px;
        }}
        .footer-text {{
            margin: 5px 0;
        }}
        .welcome-icon {{
            font-size: 48px;
            text-align: center;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>🐔 ZooSanMarino</div>
            <div class='subtitle'>Sistema Zootécnico</div>
        </div>
        
        <div class='content'>
            <div class='welcome-icon'>🎉</div>
            
            <div class='greeting'>¡Bienvenido/a, {userName}!</div>
            
            <div class='message'>
                Tu cuenta ha sido creada exitosamente en el sistema ZooSanMarino. A continuación encontrarás tus credenciales de acceso:
            </div>
            
            <div class='credentials-box'>
                <div class='credential-item'>
                    <div class='credential-label'>Correo Electrónico:</div>
                    <div class='credential-value'>{email}</div>
                </div>
                <div class='credential-item'>
                    <div class='credential-label'>Contraseña Temporal:</div>
                    <div class='credential-value'>{password}</div>
                </div>
            </div>
            
            <div class='info-box'>
                <p class='info-text'>
                    <strong>ℹ️ Nota:</strong> Por seguridad, te recomendamos cambiar esta contraseña temporal después de tu primer inicio de sesión.
                </p>
            </div>
            
            <div class='button-container'>
                <a href='{applicationUrl}/login' class='button'>Acceder a la Aplicación</a>
            </div>
            
            <div class='message'>
                <strong>¿Necesitas ayuda?</strong><br>
                Si tienes alguna pregunta o necesitas asistencia, no dudes en contactar al administrador del sistema.
            </div>
        </div>
        
        <div class='footer'>
            <p class='footer-text'>© {DateTime.Now.Year} Sanmarino Genética Avícola</p>
            <p class='footer-text'>Todos los derechos reservados</p>
            <p class='footer-text'>Este es un correo automático, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
    }
}
