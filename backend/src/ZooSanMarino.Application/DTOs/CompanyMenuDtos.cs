namespace ZooSanMarino.Application.DTOs;

/// <summary>Ítem de menú asignado a una empresa, con estado habilitado/deshabilitado.</summary>
public record CompanyMenuItemDto(
    int Id,
    string Label,
    string? Icon,
    string? Route,
    int Order,
    bool IsEnabled,
    CompanyMenuItemDto[] Children
);

/// <summary>Request para asignar o actualizar los menús de una empresa.</summary>
public record SetCompanyMenusRequest(
    int[] MenuIds,
    bool IsEnabled = true
);

/// <summary>Un ítem en la estructura de menú de la empresa (para reordenar y reparentar).</summary>
public record CompanyMenuItemStructureDto(
    int MenuId,
    int SortOrder,
    int? ParentMenuId,
    bool IsEnabled = true
);

/// <summary>Request para actualizar orden y jerarquía de menús de una empresa.</summary>
public record UpdateCompanyMenuStructureRequest(
    CompanyMenuItemStructureDto[] Items
);
