namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Bitácora de toda operación de escritura/DDL/control de sesiones ejecutada desde DB Studio.
/// </summary>
public class DbStudioAudit
{
    public long Id { get; set; }

    /// <summary>Acción lógica: SELECT, EXECUTE, CREATE_TABLE, INSERT_DATA, TERMINATE_BACKEND, etc.</summary>
    public string Action { get; set; } = null!;

    public string? SchemaName { get; set; }
    public string? ObjectName { get; set; }

    /// <summary>SQL efectivamente enviado (o resumen de la acción).</summary>
    public string SqlText { get; set; } = string.Empty;

    /// <summary>Resumen del resultado en JSON (filas afectadas, error, pid, etc.).</summary>
    public string ResultSummary { get; set; } = "{}";

    public bool Success { get; set; }

    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public int CompanyId { get; set; }
    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
