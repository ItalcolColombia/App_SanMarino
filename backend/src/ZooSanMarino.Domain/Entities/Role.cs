namespace ZooSanMarino.Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }  // puede ser nullable

        /// <summary>
        /// Rol "Administrador de Empresa/País": otorga visibilidad global de las
        /// entidades activas de la empresa (hoy: todas las granjas al asignar usuarios).
        /// Solo un Super Admin puede activar/desactivar este flag.
        /// </summary>
        public bool IsCompanyAdmin { get; set; }

        /// <summary>
        /// Qué tipos de ticket puede ABRIR quien tenga este rol: NORMAL | IMPLEMENTADOR (ver
        /// <c>NivelTicket</c>). NULL = el rol no define ⇒ NORMAL, igual que antes de existir la
        /// columna. Es lo opuesto a la plantilla de resolutor (quién ATIENDE): dar nivel de apertura
        /// NUNCA convierte a nadie en resolutor.
        /// </summary>
        public string? TicketNivelCreacion { get; set; }

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
