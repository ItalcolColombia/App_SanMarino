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
    bool     MobileAccess,
    // Default GLOBAL de la empresa: ¿el alimento se maneja a nivel GALPÓN? (cada granja puede overridear)
    bool     ManejaAlimentoPorGalpon = false,
    // ¿La empresa maneja códigos ERP avícolas (bodega/C.O./instalación/ubicación/centro de costo)?
    bool     ManejaCodigosErpAvicola = false,
    // ¿La empresa clasifica los huevos por ÍTEMS del catálogo (Primera/Pnc) en el seguimiento
    // diario de producción, en vez de las 11 columnas fijas?
    bool     ClasificacionHuevoPorItems = false,
    // ¿La empresa puede trasladar aves entre etapas (Levante → Producción) desde el seguimiento diario?
    bool     PermiteTrasladoAvesCrossEtapa = false
);
