// src/ZooSanMarino.Domain/Entities/Nucleo.cs
namespace ZooSanMarino.Domain.Entities;

public class Nucleo : AuditableEntity
{
    // PK natural (string) + FK a Farm por GranjaId
    public string NucleoId     { get; set; } = null!;
    public int    GranjaId     { get; set; }
    public string NucleoNombre { get; set; } = null!;

    // Navegación
    public Farm Farm { get; set; } = null!;

    // 1:N Núcleo → Galpones y Lotes
    public ICollection<Galpon> Galpones { get; set; } = new List<Galpon>();
    public ICollection<Lote>   Lotes    { get; set; } = new List<Lote>();
}
