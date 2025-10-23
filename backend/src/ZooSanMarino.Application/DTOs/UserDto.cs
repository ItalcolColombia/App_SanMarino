// src/ZooSanMarino.Application/DTOs/UserDto.cs
namespace ZooSanMarino.Application.DTOs;

public record UserDto(
    Guid     Id,
    string   SurName,
    string   FirstName,
    string   Cedula,
    string   Telefono,
    string   Ubicacion,
    string[] Roles,
    int[]    CompanyIds,
    UserFarmLiteDto[] Farms,
    bool     IsActive,
    bool     IsLocked,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record CreateUserDto(
    string   SurName,
    string   FirstName,
    string   Cedula,
    string   Telefono,
    string   Ubicacion,
    string   Email,
    string   Password,
    int[]    CompanyIds,
    int[]    RoleIds,
    int[]    FarmIds
);
public record UpdateUserDto(
    string?  SurName,
    string?  FirstName,
    string?  Cedula,
    string?  Telefono,
    string?  Ubicacion,
    bool?    IsActive,
    bool?    IsLocked,
    int[]?   CompanyIds,
    int[]?   RoleIds,
    int[]?   FarmIds
);

