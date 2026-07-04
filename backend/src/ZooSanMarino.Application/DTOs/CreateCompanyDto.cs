// src/ZooSanMarino.Application/DTOs/CreateCompanyDto.cs
namespace ZooSanMarino.Application.DTOs;

public record CreateCompanyDto(
    string   Name,
    string   Identifier,    // número
    string   DocumentType,  // tipo
    string?  Address,
    string?  Phone,
    string?  Email,
    string?  Country,
    string?  State,
    string?  City,
    string?  LogoDataUrl,
    string[] VisualPermissions,
    bool     MobileAccess,
    // Default GLOBAL de la empresa: ¿el alimento se maneja a nivel GALPÓN? (cada granja puede overridear)
    bool     ManejaAlimentoPorGalpon = false
);