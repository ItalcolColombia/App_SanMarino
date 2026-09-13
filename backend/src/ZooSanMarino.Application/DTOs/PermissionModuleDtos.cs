namespace ZooSanMarino.Application.DTOs;

/// <summary>Un módulo del catálogo con las keys de permiso que agrupa.</summary>
/// <param name="EmpresasConModulo">Cuántas empresas lo tienen prendido (un módulo en uso no se puede borrar).</param>
public record PermissionModuleDto(
    int Id,
    string Key,
    string Nombre,
    string? Descripcion,
    int Orden,
    IReadOnlyList<string> PermissionKeys,
    int EmpresasConModulo
);

/// <summary>Alta de módulo. La <c>Key</c> es inmutable después (los seeds la usan para localizarlo).</summary>
public record CreatePermissionModuleDto(string Key, string Nombre, string? Descripcion, int Orden);

public record UpdatePermissionModuleDto(string Nombre, string? Descripcion, int Orden);

/// <summary>Reemplaza los permisos que agrupa un módulo (clasificación M:N).</summary>
public record SetPermissionModulePermissionsRequest(int[] PermissionIds);

/// <summary>Un módulo visto desde una empresa.</summary>
/// <param name="PermisosHabilitados">De los permisos del módulo, cuántos tiene prendidos la empresa (ajuste fino).</param>
public record CompanyPermissionModuleItemDto(
    int ModuleId,
    string Key,
    string Nombre,
    string? Descripcion,
    int Orden,
    bool IsEnabled,
    int TotalPermisos,
    int PermisosHabilitados
);

/// <summary>Fija los módulos prendidos de una empresa (los no enviados se apagan).</summary>
public record SetCompanyPermissionModulesRequest(int[] ModuleIds);

/// <summary>Efecto de un cambio de módulos o de clasificación sobre <c>company_permissions</c>.</summary>
public record CambioPermisosDto(int EmpresasAfectadas, int PermisosPrendidos, int PermisosApagados);

public enum EliminarModuloResultado
{
    Eliminado,
    NoExiste,
    /// <summary>Alguna empresa lo tiene prendido: hay que apagarlo antes de borrarlo.</summary>
    EnUso
}
