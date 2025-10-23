namespace ZooSanMarino.Domain.Entities;

public class MenuPermission
{
    public int MenuId { get; set; }
    public int PermissionId { get; set; }

    public Menu Menu { get; set; } = null!;
    public Permission Permission { get; set; } = null!;

    
}
