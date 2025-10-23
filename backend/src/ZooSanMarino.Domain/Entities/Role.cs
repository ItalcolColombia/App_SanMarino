namespace ZooSanMarino.Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }  // puede ser nullable

        // N:M con usuarios
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        // N:M con permisos
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

        // N:M con compañías
        public ICollection<RoleCompany> RoleCompanies { get; set; } = new List<RoleCompany>();
        // Role.cs
        public ICollection<RoleMenu> RoleMenus { get; set; } = new List<RoleMenu>();

    }
}
