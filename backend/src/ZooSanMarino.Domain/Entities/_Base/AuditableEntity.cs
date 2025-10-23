// src/ZooSanMarino.Domain/Entities/_Base/AuditableEntity.cs
namespace ZooSanMarino.Domain.Entities;

public abstract class AuditableEntity
{
    public int CompanyId { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    public int?      UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt       { get; set; }

    // Soft-delete (opcional)
    public DateTime? DeletedAt { get; set; }
}
