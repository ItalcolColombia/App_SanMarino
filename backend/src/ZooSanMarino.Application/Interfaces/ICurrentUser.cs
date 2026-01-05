namespace ZooSanMarino.Application.Interfaces;

public interface ICurrentUser
{
    int CompanyId { get; }
    int UserId { get; }
    int? PaisId { get; } // ← NUEVO: País activo
    string? ActiveCompanyName { get; }
    Guid? UserGuid { get; } // ← NUEVO: Guid del usuario desde el JWT
}
