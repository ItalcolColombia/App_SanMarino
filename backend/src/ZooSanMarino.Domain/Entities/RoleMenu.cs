// src/ZooSanMarino.Domain/Entities/RoleMenu.cs
namespace ZooSanMarino.Domain.Entities
{
    public class RoleMenu
    {
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        public int MenuId { get; set; }
        public Menu Menu { get; set; } = null!;
    }
}
