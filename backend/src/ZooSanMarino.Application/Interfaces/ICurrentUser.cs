namespace ZooSanMarino.Application.Interfaces;

public interface ICurrentUser
{
    int CompanyId { get; }
    int UserId { get; }
    int? PaisId { get; }
    string? ActiveCompanyName { get; }
    Guid? UserGuid { get; }
    IReadOnlyList<string> Permissions { get; }
}
