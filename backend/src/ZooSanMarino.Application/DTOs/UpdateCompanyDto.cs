// src/ZooSanMarino.Application/DTOs/UpdateCompanyDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Actualización de empresa. Los flags de comportamiento son <c>bool?</c> a propósito:
/// <b>omitirlos conserva el valor actual</b>. Antes eran <c>bool = false</c>, así que cualquier
/// cliente que no los mandara (p. ej. el formulario de Configuración → Empresas, que sólo envía
/// datos de contacto) los APAGABA en silencio — una empresa con peso diferido o clasificación de
/// huevos por ítems perdía su configuración con sólo corregirle el teléfono.
/// Enviar <c>false</c> explícito sigue apagando el flag.
/// </summary>
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
    bool?    ManejaAlimentoPorGalpon = null,
    // ¿La empresa maneja códigos ERP avícolas (bodega/C.O./instalación/ubicación/centro de costo)?
    bool?    ManejaCodigosErpAvicola = null,
    // ¿La empresa clasifica los huevos por ÍTEMS del catálogo (Primera/Pnc) en el seguimiento
    // diario de producción, en vez de las 11 columnas fijas?
    bool?    ClasificacionHuevoPorItems = null,
    // ¿La empresa puede trasladar aves entre etapas (Levante → Producción) desde el seguimiento diario?
    bool?    PermiteTrasladoAvesCrossEtapa = null,
    // ¿La empresa captura la clasificación de huevos en el seguimiento diario de LEVANTE desde la
    // semana 14 (con arrastre del acumulado a producción al liquidar)?
    bool?    CapturaHuevosEnLevante = null,
    // ¿El peso báscula de la venta de pollo engorde llega al día siguiente (se carga al confirmar)?
    bool?    VentaEngordePesoDiferido = null,
    /// <summary>La hora de llegada de las aves decide el primer día con registro del lote (≥13:00 ⇒ día siguiente).</summary>
    bool?    PrimerRegistroSegunHoraLlegada = null
);
