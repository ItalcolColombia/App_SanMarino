namespace ZooSanMarino.Domain.Entities
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Identifier { get; set; } = null!;  // antes Nit
        public string DocumentType { get; set; } = null!;

        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string[] VisualPermissions { get; set; } = Array.Empty<string>();
        public bool MobileAccess { get; set; }

        // ← Añadimos las colecciones de navegación:
        public ICollection<Farm> Farms { get; set; } = new List<Farm>();
        public ICollection<Regional> Regionales { get; set; } = new List<Regional>();
        public ICollection<Zona> Zonas { get; set; } = new List<Zona>();
        public ICollection<RoleCompany> RoleCompanies { get; set; } = new List<RoleCompany>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
        public ICollection<CompanyPais> CompanyPaises { get; set; } = new List<CompanyPais>();


    }
}
