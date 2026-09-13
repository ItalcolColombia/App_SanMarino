namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Clasificación M:N de un permiso dentro de un <see cref="PermissionModule"/>. Un permiso sin ninguna
/// fila acá está «sin clasificar»: ningún módulo lo prende ni lo apaga y se gestiona suelto.
/// </summary>
public class PermissionModulePermission
{
    public int ModuleId { get; set; }
    public PermissionModule Module { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
