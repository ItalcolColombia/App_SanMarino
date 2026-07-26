namespace ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

/// <summary>
/// Contratos del módulo "Reporte Técnico Semanal" (Sanmarino postura):
/// dos reportes (Levante 1-25 / Producción 25+) por lote base, con un tab por
/// sublote (galpón) + consolidado, todo comparado contra la guía genética.
/// Estructura espejo de los formatos Excel oficiales (Resumen Semanal Galpón
/// de Levante / Resumen Semanal de Producción).
/// </summary>
public sealed record ReporteTecnicoSemanalRequest(
    int LotePosturaBaseId,
    int? SemanaDesde = null,
    int? SemanaHasta = null);

// ─────────────────────────────────────────────────────────────────────────────
// Cabecera común del tab (hoja del Excel = un galpón; consolidado sin galpón).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReporteSemanalTabHeaderDto
{
    public int? LoteId { get; set; }
    public int? LotePosturaProduccionId { get; set; }
    public string LoteNombre { get; set; } = string.Empty;
    public bool EsConsolidado { get; set; }

    public int? GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? Municipio { get; set; }
    public string? NucleoId { get; set; }
    public string? NucleoNombre { get; set; }
    public string? GalponId { get; set; }
    public string? GalponNombre { get; set; }
    public string? Tecnico { get; set; }

    public string? Raza { get; set; }
    public int? AnioGuia { get; set; }
    public DateTime? FechaEncaset { get; set; }
    public DateTime? FechaInicioProduccion { get; set; }

    /// <summary>Base FIJA de aves hembras (fallback resuelto) usada para % del Excel.</summary>
    public double BaseHembras { get; set; }
    public double BaseMachos { get; set; }
    public double? PesoInicialHembras { get; set; }
    public int? MortCajasHembras { get; set; }
    public int? MortCajasMachos { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// LEVANTE — fila semanal (columnas del "Resumen Semanal Galpón de Levante").
// Bloque Hembras + bloque Machos. Guía = guia_genetica_sanmarino_colombia.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReporteSemanalLevanteSemanaDto
{
    public int Semana { get; set; }
    public DateTime? FechaFinSemana { get; set; }
    public int DiasConRegistro { get; set; }

    public double AvesHembrasFin { get; set; }
    public double AvesMachosFin { get; set; }
    /// <summary>Relación M:H % = aves machos fin / aves hembras fin * 100.</summary>
    public double? RelacionMachosHembrasPct { get; set; }

    // ── Hembras · retiro de aves ──
    public int MortalidadHembras { get; set; }
    public double? MortalidadHembrasPct { get; set; }
    public double? MortalidadHembrasAcumPct { get; set; }
    public int SeleccionHembras { get; set; }
    public double? SeleccionHembrasPct { get; set; }
    public double? SeleccionHembrasAcumPct { get; set; }
    /// <summary>M+D % guía semanal (mort_sem_h).</summary>
    public double? MortSelHembrasGuiaPct { get; set; }
    public int ErrorHembras { get; set; }
    public double? ErrorHembrasPct { get; set; }
    public double? ErrorHembrasAcumPct { get; set; }
    public double? RetiroAcumHembrasPct { get; set; }
    /// <summary>Guía retiro acumulado hembras (retiro_ac_h).</summary>
    public double? RetiroAcumHembrasGuiaPct { get; set; }

    // ── Hembras · alimento ──
    public double ConsumoKgHembras { get; set; }
    public double ConsumoKgHembrasAcum { get; set; }
    public double? GrAveDiaHembras { get; set; }
    public double? GrAveDiaHembrasGuia { get; set; }
    public double? IncrementoGrAveDiaHembras { get; set; }
    public double? IncrementoGrAveDiaHembrasGuia { get; set; }
    public double? ConsumoAcumGrAveHembras { get; set; }
    public double? ConsumoAcumGrAveHembrasGuia { get; set; }

    // ── Hembras · peso ──
    public double? PesoHembras { get; set; }
    public double? PesoHembrasGuia { get; set; }
    public double? GananciaHembras { get; set; }
    public double? DesviacionPesoHembrasPct { get; set; }

    // ── Uniformidad (lote) ──
    public double? UniformidadHembras { get; set; }
    public double? UniformidadGuia { get; set; }
    public double? CvHembras { get; set; }

    // ── Nutrición (hembras) ──
    public double? KcalAlimentoHembras { get; set; }
    public double? ProtAlimentoHembras { get; set; }
    public double? KcalAveAcumHembras { get; set; }
    public double? ProtAveAcumHembras { get; set; }

    // ── Machos · retiro de aves ──
    public int MortalidadMachos { get; set; }
    public double? MortalidadMachosPct { get; set; }
    public double? MortalidadMachosAcumPct { get; set; }
    public int SeleccionMachos { get; set; }
    public double? SeleccionMachosPct { get; set; }
    public double? SeleccionMachosAcumPct { get; set; }
    /// <summary>M % guía semanal machos (mort_sem_m).</summary>
    public double? MortSelMachosGuiaPct { get; set; }
    public int ErrorMachos { get; set; }
    public double? ErrorMachosPct { get; set; }
    public double? ErrorMachosAcumPct { get; set; }
    public double? RetiroAcumMachosPct { get; set; }
    public double? RetiroAcumMachosGuiaPct { get; set; }

    // ── Machos · alimento ──
    public double ConsumoKgMachos { get; set; }
    public double ConsumoKgMachosAcum { get; set; }
    public double? GrAveDiaMachos { get; set; }
    public double? GrAveDiaMachosGuia { get; set; }
    public double? IncrementoGrAveDiaMachos { get; set; }
    public double? IncrementoGrAveDiaMachosGuia { get; set; }
    public double? ConsumoAcumGrAveMachos { get; set; }
    public double? ConsumoAcumGrAveMachosGuia { get; set; }

    // ── Machos · peso ──
    public double? PesoMachos { get; set; }
    public double? PesoMachosGuia { get; set; }
    public double? GananciaMachos { get; set; }
    public double? DesviacionPesoMachosPct { get; set; }
}

public sealed class ReporteSemanalLevanteTabDto
{
    public ReporteSemanalTabHeaderDto Header { get; set; } = new();
    public List<ReporteSemanalLevanteSemanaDto> Semanas { get; set; } = new();
}

public sealed record ReporteTecnicoSemanalLevanteResponse(
    int LotePosturaBaseId,
    string LoteBaseNombre,
    string? Raza,
    int? AnioGuia,
    bool TieneGuia,
    List<ReporteSemanalLevanteTabDto> Tabs,
    ReporteSemanalLevanteTabDto? Consolidado);

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCCIÓN — fila semanal (columnas del "Resumen Semanal de Producción").
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReporteSemanalProduccionSemanaDto
{
    public int Semana { get; set; }
    public DateTime? FechaInicioSemana { get; set; }
    public DateTime? FechaFinSemana { get; set; }
    public int DiasConRegistro { get; set; }

    public int AvesHembrasFin { get; set; }
    public int AvesMachosFin { get; set; }
    /// <summary>Apareo M:H % = aves machos / aves hembras * 100.</summary>
    public double? ApareoPct { get; set; }
    public double? ApareoGuiaPct { get; set; }

    // ── Mortalidad - descarte Hembras ──
    public int MortalidadHembras { get; set; }
    public int SeleccionHembras { get; set; }
    public double? MortalidadHembrasPct { get; set; }
    public double? MortalidadHembrasGuiaPct { get; set; }
    /// <summary>% mortalidad acumulada GUÍA (suma corrida de mort_sem_h).</summary>
    public double? MortalidadHembrasAcumGuiaPct { get; set; }
    /// <summary>% M+D acumulado real hembras (mort+sel acum / aves iniciales).</summary>
    public double? MortSelHembrasAcumPct { get; set; }
    public double? RetiroAcumHembrasGuiaPct { get; set; }

    // ── Mortalidad - descarte Machos ──
    public int MortalidadMachos { get; set; }
    public int SeleccionMachos { get; set; }
    public double? MortalidadMachosPct { get; set; }
    public double? MortalidadMachosGuiaPct { get; set; }
    public double? MortSelMachosAcumPct { get; set; }
    public double? RetiroAcumMachosGuiaPct { get; set; }

    // ── Producción total de huevos ──
    public int HuevosTotales { get; set; }
    public long HuevosTotalesAcum { get; set; }
    public double? Htaa { get; set; }
    public double? HtaaGuia { get; set; }
    public double? PorcentajeProduccion { get; set; }
    public double? PorcentajeProduccionGuia { get; set; }

    // ── Huevos incubables ──
    public int HuevosIncubables { get; set; }
    public long HuevosIncubablesAcum { get; set; }
    public double? PorcentajeIncubables { get; set; }
    public double? PorcentajeIncubablesGuia { get; set; }
    public double? PorcentajeIncubablesAcum { get; set; }
    public double? PorcentajeIncubablesAcumGuia { get; set; }
    public double? Hiaa { get; set; }
    public double? HiaaGuia { get; set; }

    // ── Alimento Hembras ──
    public double ConsumoKgHembras { get; set; }
    public double ConsumoKgHembrasAcum { get; set; }
    public double? GrAveDiaHembras { get; set; }
    public double? GrAveDiaHembrasGuia { get; set; }
    public double? IncrementoGrAveDiaHembras { get; set; }

    // ── Alimento Machos ──
    public double ConsumoKgMachos { get; set; }
    public double ConsumoKgMachosAcum { get; set; }
    public double? GrAveDiaMachos { get; set; }
    public double? GrAveDiaMachosGuia { get; set; }

    // ── Conversión ──
    public double? ConversionGrHuevoInc { get; set; }
    public double? ConversionGrHuevoIncGuia { get; set; }

    // ── Peso huevo / masa huevo ──
    public double? PesoHuevo { get; set; }
    public double? PesoHuevoGuia { get; set; }
    public double? MasaHuevoLote { get; set; }
    public double? MasaHuevoGuia { get; set; }

    // ── Peso corporal (gramos) ──
    public double? PesoHembras { get; set; }
    public double? PesoHembrasGuia { get; set; }
    public double? DesviacionPesoHembrasPct { get; set; }
    public double? PesoMachos { get; set; }
    public double? PesoMachosGuia { get; set; }
    public double? DesviacionPesoMachosPct { get; set; }

    public double? Uniformidad { get; set; }
    public double? UniformidadGuia { get; set; }
    public double? CoeficienteVariacion { get; set; }

    // ── Pollitos / nacimiento (v1: solo guía; reales en fase 2) ──
    public double? NacimientoGuiaPct { get; set; }
    public double? PollitosAveGuia { get; set; }
}

public sealed class ReporteSemanalProduccionTabDto
{
    public ReporteSemanalTabHeaderDto Header { get; set; } = new();
    public List<ReporteSemanalProduccionSemanaDto> Semanas { get; set; } = new();
}

public sealed record ReporteTecnicoSemanalProduccionResponse(
    int LotePosturaBaseId,
    string LoteBaseNombre,
    string? Raza,
    int? AnioGuia,
    bool TieneGuia,
    List<ReporteSemanalProduccionTabDto> Tabs,
    ReporteSemanalProduccionTabDto? Consolidado);
