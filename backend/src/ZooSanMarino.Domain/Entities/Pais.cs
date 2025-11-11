namespace ZooSanMarino.Domain.Entities;
public class Pais
{
    public int    PaisId     { get; set; }       // PK
    public string PaisNombre{ get; set; } = null!;

    // Navegación
    public ICollection<Departamento> Departamentos { get; set; } = new List<Departamento>();
    public ICollection<CompanyPais> CompanyPaises { get; set; } = new List<CompanyPais>();
    public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
}
