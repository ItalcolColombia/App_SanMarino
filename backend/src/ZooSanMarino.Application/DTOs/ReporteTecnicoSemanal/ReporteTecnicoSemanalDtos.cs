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

    // ── Nutrición SEMANAL por sexo (hoja «ALIMLev» del Informe RA Pesadas) ──
    // Energía/proteína consumidas POR AVE en la semana = gramos consumidos por
    // ave × energía (o proteína) del alimento. En hembras el alimento real se
    // captura (kcal_al_h / prot_al_h del seguimiento diario); en machos NO se
    // captura, así que se usa la energía/proteína NOMINAL del alimento que la
    // guía asigna a esa semana (kcal_m / prot_m). Documentado para que nadie
    // lea la desviación de machos como si incluyera cambios de formulación.
    /// <summary>Fase de alimento de la guía para la semana (INI / LEV / PP / F1…).</summary>
    public string? FaseAlimentoHembras { get; set; }
    public string? FaseAlimentoMachos { get; set; }
    public double? KcalSemanaHembras { get; set; }
    public double? KcalSemanaHembrasGuia { get; set; }
    public double? ProtSemanaHembras { get; set; }
    public double? ProtSemanaHembrasGuia { get; set; }
    public double? KcalSemanaMachos { get; set; }
    public double? KcalSemanaMachosGuia { get; set; }
    public double? ProtSemanaMachos { get; set; }
    public double? ProtSemanaMachosGuia { get; set; }
    public double? KcalAcumHembras { get; set; }
    public double? KcalAcumHembrasGuia { get; set; }
    public double? KcalAcumMachos { get; set; }
    public double? KcalAcumMachosGuia { get; set; }
    public double? ProtAcumHembras { get; set; }
    public double? ProtAcumHembrasGuia { get; set; }
    public double? ProtAcumMachos { get; set; }
    public double? ProtAcumMachosGuia { get; set; }

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
    /// <summary>Hoja «ALIMLev»: energía y proteína agrupadas por fase de alimento.</summary>
    public ReporteSemanalAlimentoPorFaseDto AlimentoPorFase { get; set; } = new();
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

    // ── Pollitos / nacimiento ──
    /// <summary>"HI Cargado" del Excel: huevos incubables enviados a planta/incubadora
    /// en la semana (traslado_huevos Completado con destino Planta, limpio + tratado).</summary>
    public int HuevosCargadosPlanta { get; set; }
    public long HuevosCargadosPlantaAcum { get; set; }
    /// <summary>% de los incubables producidos en la semana que se enviaron a planta.</summary>
    public double? PorcentajeCargaSobreIncubables { get; set; }
    /// <summary>Nacimientos y pollitos REALES no se capturan en el sistema (no hay retorno de
    /// incubadora en BD): solo se expone el valor de guía.</summary>
    public double? NacimientoGuiaPct { get; set; }
    public double? PollitosAveGuia { get; set; }

    // ── Venta de aves (columnas VentaH / VentaM del archivo) ──
    // Salidas registradas en el módulo de Movimientos de Aves con tipo «Venta».
    // NO salen del seguimiento diario: allí no existe el concepto de venta.
    public int VentaHembras { get; set; }
    public int VentaMachos { get; set; }

    // ── Clasificación de huevo (hoja «CLAS Huevo» del Informe RA Pesadas) ──
    // Conteos de la semana y su % sobre el huevo TOTAL de la semana.
    // ⚠️ El Excel trae UNA columna «Deforme Blanco»; la BD guarda huevo_deforme
    //    y huevo_blanco por separado ⇒ acá se SUMAN para casar con el archivo.
    public int HuevosLimpios { get; set; }
    public int HuevosTratados { get; set; }
    public int HuevosSucios { get; set; }
    public int HuevosDeformeBlanco { get; set; }
    public int HuevosDobleYema { get; set; }
    public int HuevosPiso { get; set; }
    public int HuevosPequenos { get; set; }
    public int HuevosRotos { get; set; }
    public int HuevosDesecho { get; set; }
    public int HuevosOtro { get; set; }
    public double? PctLimpio { get; set; }
    public double? PctTratado { get; set; }
    public double? PctSucio { get; set; }
    public double? PctDeformeBlanco { get; set; }
    public double? PctDobleYema { get; set; }
    public double? PctPiso { get; set; }
    public double? PctPequeno { get; set; }
    public double? PctRoto { get; set; }
    public double? PctDesecho { get; set; }
    public double? PctOtro { get; set; }
}

/// <summary>
/// Fila de la hoja «ALIMLev»: energía o proteína AGRUPADA por fase de alimento
/// (INI / LEV / PP / F1 en hembras; INI / LEV / M en machos), real vs guía.
/// La fase de cada semana la fija la guía genética (alim_h / alim_m).
/// </summary>
public sealed class ReporteSemanalAlimentoFaseDto
{
    public string Fase { get; set; } = string.Empty;
    public int Semanas { get; set; }
    public double? Real { get; set; }
    public double? Guia { get; set; }
    public double? Diferencia { get; set; }
    public double? DiferenciaPct { get; set; }
}

/// <summary>Las cuatro tablas de la hoja «ALIMLev» (energía y proteína × sexo).</summary>
public sealed class ReporteSemanalAlimentoPorFaseDto
{
    public List<ReporteSemanalAlimentoFaseDto> EnergiaHembras { get; set; } = new();
    public List<ReporteSemanalAlimentoFaseDto> EnergiaMachos { get; set; } = new();
    public List<ReporteSemanalAlimentoFaseDto> ProteinaHembras { get; set; } = new();
    public List<ReporteSemanalAlimentoFaseDto> ProteinaMachos { get; set; } = new();
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
