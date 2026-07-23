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

    /// <summary>
    /// Override de la granja para el manejo de alimento: <c>null</c> = hereda el default de la
    /// empresa (<see cref="Company.ManejaAlimentoPorGalpon"/>); <c>true</c> = fuerza nivel GALPÓN;
    /// <c>false</c> = fuerza nivel GRANJA. Resolución efectiva: <c>farm ?? company</c>.
    /// </summary>
    public bool? ManejaAlimentoPorGalpon { get; set; }

    /// <summary>
    /// Panamá: código ERP vigente de la granja para lotes de pollo engorde (ej. "4001017" =
    /// prefijo granja + lote base activo). Los lotes nuevos lo capturan en <c>lote_erp</c>; al
    /// cerrar TODOS los lotes del lote base en la granja avanza +1 automáticamente
    /// (4001017 → 4001018 … 4001099 → 4001100). <c>null</c> = comportamiento actual (otros países).
    /// </summary>
    public string? CodigoErpEngorde { get; set; }

    public ICollection<Nucleo> Nucleos { get; set; } = new List<Nucleo>();
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    public ICollection<Galpon> Galpones { get; set; } = new List<Galpon>();
    public ICollection<UserFarm> UserFarms { get; set; } = new List<UserFarm>();
}
