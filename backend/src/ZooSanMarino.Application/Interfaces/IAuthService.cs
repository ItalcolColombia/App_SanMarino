// src/ZooSanMarino.Application/Interfaces/IAuthService.cs
using System;
using System.Threading.Tasks;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    Task ChangeEmailAsync(Guid userId, ChangeEmailDto dto);
    Task<PasswordRecoveryResponseDto> RecoverPasswordAsync(PasswordRecoveryRequestDto dto);

    // Nuevo: bootstrap de sesión para el front
    Task<SessionBootstrapDto> GetSessionAsync(Guid userId, int? companyId = null);
    
    // Nuevo: obtener menú del usuario (separado del login)
    Task<IEnumerable<MenuItemDto>> GetMenuForUserAsync(Guid userId, int? companyId = null);
    
    // Nuevo: obtener menús por rol del usuario
    Task<List<RoleMenusLiteDto>> GetMenusByRoleForUserAsync(Guid userId);
}
