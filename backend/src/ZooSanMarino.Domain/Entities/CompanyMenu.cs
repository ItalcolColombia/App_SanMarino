namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Relación empresa-menú: qué ítems del menú tiene asignada cada empresa y si están habilitados.
/// </summary>
public class CompanyMenu
{
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int MenuId { get; set; }
    public Menu Menu { get; set; } = null!;

    /// <summary>Si el ítem está habilitado para esta empresa.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Orden de visualización para esta empresa (0-based).</summary>
    public int SortOrder { get; set; }

    /// <summary>Padre del ítem en el menú de esta empresa; null = usar Menu.ParentId global.</summary>
    public int? ParentMenuId { get; set; }
}
