// src/ZooSanMarino.Infrastructure/Services/EmailQueueService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para gestionar la cola de correos electrónicos
/// </summary>
public class EmailQueueService : IEmailQueueService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ILogger<EmailQueueService> _logger;

    public EmailQueueService(ZooSanMarinoContext context, ILogger<EmailQueueService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Agrega un correo a la cola para envío asíncrono
    /// </summary>
    public async Task<int> EnqueueEmailAsync(string toEmail, string subject, string body, string emailType, string? metadata = null)
    {
        try
        {
            var emailQueue = new EmailQueue
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                EmailType = emailType,
                Status = "pending",
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                Metadata = metadata
            };

            _context.EmailQueue.Add(emailQueue);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Correo agregado a la cola: ID={EmailQueueId}, To={ToEmail}, Type={EmailType}", 
                emailQueue.Id, toEmail, emailType);

            return emailQueue.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar correo a la cola: To={ToEmail}, Type={EmailType}", toEmail, emailType);
            throw;
        }
    }

    /// <summary>
    /// Obtiene el estado de un correo en la cola
    /// </summary>
    public async Task<string?> GetEmailStatusAsync(int emailQueueId)
    {
        try
        {
            var emailQueue = await _context.EmailQueue
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == emailQueueId);

            return emailQueue?.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado del correo: EmailQueueId={EmailQueueId}", emailQueueId);
            return null;
        }
    }

    /// <summary>
    /// Obtiene información completa del estado de un correo en la cola
    /// </summary>
    public async Task<EmailQueueStatusDto?> GetEmailStatusDetailAsync(int emailQueueId)
    {
        try
        {
            var emailQueue = await _context.EmailQueue
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == emailQueueId);

            if (emailQueue == null)
                return null;

            return new EmailQueueStatusDto
            {
                Id = emailQueue.Id,
                Status = emailQueue.Status,
                ToEmail = emailQueue.ToEmail,
                EmailType = emailQueue.EmailType,
                ErrorMessage = emailQueue.ErrorMessage,
                ErrorType = emailQueue.ErrorType,
                RetryCount = emailQueue.RetryCount,
                CreatedAt = emailQueue.CreatedAt,
                SentAt = emailQueue.SentAt,
                FailedAt = emailQueue.FailedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener detalle del correo: EmailQueueId={EmailQueueId}", emailQueueId);
            return null;
        }
    }
}

