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
    bool     VentaEngordePesoDiferido = false,
    /// <summary>La hora de llegada de las aves decide el primer día con registro del lote (≥13:00 ⇒ día siguiente).</summary>
    bool     PrimerRegistroSegunHoraLlegada = false,
    // Días ANTES del encasetamiento cuyo alimento ya cuenta como del lote en el saldo del reporte
    // diario de engorde. Rango 0-30 (clamp en el service vía VentanaAlimentoPrevioCalculos), default 10.
    int      DiasAlimentoPrevioEncaset = 10,
    /// <summary>Los lotes de engorde se programan (lote base obligatorio + gasto contra lote programado).</summary>
    bool     ProgramacionLotesEngorde = false,
    /// <summary>El nombre del lote lleva el sufijo de corrida desde la primera apertura ("96 - 1").</summary>
    bool     NombreLoteIncluyeCorrida = false,
    /// <summary>El inventario se ubica en silos/bodegas de la granja, no en el galpón.</summary>
    bool     ManejaInventarioPorSilo = false
);