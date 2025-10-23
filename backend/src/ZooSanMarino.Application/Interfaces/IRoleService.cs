// src/ZooSanMarino.Application/Interfaces/IRoleService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(int id);
    Task<RoleDto> CreateAsync(CreateRoleDto dto);
    Task<RoleDto?> UpdateAsync(UpdateRoleDto dto);
    Task<bool> DeleteAsync(int id);
    Task<RoleDto?> AddPermissionsAsync(int roleId, string[] permissionKeys);
    Task<RoleDto?> RemovePermissionsAsync(int roleId, string[] permissionKeys);
    Task<RoleDto?> ReplacePermissionsAsync(int roleId, string[] permissionKeys);

}
