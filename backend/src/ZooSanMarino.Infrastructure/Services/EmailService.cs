// src/ZooSanMarino.Infrastructure/Services/EmailService.cs
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Leer configuración desde appsettings.json o variables de entorno
        _smtpHost = _configuration["EmailSettings:SmtpHost"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
        _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
        _smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "";
        _smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "";
        _fromEmail = _configuration["EmailSettings:FromEmail"] ?? Environment.GetEnvironmentVariable("FROM_EMAIL") ?? "";
        _fromName = _configuration["EmailSettings:FromName"] ?? Environment.GetEnvironmentVariable("FROM_NAME") ?? "Zoo San Marino";
    }

    public async Task SendPasswordRecoveryEmailAsync(string email, string newPassword)
    {
        var subject = "Recuperación de Contraseña - Zoo San Marino";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Recuperación de Contraseña</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f8f9fa; padding: 30px; border-radius: 0 0 8px 8px; }}
        .password-box {{ background: #e8f4fd; border: 2px solid #3498db; padding: 15px; border-radius: 5px; text-align: center; margin: 20px 0; }}
        .password {{ font-size: 24px; font-weight: bold; color: #2c3e50; letter-spacing: 2px; }}
        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Recuperación de Contraseña</h1>
            <p>Zoo San Marino - Sistema de Gestión Avícola</p>
        </div>
        
        <div class='content'>
            <h2>Hola,</h2>
            <p>Hemos recibido una solicitud para recuperar tu contraseña en el sistema Zoo San Marino.</p>
            
            <div class='password-box'>
                <p><strong>Tu nueva contraseña temporal es:</strong></p>
                <div class='password'>{newPassword}</div>
            </div>
            
            <div class='warning'>
                <p><strong>⚠️ Importante:</strong></p>
                <ul>
                    <li>Esta es una contraseña temporal generada automáticamente</li>
                    <li>Te recomendamos cambiar esta contraseña después de iniciar sesión</li>
                    <li>No compartas esta información con nadie</li>
                    <li>Si no solicitaste este cambio, contacta al administrador del sistema</li>
                </ul>
            </div>
            
            <p>Puedes iniciar sesión con esta contraseña temporal y luego cambiarla por una de tu elección.</p>
            
            <p>Si tienes alguna pregunta, no dudes en contactar al equipo de soporte.</p>
        </div>
        
        <div class='footer'>
            <p>© {DateTime.Now.Year} Zoo San Marino - Todos los derechos reservados</p>
            <p>Este es un mensaje automático, por favor no respondas a este correo.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, body, true);
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

            using var message = new MailMessage();
            message.From = new MailAddress(_fromEmail, _fromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = isHtml;

            await client.SendMailAsync(message);
            
            _logger.LogInformation("Email enviado exitosamente a {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar email a {Email}", to);
            throw new InvalidOperationException($"Error al enviar email: {ex.Message}", ex);
        }
    }
}



