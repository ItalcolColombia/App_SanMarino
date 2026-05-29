// ZooSanMarino.Domain/Entities/Farm.cs
namespace ZooSanMarino.Domain.Entities;
// Domain/Entities/Farm.cs
public class Farm : AuditableEntity
{
    public int Id { get; set; }
    public new int CompanyId { get; set; }
    public string Name { get; set; } = default!;
    public int? RegionalId { get; set; }      // ← nullable
    public string Status { get; set; } = "A"; // ← 1-char con default 'A'
    public int DepartamentoId { get; set; }
    public int MunicipioId { get; set; }

    // Panamá: cliente asociado y zona denormalizada (se sincroniza con el cliente)
    public int? ClienteId { get; set; }
    public string? Zona { get; set; }              // 'Zona 1' | 'Zona 2'
    public bool CertificadoGab { get; set; }       // Sí/No (Panamá)
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    public ICollection<Nucleo> Nucleos { get; set; } = new List<Nucleo>();
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    public ICollection<Galpon> Galpones { get; set; } = new List<Galpon>();
    public ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
}
