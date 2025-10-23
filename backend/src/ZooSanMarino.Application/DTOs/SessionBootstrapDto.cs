// src/ZooSanMarino.Application/DTOs/SessionBootstrapDto.cs
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs;


public record SessionBootstrapDto(
    Guid UserId,
    string Email,
    string FullName,
    bool IsActive,
    bool IsLocked,
    DateTime? LastLoginAt,
    int? SelectedCompanyId,
    IReadOnlyList<CompanyLiteDto> Companies,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<MenuItemDto> Menu
);
