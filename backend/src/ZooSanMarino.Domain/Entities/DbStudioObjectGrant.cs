namespace ZooSanMarino.Domain.Entities;

public enum DbStudioAccessLevel
{
    Read = 0,
    Write = 1
}

/// <summary>
/// Permiso de un usuario sobre un objeto (tabla/vista) concreto del DB Studio.
/// Los administradores ven/operan todo y no requieren filas aquí.
/// </summary>
public class DbStudioObjectGrant
{
    public long Id { get; set; }

    /// <summary>Usuario beneficiario (FK lógica a users.id, Guid).</summary>
    public Guid UserId { get; set; }

    /// <summary>Empresa en cuyo contexto aplica el permiso.</summary>
    public int CompanyId { get; set; }

    public string SchemaName { get; set; } = "public";
    public string ObjectName { get; set; } = null!;

    /// <summary>Read = solo SELECT/preview; Write = además insert/update/delete de datos.</summary>
    public DbStudioAccessLevel AccessLevel { get; set; } = DbStudioAccessLevel.Read;

    public Guid GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}
