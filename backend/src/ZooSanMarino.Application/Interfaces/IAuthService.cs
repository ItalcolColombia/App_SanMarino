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
    // Task<PasswordRecoveryResponseDto> RecoverPasswordAsync(PasswordRecoveryRequestDto dto); // Temporalmente comentado para debug

    // Nuevo: bootstrap de sesión para el front
    Task<SessionBootstrapDto> GetSessionAsync(Guid userId, int? companyId = null);
}
