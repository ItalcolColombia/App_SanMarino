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
    bool NombreLoteIncluyeCorrida = false,
    /// <summary>
    /// El inventario se ubica en SILOS y BODEGAS de la granja, no en el galpón. El front lo usa para
    /// mostrar el selector de silo en ingreso, traslado y seguimiento diario, y para habilitar las
    /// pantallas de asignación de silos (granja, galpón y lote).
    /// </summary>
    bool ManejaInventarioPorSilo = false,
    /// <summary>
    /// Los reportes Contable y Técnico leen el alimento del inventario unificado
    /// (<c>inventario_gestion_movimiento</c>) en vez de la tabla vieja.
    /// </summary>
    bool ReportesAlimentoDesdeInventarioUnificado = false,
    /// <summary>
    /// Los seguimientos diarios exigen doble validación: al guardar se separa (reserva) el alimento y
    /// las aves, y el descuento real ocurre al validar. El front lo usa para mostrar la columna
    /// Estado, el botón Validar, el semáforo de retraso y el modal de pendientes.
    /// </summary>
    bool RequiereValidacionSeguimientoDiario = false,
    /// <summary>Pollo engorde en modo MIXTO: una sola columna en vez de hembras/machos.</summary>
    bool SeguimientoEngordeMixto = false,
    /// <summary>El reporte de costos toma el alimento de las fuentes reales de inventario.</summary>
    bool ReporteCostosAlimentoDesdeFuentesReales = false,
    /// <summary>El seguimiento diario no captura consumo de alimento de machos (producción ni levante).</summary>
    bool ConsumoAlimentoSoloHembras = false,
    /// <summary>Oculta la columna Machos en mortalidad/selección/peso/uniformidad/traslados/ventas y retira error de sexaje del registro diario.</summary>
    bool OcultaMachosEnPostura = false,
    /// <summary>Última semana en la que el huevo de primera postura sigue habilitado. Null = la empresa no usa el concepto.</summary>
    int? HuevoPrimeraPosturaHastaSemana = null,
    /// <summary>La etapa del ciclo de vida del ave (alistamiento/levante/levante en producción/postura) se calcula por semana y por raza.</summary>
    bool SemanasCicloPosturaPorRaza = false,
    /// <summary>El catálogo de ítems de inventario sólo ofrece Alimento y Aves.</summary>
    bool LimitaTiposInventarioAlimentoYAves = false,
    /// <summary>El listado de lotes de postura se separa en pestanas por etapa (Levante / Produccion).</summary>
    bool SeparaLotesPosturaPorEtapa = false,
    /// <summary>La app móvil manda ítems de inventario reales (no el escalar de hoy) y el seguimiento
    /// diario descuenta stock igual que el front web. Kill switch de F5 — no encender para empresas
    /// con <c>ManejaInventarioPorSilo</c> hasta que exista el selector de silo en la app.</summary>
    bool DescuentaInventarioDesdeMovil = false,
    /// <summary>
    /// Qué modelo de guía genética usa la empresa: <c>"sanmarino"</c> (default) = tabla ancha
    /// compartida; <c>"reducida"</c> = tabla plana de 3 métricas. El front lo lee desde
    /// <c>ActiveCompanyConfigService</c> para decidir qué pantalla de guía ofrecer.
    /// Valores válidos y resolución: <c>GuiaGeneticaPerfilCalculos</c>.
    /// </summary>
    string GuiaGeneticaPerfil = "sanmarino",
    /// <summary>
    /// Semana de vida desde la que arrancan los indicadores de producción. <c>25</c> (default) = el
    /// valor que estuvo hardcodeado en <c>fn_indicadores_produccion_postura</c>. Santa Reyes usa
    /// <c>18</c> (postura comercial: pone mucho antes que una reproductora).
    /// </summary>
    int SemanaInicioIndicadoresProduccion = 25
);