// src/ZooSanMarino.Application/Interfaces/IEmailQueueService.cs
using ZooSanMarino.Application.DTOs;
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Interfaz para el servicio de cola de correos electrónicos
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Agrega un correo a la cola para envío asíncrono
    /// </summary>
    /// <param name="toEmail">Correo del destinatario</param>
    /// <param name="subject">Asunto del correo</param>
    /// <param name="body">Cuerpo HTML del correo</param>
    /// <param name="emailType">Tipo de correo (welcome, password_recovery, etc.)</param>
    /// <param name="metadata">Información adicional en formato JSON (opcional)</param>
    /// <returns>ID del registro en la cola</returns>
    Task<int> EnqueueEmailAsync(string toEmail, string subject, string body, string emailType, string? metadata = null);

    /// <summary>
    /// Obtiene el estado de un correo en la cola
    /// </summary>
    /// <param name="emailQueueId">ID del registro en la cola</param>
    /// <returns>Estado del correo (pending, processing, sent, failed) o null si no existe</returns>
    Task<string?> GetEmailStatusAsync(int emailQueueId);
    
    /// <summary>
    /// Obtiene información completa del estado de un correo en la cola
    /// </summary>
    /// <param name="emailQueueId">ID del registro en la cola</param>
    /// <returns>DTO con información completa del correo o null si no existe</returns>
    Task<EmailQueueStatusDto?> GetEmailStatusDetailAsync(int emailQueueId);
}

