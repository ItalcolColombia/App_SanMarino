// src/ZooSanMarino.Application/DTOs/CompanyDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para información de empresa
/// </summary>
public record CompanyDto(
    int Id,
    string Name,
    string Identifier,
    string DocumentType,
    string? Address,
    string? Phone,
    string? Email,
    string? Country,
    string? State,
    string? City,
    string? LogoDataUrl,
    bool MobileAccess,
    string[] VisualPermissions,
    bool ManejaAlimentoPorGalpon = false,
    // ¿La empresa maneja códigos ERP avícolas (bodega/C.O./instalación/ubicación/centro de costo)?
    // El front lo usa para mostrar u ocultar esos campos en granja, núcleo, galpón y lote.
    bool ManejaCodigosErpAvicola = false,
    // ¿La empresa clasifica los huevos por ÍTEMS del catálogo (Primera/Pnc) en vez de las 11
    // columnas fijas? El front lo usa para pintar filas dinámicas de ítem+cantidad en el
    // seguimiento diario de producción.
    bool ClasificacionHuevoPorItems = false,
    // ¿La empresa puede trasladar aves entre etapas (Levante → Producción) desde el seguimiento
    // diario? El front lo usa para habilitar el selector de etapa destino en el modal de traslado.
    bool PermiteTrasladoAvesCrossEtapa = false
);