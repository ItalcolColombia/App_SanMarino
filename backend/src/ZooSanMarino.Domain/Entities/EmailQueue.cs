// src/ZooSanMarino.Domain/Entities/EmailQueue.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Entidad para la cola de correos electrónicos
/// </summary>
[Table("email_queue")]
public class EmailQueue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("to_email")]
    public string ToEmail { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Required]
    [Column("body", TypeName = "text")]
    public string Body { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("email_type")]
    public string EmailType { get; set; } = null!; // 'welcome', 'password_recovery', etc.

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "pending"; // 'pending', 'processing', 'sent', 'failed'

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [MaxLength(100)]
    [Column("error_type")]
    public string? ErrorType { get; set; } // 'smtp_auth', 'network', 'invalid_email', etc.

    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    [Column("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("failed_at")]
    public DateTime? FailedAt { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; } // JSON con información adicional
}


