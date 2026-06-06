namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Perfiles de atención asociados a un ROL (defaults).
/// Al asignar el rol a un usuario, se crean/completan sus <see cref="TicketResolutor"/>
/// automáticamente (si no existen ya).
/// </summary>
public class TicketResolutorRol
{
    public long Id { get; set; }

    public int RoleId { get; set; }

    /// <summary>SOPORTE | DESARROLLO | REQUERIMIENTO | DUDAS</summary>
    public string Tipo { get; set; } = default!;

    /// <summary>País que atiende el rol. NULL = global.</summary>
    public int? PaisId { get; set; }

    public int CompanyId { get; set; }
    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
