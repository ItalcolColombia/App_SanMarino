using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IRoleCompositeService
{
    // ===== Roles =====
    Task<IEnumerable<RoleDto>> Roles_GetAllAsync(string? q, int page, int pageSize);
    Task<RoleDto?> Roles_GetByIdAsync(int id);
    Task<RoleDto> Roles_CreateAsync(CreateRoleDto dto);
    Task<RoleDto?> Roles_UpdateAsync(UpdateRoleDto dto);
    Task<bool> Roles_DeleteAsync(int id);

    Task<string[]?> Roles_GetPermissionsAsync(int roleId);
    Task<RoleDto?> Roles_AddPermissionsAsync(int roleId, string[] keys);
    Task<RoleDto?> Roles_RemovePermissionsAsync(int roleId, string[] keys);
    Task<RoleDto?> Roles_ReplacePermissionsAsync(int roleId, string[] keys);

    // ===== Permissions (catálogo) =====
    Task<IEnumerable<PermissionDto>> Permissions_GetAllAsync();

    // ===== Menús (catálogo/CRUD/filtrado) =====
    Task<IEnumerable<MenuItemDto>> Menus_GetTreeAsync();
    Task<IEnumerable<MenuItemDto>> Menus_GetForUserAsync(Guid userId, int? companyId);
    Task<MenuItemDto> Menus_CreateAsync(CreateMenuDto dto);
    Task<MenuItemDto?> Menus_UpdateAsync(UpdateMenuDto dto);
    Task<bool> Menus_DeleteAsync(int id);

    // ===== Menús asignados a Roles =====
    Task<int[]?>   Roles_GetMenusAsync(int roleId);
    Task<RoleDto?> Roles_AddMenusAsync(int roleId, int[] menuIds);
    Task<RoleDto?> Roles_RemoveMenusAsync(int roleId, int[] menuIds);
    Task<RoleDto?> Roles_ReplaceMenusAsync(int roleId, int[] menuIds);
}
