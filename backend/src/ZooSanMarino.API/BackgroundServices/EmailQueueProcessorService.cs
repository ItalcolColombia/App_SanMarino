// src/ZooSanMarino.API/BackgroundServices/EmailQueueProcessorService.cs
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.API.BackgroundServices;

/// <summary>
/// Servicio en segundo plano para procesar la cola de correos electrónicos.
///
/// El transporte concreto (Microsoft Graph, SMTP) lo resuelve <see cref="IEmailSender"/> por
/// configuración: acá sólo vive la máquina de estados de la cola (reintentos, metadata, errores).
/// </summary>
public class EmailQueueProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailQueueProcessorService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30); // Procesar cada 30 segundos

    public EmailQueueProcessorService(
        IServiceProvider serviceProvider,
        IEmailSender emailSender,
        ILogger<EmailQueueProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _emailSender = emailSender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🚀 EmailQueueProcessorService iniciado (transporte: {Transporte}). Procesando cola de correos cada {Interval} segundos",
            _emailSender.Nombre, _pollingInterval.TotalSeconds);

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
                            emailQueue.ErrorType = _lastEmailErrorType ?? "unknown";
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
    private string? _lastEmailErrorType = null;

    /// <summary>
    /// Delega en el transporte configurado y guarda el detalle del fallo para la fila de la cola.
    /// No propaga el token de cancelación a propósito: la fila ya quedó en "processing", así que un
    /// apagado a mitad del envío la dejaría trabada fuera del universo de reintentos.
    /// </summary>
    private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        _lastEmailErrorDetails = null; // Reset error details
        _lastEmailErrorType = null;

        var resultado = await _emailSender.EnviarAsync(toEmail, subject, body);

        if (resultado.Exitoso)
            return true;

        _lastEmailErrorDetails = resultado.Detalle;
        _lastEmailErrorType = resultado.TipoError;
        return false;
    }

    private Task<string?> GetLastEmailErrorDetailsAsync(string toEmail, string subject)
    {
        return Task.FromResult<string?>(_lastEmailErrorDetails);
    }

    private string GetErrorType(Exception ex)
    {
        if (ex.Message.Contains("invalid") || ex.Message.Contains("format") || ex.Message.Contains("address"))
            return "invalid_email";

        if (ex is TimeoutException || ex.Message.Contains("timeout"))
            return "timeout";

        if (ex is System.Net.Sockets.SocketException)
            return "network_socket";

        return $"unknown_{ex.GetType().Name}";
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

        return details.ToString();
    }

    private string BuildDetailedExceptionMessage(Exception ex, int attemptNumber)
    {
        var details = new StringBuilder();
        details.AppendLine($"Exception occurred on attempt #{attemptNumber}:");
        details.AppendLine($"  Exception Type: {ex.GetType().FullName}");
        details.AppendLine($"  Message: {ex.Message}");
        details.AppendLine($"  Source: {ex.Source ?? "Unknown"}");

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
            
            metadata["last_error"] = errorDetails ?? string.Empty;
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

