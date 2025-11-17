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

    public new int CompanyId { get; set; }

    // Navegación
    public Nucleo Nucleo   { get; set; } = null!;
    public Farm   Farm     { get; set; } = null!;
    public Company Company { get; set; } = null!; // 👈 NUEVO
}
