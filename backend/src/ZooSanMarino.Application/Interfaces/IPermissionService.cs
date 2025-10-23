using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IPermissionService
{
    Task<IEnumerable<PermissionDto>> GetAllAsync();
    Task<string[]>                   GetAllKeysAsync();
    Task<PermissionDto?>             GetByIdAsync(int id);
    Task<PermissionDto>              CreateAsync(CreatePermissionDto dto);
    Task<PermissionDto?>             UpdateAsync(UpdatePermissionDto dto);
    Task<bool>                       DeleteAsync(int id);
}
