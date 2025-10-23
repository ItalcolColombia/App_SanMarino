namespace ZooSanMarino.Domain.Entities;

public class Menu
{
    public int Id { get; set; }
    public string Label { get; set; } = null!;
    public string? Icon { get; set; }          // opcional: nombre de icono
    public string? Route { get; set; }         // opcional: /ruta
    public int Order { get; set; }             // orden entre hermanos
    public bool IsActive { get; set; } = true;

    public int? ParentId { get; set; }
    public Menu? Parent { get; set; }
    public ICollection<Menu> Children { get; set; } = new List<Menu>();

    public ICollection<MenuPermission> MenuPermissions { get; set; } = new List<MenuPermission>();
    // Menu.cs
    public ICollection<RoleMenu> RoleMenus { get; set; } = new List<RoleMenu>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
