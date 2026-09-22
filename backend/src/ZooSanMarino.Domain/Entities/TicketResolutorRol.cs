namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Perfiles de atención asociados a un ROL: quien tenga el rol ATIENDE (recibe) estos tipos.
/// Se leen en vivo al armar los asignables; desde el 19-sep-2026 ya NO se copian a
/// <see cref="TicketResolutor"/> al asignar el rol (la copia quedaba para siempre aunque se
/// quitara el rol).
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

    /// <summary>EMPRESA (solo tickets de <see cref="CompanyId"/>) | GLOBAL (todas las empresas) — ver <see cref="TicketAlcance"/>.</summary>
    public string Alcance { get; set; } = TicketAlcance.Empresa;

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
