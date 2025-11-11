// src/ZooSanMarino.Application/DTOs/EmailQueueStatusDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para el estado de un correo en la cola
/// </summary>
public class EmailQueueStatusDto
{
    /// <summary>ID del correo en la cola</summary>
    public int Id { get; set; }
    
    /// <summary>Estado del correo: pending, processing, sent, failed</summary>
    public string Status { get; set; } = null!;
    
    /// <summary>Correo del destinatario</summary>
    public string ToEmail { get; set; } = null!;
    
    /// <summary>Tipo de correo: welcome, password_recovery, etc.</summary>
    public string EmailType { get; set; } = null!;
    
    /// <summary>Mensaje de error si falló</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Tipo de error si falló</summary>
    public string? ErrorType { get; set; }
    
    /// <summary>Número de reintentos</summary>
    public int RetryCount { get; set; }
    
    /// <summary>Fecha de creación</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>Fecha de envío (si se envió)</summary>
    public DateTime? SentAt { get; set; }
    
    /// <summary>Fecha de fallo (si falló)</summary>
    public DateTime? FailedAt { get; set; }
}


