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
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; } = false;
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
