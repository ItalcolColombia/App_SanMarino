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
    bool?    PrimerRegistroSegunHoraLlegada = null,
    // Días ANTES del encasetamiento cuyo alimento ya cuenta como del lote en el saldo del reporte
    // diario de engorde. `null` = el cliente no lo mandó ⇒ se conserva el valor actual (mismo
    // criterio que los flags de arriba). Rango 0-30, clamp vía VentanaAlimentoPrevioCalculos.
    int?     DiasAlimentoPrevioEncaset = null,
    /// <summary>Los lotes de engorde se programan (lote base obligatorio + gasto contra lote programado).</summary>
    bool?    ProgramacionLotesEngorde = null,
    /// <summary>El nombre del lote lleva el sufijo de corrida desde la primera apertura ("96 - 1").</summary>
    bool?    NombreLoteIncluyeCorrida = null,
    /// <summary>El inventario se ubica en silos/bodegas de la granja, no en el galpón.</summary>
    bool?    ManejaInventarioPorSilo = null,
    /// <summary>Los reportes leen el alimento del inventario unificado, no de la tabla vieja.</summary>
    bool?    ReportesAlimentoDesdeInventarioUnificado = null,
    /// <summary>Los seguimientos diarios exigen doble validación (separan al guardar, descuentan al validar).</summary>
    bool?    RequiereValidacionSeguimientoDiario = null,
    bool?    SeguimientoEngordeMixto = null,
    bool?    ReporteCostosAlimentoDesdeFuentesReales = null,
    /// <summary>El seguimiento diario no captura consumo de alimento de machos (producción ni levante).</summary>
    bool?    ConsumoAlimentoSoloHembras = null,
    /// <summary>Oculta la columna Machos en mortalidad/selección/peso/uniformidad/traslados/ventas y retira error de sexaje del registro diario.</summary>
    bool?    OcultaMachosEnPostura = null,
    /// <summary>Última semana en la que el huevo de primera postura sigue habilitado. Null = la empresa no usa el concepto (omitir conserva el valor actual; para borrarlo explícitamente no hay bandera separada, hoy no hace falta).</summary>
    int?     HuevoPrimeraPosturaHastaSemana = null,
    /// <summary>La etapa del ciclo de vida del ave (alistamiento/levante/levante en producción/postura) se calcula por semana y por raza.</summary>
    bool?    SemanasCicloPosturaPorRaza = null,
    /// <summary>El catálogo de ítems de inventario sólo ofrece Alimento y Aves.</summary>
    bool?    LimitaTiposInventarioAlimentoYAves = null,
    bool?    SeparaLotesPosturaPorEtapa         = null,
    /// <summary>La app móvil manda ítems reales de inventario. Kill switch de F5.</summary>
    bool?    DescuentaInventarioDesdeMovil      = null
);
