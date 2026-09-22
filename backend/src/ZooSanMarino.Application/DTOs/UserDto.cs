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
    DateTime? LastLoginAt,
    string?  Zona = null,
    // Solo se completan al crear (ver UserService.CreateAsync); en GetAll/GetById/Update quedan
    // null salvo Email, que se resuelve siempre desde el mismo join a UserLogins→Login.
    string?  Email = null,
    bool?    EmailSent = null,
    int?     EmailQueueId = null
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
    int[]    FarmIds,
    string?  Zona = null,
    // Espejo de RegisterDto.IsPlatformUser: email sintético (@zootecnico.com), sin correo de
    // bienvenida y con IsEmailLogin=false. Ver UserService.CreateAsync.
    bool     IsPlatformUser = false
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
    int[]?   FarmIds,
    string?  Zona = null
);

