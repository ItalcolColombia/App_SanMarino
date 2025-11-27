namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Tabla intermedia para relacionar empresas con países (muchos a muchos)
/// Permite que una empresa opere en múltiples países y un país tenga múltiples empresas
/// </summary>
public class CompanyPais
{
    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    
    // Navegación
    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
    
    // Campos de auditoría
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}





