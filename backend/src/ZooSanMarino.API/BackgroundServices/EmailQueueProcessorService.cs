// src/ZooSanMarino.API/BackgroundServices/EmailQueueProcessorService.cs
using System.Collections.Generic;
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
                    
                    // Obtener detalles del último error de SendEmailAsync
                    var lastErrorDetails = await GetLastEmailErrorDetailsAsync(emailQueue.ToEmail, emailQueue.Subject);
                    
                    if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                    {
                        // Marcar como fallido después de agotar reintentos
                        emailQueue.Status = "failed";
                        emailQueue.FailedAt = DateTime.UtcNow;
                        emailQueue.ErrorType = "max_retries_exceeded";
                        
                        // Construir mensaje de error detallado
                        var detailedError = BuildDetailedErrorMessage(
                            emailQueue.RetryCount,
                            emailQueue.MaxRetries,
                            emailQueue.CreatedAt,
                            lastErrorDetails
                        );
                        emailQueue.ErrorMessage = detailedError;
                        
                        // Actualizar metadata con información detallada del error
                        var metadata = UpdateMetadataWithErrorDetails(emailQueue.Metadata, lastErrorDetails, emailQueue.RetryCount);
                        emailQueue.Metadata = metadata;
                        
                        _logger.LogError(
                            "❌ Correo falló después de {RetryCount}/{MaxRetries} intentos: ID={EmailQueueId}, To={ToEmail}, Type={EmailType}, Error={ErrorDetails}",
                            emailQueue.RetryCount, emailQueue.MaxRetries, emailQueue.Id, emailQueue.ToEmail, emailQueue.EmailType, detailedError);
                    }
                    else
                    {
                        // Volver a estado pending para reintento
                        emailQueue.Status = "pending";
                        emailQueue.ProcessedAt = null;
                        
                        // Guardar información del error actual para referencia
                        if (!string.IsNullOrEmpty(lastErrorDetails))
                        {
                            emailQueue.ErrorMessage = $"Intento {emailQueue.RetryCount} fallido: {lastErrorDetails}";
                            emailQueue.ErrorType = GetErrorTypeFromDetails(lastErrorDetails);
                        }
                        
                        _logger.LogWarning(
                            "⚠️ Correo falló, reintentando ({RetryCount}/{MaxRetries}): ID={EmailQueueId}, To={ToEmail}, Error={ErrorDetails}",
                            emailQueue.RetryCount, emailQueue.MaxRetries, emailQueue.Id, emailQueue.ToEmail, lastErrorDetails ?? "Unknown");
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Construir mensaje de error detallado
                var detailedError = BuildDetailedExceptionMessage(ex, emailQueue.RetryCount + 1);
                
                _logger.LogError(ex, 
                    "Error al procesar correo: ID={EmailQueueId}, To={ToEmail}, Type={EmailType}, Retry={RetryCount}, Error={ErrorDetails}",
                    emailQueue.Id, emailQueue.ToEmail, emailQueue.EmailType, emailQueue.RetryCount + 1, detailedError);

                // Registrar error
                emailQueue.RetryCount++;
                emailQueue.ErrorMessage = detailedError;
                emailQueue.ErrorType = GetErrorType(ex);

                if (emailQueue.RetryCount >= emailQueue.MaxRetries)
                {
                    emailQueue.Status = "failed";
                    emailQueue.FailedAt = DateTime.UtcNow;
                    emailQueue.ErrorType = emailQueue.ErrorType == "max_retries_exceeded" 
                        ? emailQueue.ErrorType 
                        : $"max_retries_exceeded_{emailQueue.ErrorType}";
                    
                    // Actualizar metadata con información detallada del error
                    var metadata = UpdateMetadataWithExceptionDetails(emailQueue.Metadata, ex, emailQueue.RetryCount);
                    emailQueue.Metadata = metadata;
                    
                    _logger.LogError(
                        "❌ Correo falló definitivamente después de {RetryCount}/{MaxRetries} intentos: ID={EmailQueueId}, To={ToEmail}, Type={EmailType}, Error={ErrorDetails}",
                        emailQueue.RetryCount, emailQueue.MaxRetries, emailQueue.Id, emailQueue.ToEmail, emailQueue.EmailType, detailedError);
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

    private string? _lastEmailErrorDetails = null;

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        _lastEmailErrorDetails = null; // Reset error details
        
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
            // Capturar información detallada del error SMTP
            var smtpDetails = BuildSmtpExceptionDetails(ex, toEmail);
            _lastEmailErrorDetails = smtpDetails;
            
            _logger.LogError(ex, "Error SMTP al enviar correo a {ToEmail}: {Message} | Details: {Details}", 
                toEmail, ex.Message, smtpDetails);
            
            // Log detallado para diagnóstico
            if (ex.Message.Contains("535") || ex.Message.Contains("Authentication") || (int)ex.StatusCode == 535)
            {
                _logger.LogWarning("⚠️ Error de autenticación SMTP. Verifica:");
                _logger.LogWarning("   1. Que SMTP AUTH esté habilitado en Office 365");
                _logger.LogWarning("   2. Que uses una 'App Password' en lugar de la contraseña normal");
                _logger.LogWarning("   3. Que la cuenta tenga permisos para enviar correos");
                _logger.LogWarning("   4. Host: {Host}, Port: {Port}, SSL: {Ssl}", _smtpHost, _smtpPort, _smtpEnableSsl);
                _logger.LogWarning("   URL: https://aka.ms/smtp_auth_disabled");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            var stackTraceLength = ex.StackTrace?.Length ?? 0;
            var stackTracePreview = stackTraceLength > 0 
                ? ex.StackTrace?.Substring(0, Math.Min(500, stackTraceLength)) ?? ""
                : "";
            var errorDetails = $"Type: {ex.GetType().Name}, Message: {ex.Message}, StackTrace: {stackTracePreview}";
            _lastEmailErrorDetails = errorDetails;
            
            _logger.LogError(ex, "Error inesperado al enviar correo a {ToEmail}: {Message} | Details: {Details}", 
                toEmail, ex.Message, errorDetails);
            return false;
        }
    }

    private Task<string?> GetLastEmailErrorDetailsAsync(string toEmail, string subject)
    {
        return Task.FromResult<string?>(_lastEmailErrorDetails);
    }

    private string GetErrorType(Exception ex)
    {
        if (ex is SmtpException smtpEx)
        {
            var statusCode = (int)smtpEx.StatusCode;
            if (statusCode == 535 || 
                smtpEx.Message.Contains("Authentication") || 
                smtpEx.Message.Contains("535") ||
                smtpEx.Message.Contains("535-5.7.3"))
                return "smtp_auth";
            if (statusCode == 421 || statusCode == 454 ||
                smtpEx.Message.Contains("network") || 
                smtpEx.Message.Contains("timeout") ||
                smtpEx.Message.Contains("connection"))
                return "network";
            if (statusCode == 550 ||
                smtpEx.Message.Contains("550") ||
                smtpEx.Message.Contains("mailbox"))
                return "invalid_email";
            return $"smtp_error_{statusCode}";
        }

        if (ex.Message.Contains("invalid") || ex.Message.Contains("format") || ex.Message.Contains("address"))
            return "invalid_email";

        if (ex is TimeoutException || ex.Message.Contains("timeout"))
            return "timeout";

        if (ex is System.Net.Sockets.SocketException)
            return "network_socket";

        return $"unknown_{ex.GetType().Name}";
    }

    private string GetErrorTypeFromDetails(string? errorDetails)
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
        
        // Agregar información específica según el código de estado
        var statusCode = (int)ex.StatusCode;
        switch (statusCode)
        {
            case 535:
                details.AppendLine($"  Diagnosis: Error de autenticación. Verificar credenciales SMTP y App Password.");
                break;
            case 421:
            case 454:
                details.AppendLine($"  Diagnosis: Servicio SMTP no disponible. Verificar conectividad de red.");
                break;
            case 550:
                details.AppendLine($"  Diagnosis: Buzón de correo no disponible. Verificar dirección de email.");
                break;
        }
        
        return details.ToString();
    }

    private string BuildDetailedErrorMessage(int retryCount, int maxRetries, DateTime createdAt, string? lastErrorDetails)
    {
        var details = new StringBuilder();
        details.AppendLine($"Email failed after {retryCount}/{maxRetries} retry attempts.");
        details.AppendLine($"Created at: {createdAt:yyyy-MM-dd HH:mm:ss} UTC");
        details.AppendLine($"Failed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        details.AppendLine($"Total time elapsed: {(DateTime.UtcNow - createdAt).TotalMinutes:F2} minutes");
        details.AppendLine();
        details.AppendLine("Last error details:");
        details.AppendLine(lastErrorDetails ?? "No error details available");
        details.AppendLine();
        details.AppendLine("Possible causes:");
        details.AppendLine("  1. SMTP server authentication failure (check credentials and App Password)");
        details.AppendLine("  2. Network connectivity issues");
        details.AppendLine("  3. Invalid email address format");
        details.AppendLine("  4. SMTP server temporarily unavailable");
        details.AppendLine("  5. Firewall or security restrictions");
        
        return details.ToString();
    }

    private string BuildDetailedExceptionMessage(Exception ex, int attemptNumber)
    {
        var details = new StringBuilder();
        details.AppendLine($"Exception occurred on attempt #{attemptNumber}:");
        details.AppendLine($"  Exception Type: {ex.GetType().FullName}");
        details.AppendLine($"  Message: {ex.Message}");
        details.AppendLine($"  Source: {ex.Source ?? "Unknown"}");
        
        if (ex is SmtpException smtpEx)
        {
            details.AppendLine($"  SMTP Status Code: {smtpEx.StatusCode}");
        }
        
        if (ex.InnerException != null)
        {
            details.AppendLine($"  Inner Exception: {ex.InnerException.GetType().FullName}");
            details.AppendLine($"  Inner Message: {ex.InnerException.Message}");
        }
        
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            var stackTracePreview = ex.StackTrace.Length > 1000 
                ? ex.StackTrace.Substring(0, 1000) + "... (truncated)" 
                : ex.StackTrace;
            details.AppendLine($"  Stack Trace: {stackTracePreview}");
        }
        
        return details.ToString();
    }

    private string? UpdateMetadataWithErrorDetails(string? existingMetadata, string? errorDetails, int retryCount)
    {
        try
        {
            var metadata = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(existingMetadata))
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(existingMetadata);
                if (existing != null)
                {
                    foreach (var kvp in existing)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            metadata["error_history"] = metadata.ContainsKey("error_history") 
                ? $"{metadata["error_history"]}\nAttempt {retryCount}: {errorDetails}"
                : $"Attempt {retryCount}: {errorDetails}";
            
            metadata["last_error"] = errorDetails;
            metadata["last_error_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
            metadata["total_retries"] = retryCount;
            
            return JsonSerializer.Serialize(metadata);
        }
        catch
        {
            // Si falla la serialización, devolver metadata básico
            return $"{{\"last_error\":\"{errorDetails?.Replace("\"", "\\\"")}\",\"retry_count\":{retryCount}}}";
        }
    }

    private string? UpdateMetadataWithExceptionDetails(string? existingMetadata, Exception ex, int retryCount)
    {
        var errorDetails = BuildDetailedExceptionMessage(ex, retryCount);
        return UpdateMetadataWithErrorDetails(existingMetadata, errorDetails, retryCount);
    }
}

