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

// Respuesta del endpoint de menú con información del país activo
public class MenuWithCountryDto
{
    public IEnumerable<MenuItemDto> Menu { get; set; } = Array.Empty<MenuItemDto>();
    public int? PaisId { get; set; }
    public string? PaisNombre { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
}
