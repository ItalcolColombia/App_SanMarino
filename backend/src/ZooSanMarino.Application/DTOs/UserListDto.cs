// src/ZooSanMarino.Application/DTOs/UserListDto.cs
namespace ZooSanMarino.Application.DTOs;

public class UserListDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string SurName  { get; set; } = string.Empty;
    public string? Email   { get; set; }
    public bool IsActive   { get; set; }
    public string Cedula   { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Ubicacion { get; set; } = string.Empty;

    /// <summary>
    /// Zona de restricción (Panamá): 'Zona 1' | 'Zona 2' | null = sin restricción.
    /// </summary>
    public string? Zona { get; set; }

    // Ya existía:
    public List<string> Roles { get; set; } = new();

    // NUEVOS:
    public List<string> CompanyNames { get; set; } = new();
    public string? PrimaryCompany { get; set; }
    public string? PrimaryRole { get; set; }
}
