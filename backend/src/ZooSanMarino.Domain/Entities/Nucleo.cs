// src/ZooSanMarino.Domain/Entities/Nucleo.cs
namespace ZooSanMarino.Domain.Entities;

public class Nucleo : AuditableEntity
{
    // PK natural (string) + FK a Farm por GranjaId
    public string NucleoId     { get; set; } = null!;
    public int    GranjaId     { get; set; }
    public string NucleoNombre { get; set; } = null!;

    /// <summary>
    /// Código de bodega ERP del núcleo (ej. "B3001"); solo se captura en empresas con
    /// <see cref="Company.ManejaCodigosErpAvicola"/> = true. Pass-through, sin lógica.
    /// </summary>
    public string? CodigoBodega { get; set; }
    /// <summary>Descripción de la bodega ERP del núcleo.</summary>
    public string? DescripcionBodega { get; set; }

    // Navegación
    public Farm Farm { get; set; } = null!;

    // 1:N Núcleo → Galpones y Lotes
    public ICollection<Galpon> Galpones { get; set; } = new List<Galpon>();
    public ICollection<Lote>   Lotes    { get; set; } = new List<Lote>();
}
