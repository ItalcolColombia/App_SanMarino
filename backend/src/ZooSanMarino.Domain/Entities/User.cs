namespace ZooSanMarino.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string surName { get; set; } = null!;
        public string firstName { get; set; } = null!;
        public string cedula { get; set; } = null!;
        public string telefono { get; set; } = null!;
        public string ubicacion { get; set; } = null!;
        public string? Zona { get; set; }     // 'Zona 1' | 'Zona 2' | NULL = sin restricción (Panamá)
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Super Admin: atraviesa el aislamiento multiempresa (puede operar sobre empresas a las que
        /// no pertenece) y ve el catálogo global. Es un DATO, no un correo en el código: hasta
        /// ago-2026 la regla estaba escrita a mano en 14 sitios comparando un email, así que
        /// concederla o quitarla exigía desplegar. Va en el usuario y no en el rol a propósito: el
        /// rol `Admin` lo tiene más de una persona, y la marca no puede ampliarse sola.
        /// </summary>
        public bool IsSuperAdmin { get; set; } = false;
        public DateTime? LockedAt { get; set; }
        public int FailedAttempts { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public ICollection<UserLogin>    UserLogins    { get; set; } = new List<UserLogin>();
        public ICollection<UserCompany>  UserCompanies { get; set; } = new List<UserCompany>();
        public ICollection<UserRole>     UserRoles     { get; set; } = new List<UserRole>();
        public ICollection<UserFarm>     UserFarms     { get; set; } = new List<UserFarm>();
    }

}
