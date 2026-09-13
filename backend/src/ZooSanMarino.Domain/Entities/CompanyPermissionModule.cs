namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Qué módulos de permisos tiene una empresa. Prender o apagar un módulo materializa el resultado en
/// <see cref="CompanyPermission"/>, que es lo que el runtime lee.
/// <para>
/// Se localiza por <c>companies.name</c> / <c>permission_modules.key</c> en los seeds, nunca por id.
/// </para>
/// </summary>
public class CompanyPermissionModule
{
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int ModuleId { get; set; }
    public PermissionModule Module { get; set; } = null!;

    /// <summary>Se conserva la fila con <c>false</c> al apagar el módulo, igual que <see cref="CompanyPermission"/>.</summary>
    public bool IsEnabled { get; set; } = true;
}
