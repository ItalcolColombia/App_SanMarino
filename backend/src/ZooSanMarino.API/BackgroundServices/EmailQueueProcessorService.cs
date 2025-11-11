// src/ZooSanMarino.API/BackgroundServices/EmailQueueProcessorService.cs
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API.BackgroundServices;

/// <summary>
/// Servicio en segundo plano para procesar la cola de correos electrónicos
/// </summary>
public class EmailQueueProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailQueueProcessorService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30); // Procesar cada 30 segundos

    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly bool _smtpEnableSsl;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailQueueProcessorService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EmailQueueProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;

        // Obtener configuración SMTP
        _smtpHost = _configuration["Email:Smtp:Host"] ?? throw new InvalidOperationException("Email:Smtp:Host no configurado");
        var portStr = _configuration["Email:Smtp:Port"];
        _smtpPort = int.TryParse(portStr, out var port) ? port : 587;
        _smtpUsername = _configuration["Email:Smtp:Username"] ?? throw new InvalidOperationException("Email:Smtp:Username no configurado");
        _smtpPassword = _configuration["Email:Smtp:Password"] ?? throw new InvalidOperationException("Email:Smtp:Password no configurado");
        var sslStr = _configuration["Email:Smtp:EnableSsl"];
        _smtpEnableSsl = bool.TryParse(sslStr, out var ssl) ? ssl : true;
        _fromEmail = _configuration["Email:From:Address"] ?? _smtpUsername;
        _fromName = _configuration["Email:From:Name"] ?? "ZooSanMarino - Sistema Zootécnico";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 EmailQueueProcessorService iniciado. Procesando cola de correos cada {Interval} segundos", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el procesador de cola de correos");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("🛑 EmailQueueProcessorService detenido");
    }

    private async Task ProcessEmailQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ZooSanMarinoContext>();

        // Obtener correos pendientes (máximo 10 por ciclo para no sobrecargar)
        var pendingEmails = await context.EmailQueue
            .Where(e => e.Status == "pending" && e.RetryCount < e.MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (!pendingEmails.Any())
        {
            return; // No hay correos pendientes
        }

        _logger.LogInformation("📧 Procesando {Count} correos pendientes", pendingEmails.Count);

        foreach (var emailQueue in pendingEmails)
        {
            try
            {
                // Marcar como procesando
                emailQueue.Status = "processing";
                emailQueue.ProcessedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                // Intentar enviar el correo
                var success = await SendEmailAsync(emailQueue.ToEmail, emailQueue.Subject, emailQueue.Body);

                if (success)
                {
                    // Marcar como enviado
                    emailQueue.Status = "sent";
                    emailQueue.SentAt = DateTime.UtcNow;
                    emailQueue.ErrorMessage = null;
                    emailQueue.ErrorType = null;
                    _logger.LogInformation("✅ Correo enviado exitosamente: ID={EmailQueueId}, To={ToEmail}", 
                        emailQueue.Id, emailQueue.ToEmail);
                }
                else
                {
                    // Incrementar contador de reintentos
                    emailQueue.RetryCount++;
                    
                    if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                    {
                        // Marcar como fallido después de agotar reintentos
                        emailQueue.Status = "failed";
                        emailQueue.FailedAt = DateTime.UtcNow;
                        emailQueue.ErrorType = "max_retries_exceeded";
                        _logger.LogWarning("❌ Correo falló después de {RetryCount} intentos: ID={EmailQueueId}, To={ToEmail}", 
                            emailQueue.RetryCount, emailQueue.Id, emailQueue.ToEmail);
                    }
                    else
                    {
                        // Volver a estado pending para reintento
                        emailQueue.Status = "pending";
                        emailQueue.ProcessedAt = null;
                        _logger.LogWarning("⚠️ Correo falló, reintentando ({RetryCount}/{MaxRetries}): ID={EmailQueueId}, To={ToEmail}", 
                            emailQueue.RetryCount, emailQueue.MaxRetries, emailQueue.Id, emailQueue.ToEmail);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar correo: ID={EmailQueueId}, To={ToEmail}", 
                    emailQueue.Id, emailQueue.ToEmail);

                // Registrar error
                emailQueue.RetryCount++;
                emailQueue.ErrorMessage = ex.Message;
                emailQueue.ErrorType = GetErrorType(ex);

                if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                {
                    emailQueue.Status = "failed";
                    emailQueue.FailedAt = DateTime.UtcNow;
                }
                else
                {
                    emailQueue.Status = "pending";
                    emailQueue.ProcessedAt = null;
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            // Configuración mejorada para Office 365
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _smtpEnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 60000, // 60 segundos (aumentado para Office 365)
                UseDefaultCredentials = false // Importante: no usar credenciales por defecto
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Priority = MailPriority.Normal
            };

            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Error SMTP al enviar correo a {ToEmail}: {Message}", toEmail, ex.Message);
            
            // Log detallado para diagnóstico
            if (ex.Message.Contains("535") || ex.Message.Contains("Authentication"))
            {
                _logger.LogWarning("⚠️ Error de autenticación SMTP. Verifica:");
                _logger.LogWarning("   1. Que SMTP AUTH esté habilitado en Office 365");
                _logger.LogWarning("   2. Que uses una 'App Password' en lugar de la contraseña normal");
                _logger.LogWarning("   3. Que la cuenta tenga permisos para enviar correos");
                _logger.LogWarning("   URL: https://aka.ms/smtp_auth_disabled");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al enviar correo a {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    private string GetErrorType(Exception ex)
    {
        if (ex is SmtpException smtpEx)
        {
            if (smtpEx.Message.Contains("Authentication") || smtpEx.Message.Contains("535"))
                return "smtp_auth";
            if (smtpEx.Message.Contains("network") || smtpEx.Message.Contains("timeout"))
                return "network";
            return "smtp_error";
        }

        if (ex.Message.Contains("invalid") || ex.Message.Contains("format"))
            return "invalid_email";

        return "unknown";
    }
}

