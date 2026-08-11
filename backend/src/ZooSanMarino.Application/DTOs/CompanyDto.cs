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
    bool PermiteTrasladoAvesCrossEtapa = false,
    // ¿La empresa captura la clasificación de huevos en el seguimiento diario de LEVANTE desde la
    // semana 14 (con arrastre del acumulado a producción al liquidar)?
    bool CapturaHuevosEnLevante = false,
    // ¿El peso báscula de la venta de pollo engorde llega al día siguiente? Con el flag activo el
    // front deja de exigir peso bruto/tara al registrar la venta y pide el peso al confirmarla.
    bool VentaEngordePesoDiferido = false,
    /// <summary>La hora de llegada de las aves decide el primer día con registro del lote (≥13:00 ⇒ día siguiente).</summary>
    bool PrimerRegistroSegunHoraLlegada = false,
    // Días ANTES del encasetamiento cuyo alimento ya cuenta como del lote en el saldo del reporte
    // diario de engorde (ventana de "ingreso inicial del ciclo"). Rango 0-30, default 10.
    int DiasAlimentoPrevioEncaset = 10,
    /// <summary>
    /// Los lotes de engorde se PROGRAMAN: el lote base (asignado por granja) es obligatorio y da el
    /// nombre del lote, y un gasto de inventario puede cargarse contra un lote programado aún sin encasetar.
    /// </summary>
    bool ProgramacionLotesEngorde = false,
    /// <summary>El nombre del lote lleva el sufijo de corrida desde la primera apertura ("96 - 1").
    /// Con <c>false</c> el nombre es el del lote base ("2603") y el sufijo aparece desde la segunda.</summary>
    bool NombreLoteIncluyeCorrida = false
);