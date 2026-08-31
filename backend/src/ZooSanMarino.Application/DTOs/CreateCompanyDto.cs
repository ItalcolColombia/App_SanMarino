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
    bool     ManejaInventarioPorSilo = false,
    /// <summary>Los reportes leen el alimento del inventario unificado, no de la tabla vieja.</summary>
    bool     ReportesAlimentoDesdeInventarioUnificado = false,
    /// <summary>Los seguimientos diarios exigen doble validación (separan al guardar, descuentan al validar).</summary>
    bool     RequiereValidacionSeguimientoDiario = false,
    bool     SeguimientoEngordeMixto = false,
    bool     ReporteCostosAlimentoDesdeFuentesReales = false,
    /// <summary>El seguimiento diario no captura consumo de alimento de machos (producción ni levante).</summary>
    bool     ConsumoAlimentoSoloHembras = false,
    /// <summary>Oculta la columna Machos en mortalidad/selección/peso/uniformidad/traslados/ventas y retira error de sexaje del registro diario.</summary>
    bool     OcultaMachosEnPostura = false,
    /// <summary>Última semana en la que el huevo de primera postura sigue habilitado. Null = la empresa no usa el concepto.</summary>
    int?     HuevoPrimeraPosturaHastaSemana = null,
    /// <summary>La etapa del ciclo de vida del ave (alistamiento/levante/levante en producción/postura) se calcula por semana y por raza.</summary>
    bool     SemanasCicloPosturaPorRaza = false,
    /// <summary>El catálogo de ítems de inventario sólo ofrece Alimento y Aves.</summary>
    bool     LimitaTiposInventarioAlimentoYAves = false,
    bool     SeparaLotesPosturaPorEtapa         = false,
    /// <summary>La app móvil manda ítems reales de inventario. Kill switch de F5.</summary>
    bool     DescuentaInventarioDesdeMovil      = false,
    /// <summary>Perfil de guía genética: <c>"sanmarino"</c> (default) | <c>"reducida"</c>.
    /// <c>null</c>/vacío ⇒ default neutro. Un valor desconocido se rechaza (no cae al default).</summary>
    string?  GuiaGeneticaPerfil                 = null,
    /// <summary>Semana de vida desde la que arrancan los indicadores de producción.
    /// <c>null</c> ⇒ el DEFAULT 25 de la BD (comportamiento de siempre).</summary>
    int?     SemanaInicioIndicadoresProduccion  = null
);