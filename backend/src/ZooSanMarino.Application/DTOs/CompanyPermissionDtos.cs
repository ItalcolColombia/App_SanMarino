namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Un permiso del catálogo global visto desde una empresa. Siempre se devuelve el catálogo COMPLETO
/// (para que la pantalla de configuración pueda marcar/desmarcar), con <see cref="IsEnabled"/>
/// diciendo si esta empresa lo tiene habilitado.
/// </summary>
/// <param name="EnUsoPorRoles">
/// Cuántos roles vinculados a la empresa ya tienen el permiso asignado. Sirve para que el admin no
/// apague a ciegas algo que está en uso (regla R5: apagar no borra, deja huérfanos).
/// </param>
public record CompanyPermissionItemDto(
    int Id,
    string Key,
    string? Description,
    bool IsEnabled,
    int EnUsoPorRoles
);

/// <summary>Request para fijar los permisos habilitados de una empresa (reemplaza la configuración).</summary>
public record SetCompanyPermissionsRequest(
    int[] PermissionIds
);
