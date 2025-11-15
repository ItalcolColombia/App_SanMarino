namespace ZooSanMarino.Domain.Entities
{
    /// <summary>
    /// Relación entre usuarios, empresas y países
    /// Permite que un usuario esté asignado a una empresa en un país específico
    /// </summary>
    public class UserCompany
    {
        public Guid UserId   { get; set; }
        public int  CompanyId { get; set; }
        public int? PaisId   { get; set; } // País asociado (opcional)

        public bool IsDefault { get; set; } = false; // Empresa principal

        // Navegación
        public User    User    { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public Pais?   Pais    { get; set; }
    }
}