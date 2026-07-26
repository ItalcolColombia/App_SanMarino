// src/ZooSanMarino.Domain/Entities/Galpon.cs
namespace ZooSanMarino.Domain.Entities;
public class Galpon : AuditableEntity
{
    public string GalponId   { get; set; } = null!;
    public string NucleoId   { get; set; } = null!;
    public int    GranjaId   { get; set; }

    public string  GalponNombre { get; set; } = null!;
    public string? Ancho        { get; set; }
    public string? Largo        { get; set; }
    public string? TipoGalpon   { get; set; }

    /// <summary>
    /// Código ERP de ubicación del galpón (ej. "BG60201"); solo se captura en empresas con
    /// <see cref="Company.ManejaCodigosErpAvicola"/> = true. Pass-through, sin lógica.
    /// </summary>
    public string? CodigoErpUbicacion { get; set; }
    /// <summary>Descripción de la ubicación ERP del galpón.</summary>
    public string? DescripcionErpUbicacion { get; set; }

    public new int CompanyId { get; set; }

    // Navegación
    public Nucleo Nucleo   { get; set; } = null!;
    public Farm   Farm     { get; set; } = null!;
    public Company Company { get; set; } = null!; // 👈 NUEVO
}
