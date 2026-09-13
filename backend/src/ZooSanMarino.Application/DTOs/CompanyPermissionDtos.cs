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
/// <param name="Modulos">
/// Keys de los módulos de permisos que lo contienen (M:N), ordenados por <c>permission_modules.orden</c>.
/// Vacío = sin clasificar. Campo aditivo: sirve para agrupar la lista en las pantallas.
/// </param>
public record CompanyPermissionItemDto(
    int Id,
    string Key,
    string? Description,
    bool IsEnabled,
    int EnUsoPorRoles,
    IReadOnlyList<string> Modulos
);

/// <summary>Request para fijar los permisos habilitados de una empresa (reemplaza la configuración).</summary>
public record SetCompanyPermissionsRequest(
    int[] PermissionIds
);
