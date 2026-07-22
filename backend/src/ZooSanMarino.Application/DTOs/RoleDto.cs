// src/ZooSanMarino.Application/DTOs/RoleDto.cs
namespace ZooSanMarino.Application.DTOs;
// src/ZooSanMarino.Application/DTOs/RoleDto.cs  (si ya lo tienes, déjalo igual)
public record RoleDto(
    int Id,
    string Name,
    string[] Permissions,
    int[] CompanyIds,
    int[] MenuIds,
    bool IsCompanyAdmin = false
);

// src/ZooSanMarino.Application/DTOs/UpdateRoleDto.cs  (AQUÍ agregamos MenuIds)
public record UpdateRoleDto(
    int Id,
    string Name,
    string[] Permissions,
    int[] CompanyIds,
    int[]? MenuIds, // <- null = no tocar menús; array = reemplazar por delta
    bool? IsCompanyAdmin = null // <- null = no tocar; solo Super Admin puede cambiarlo
);

// src/ZooSanMarino.Application/DTOs/CreateRoleDto.cs (si quieres crear rol con menús desde el inicio, opcional)
public record CreateRoleDto(
    string Name,
    string[] Permissions,
    int[] CompanyIds,
    int[]? MenuIds, // opcional
    bool IsCompanyAdmin = false // solo Super Admin puede activarlo
);
