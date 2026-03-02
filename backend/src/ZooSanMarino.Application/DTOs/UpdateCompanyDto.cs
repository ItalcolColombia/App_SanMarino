// src/ZooSanMarino.Application/DTOs/UpdateCompanyDto.cs
namespace ZooSanMarino.Application.DTOs;

public record UpdateCompanyDto(
    int      Id,
    string   Name,
    string   Identifier,
    string   DocumentType,
    string?  Address,
    string?  Phone,
    string?  Email,
    string?  Country,
    string?  State,
    string?  City,
    string?  LogoDataUrl,
    string[] VisualPermissions,
    bool     MobileAccess
);
