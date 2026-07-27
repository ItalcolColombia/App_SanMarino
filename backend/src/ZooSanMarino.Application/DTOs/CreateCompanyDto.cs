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
    bool     ManejaAlimentoPorGalpon = false,
    // ¿La empresa maneja códigos ERP avícolas (bodega/C.O./instalación/ubicación/centro de costo)?
    bool     ManejaCodigosErpAvicola = false,
    // ¿La empresa clasifica los huevos por ÍTEMS del catálogo (Primera/Pnc) en el seguimiento
    // diario de producción, en vez de las 11 columnas fijas?
    bool     ClasificacionHuevoPorItems = false,
    // ¿La empresa puede trasladar aves entre etapas (Levante → Producción) desde el seguimiento diario?
    bool     PermiteTrasladoAvesCrossEtapa = false,
    // ¿La empresa captura la clasificación de huevos en el seguimiento diario de LEVANTE desde la
    // semana 14 (con arrastre del acumulado a producción al liquidar)?
    bool     CapturaHuevosEnLevante = false,
    // ¿El peso báscula de la venta de pollo engorde llega al día siguiente (se carga al confirmar)?
    bool     VentaEngordePesoDiferido = false
);