namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO que representa una combinación empresa-país
/// </summary>
public class CompanyPaisDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public int PaisId { get; set; }
    public string PaisNombre { get; set; } = null!;
    public bool IsDefault { get; set; }
}



