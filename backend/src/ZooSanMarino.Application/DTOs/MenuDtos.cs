namespace ZooSanMarino.Application.DTOs;

// Para lectura (árbol)
public record MenuItemDto(
    int Id,
    string Label,
    string? Icon,
    string? Route,
    int Order,
    MenuItemDto[] Children
);

// Para ABM
public record CreateMenuDto(
    string Label,
    string? Icon,
    string? Route,
    int? ParentId,
    int Order,
    bool IsActive,
    int[] PermissionIds  // permisos requeridos para ver el ítem (vacío = público)
);

public record UpdateMenuDto(
    int Id,
    string Label,
    string? Icon,
    string? Route,
    int? ParentId,
    int Order,
    bool IsActive,
    int[] PermissionIds
);
