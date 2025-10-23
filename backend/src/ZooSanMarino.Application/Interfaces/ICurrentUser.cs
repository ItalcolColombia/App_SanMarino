namespace ZooSanMarino.Application.Interfaces;

public interface ICurrentUser
{
    int CompanyId { get; }
    int UserId { get; }
    string? ActiveCompanyName { get; }
}
