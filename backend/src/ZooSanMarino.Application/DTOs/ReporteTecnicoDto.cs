// src/ZooSanMarino.Application/DTOs/ReporteTecnicoDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para datos diarios de reporte técnico
/// </summary>
public class ReporteTecnicoDiarioDto
{
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    public int NumeroAves { get; set; }
    
    // Mortalidad
    public int MortalidadTotal { get; set; }
    public decimal MortalidadPorcentajeDiario { get; set; }
    public decimal MortalidadPorcentajeAcumulado { get; set; }
    
    // Errores de sexaje
    public int ErrorSexajeNumero { get; set; }
    public decimal ErrorSexajePorcentaje { get; set; }
    public decimal ErrorSexajePorcentajeAcumulado { get; set; }
    
    // Descarte (selección normal - valores positivos)
    public int DescarteNumero { get; set; }
    public decimal DescartePorcentajeDiario { get; set; }
    public decimal DescartePorcentajeAcumulado { get; set; }
    
    // Traslados (valores negativos de SelH/SelM en valor absoluto)
    public int TrasladosNumero { get; set; }
    
    // Consumo de alimento
    public decimal ConsumoBultos { get; set; }
    public decimal ConsumoKilos { get; set; }
    public decimal ConsumoKilosAcumulado { get; set; }
    public decimal ConsumoGramosPorAve { get; set; }
    
    // Ingresos de alimentos
    public decimal IngresosAlimentoKilos { get; set; }
    
    // Traslados de alimento
    public decimal TrasladosAlimentoKilos { get; set; }
    
    // Peso
    public decimal? PesoActual { get; set; }
    public decimal? Uniformidad { get; set; }
    public decimal? GananciaPeso { get; set; }
    public decimal? CoeficienteVariacion { get; set; }
    
    // Selección ventas
    public int SeleccionVentasNumero { get; set; }
    public decimal SeleccionVentasPorcentaje { get; set; }
}

/// <summary>
/// DTO para datos semanales consolidados
/// </summary>
public class ReporteTecnicoSemanalDto
{
    public int Semana { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public int EdadInicioSemanas { get; set; }
    public int EdadFinSemanas { get; set; }
    
    // Promedios y totales semanales
    public int AvesInicioSemana { get; set; }
    public int AvesFinSemana { get; set; }
    public int MortalidadTotalSemana { get; set; }
    public decimal MortalidadPorcentajeSemana { get; set; }
    public decimal ConsumoKilosSemana { get; set; }
    public decimal ConsumoGramosPorAveSemana { get; set; }
    public decimal? PesoPromedioSemana { get; set; }
    public decimal? UniformidadPromedioSemana { get; set; }
    public int SeleccionVentasSemana { get; set; }
    public int DescarteTotalSemana { get; set; } // Total descarte/selección normal de la semana (valores positivos de SelH y SelM)
    public int TrasladosTotalSemana { get; set; } // Total traslados de la semana (valores negativos de SelH y SelM en valor absoluto)
    public int ErrorSexajeTotalSemana { get; set; } // Total error de sexaje de la semana (puede aumentar aves)
    public decimal IngresosAlimentoKilosSemana { get; set; }
    public decimal TrasladosAlimentoKilosSemana { get; set; }
    
    // Detalle diario de la semana
    public List<ReporteTecnicoDiarioDto> DetalleDiario { get; set; } = new();
}

/// <summary>
/// DTO para información del lote/sublote
/// </summary>
public class ReporteTecnicoLoteInfoDto
{
    public int LoteId { get; set; }
    public string LoteNombre { get; set; } = string.Empty;
    public string? Sublote { get; set; } // "A", "B", etc.
    public string? Raza { get; set; }
    public string? Linea { get; set; }
    public string? Etapa { get; set; } // "LEVANTE", "PRODUCCION"
    public DateTime? FechaEncaset { get; set; }
    public int? NumeroHembras { get; set; }
    public int? NumeroMachos { get; set; } // Número inicial de machos
    public int? Galpon { get; set; }
    public string? Tecnico { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoNombre { get; set; }
}

/// <summary>
/// DTO completo del reporte técnico
/// </summary>
public class ReporteTecnicoCompletoDto
{
    public ReporteTecnicoLoteInfoDto InformacionLote { get; set; } = new();
    public List<ReporteTecnicoDiarioDto> DatosDiarios { get; set; } = new();
    public List<ReporteTecnicoSemanalDto> DatosSemanales { get; set; } = new();
    public bool EsConsolidado { get; set; }
    public List<string> SublotesIncluidos { get; set; } = new();
}

/// <summary>
/// DTO para solicitud de generación de reporte
/// </summary>
public class GenerarReporteTecnicoRequestDto
{
    public int? LoteId { get; set; }
    public string? LoteNombre { get; set; } // Para buscar por nombre (ej: "K326")
    public string? Sublote { get; set; } // "A", "B", null para consolidado
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public bool IncluirSemanales { get; set; } = true;
    public bool ConsolidarSublotes { get; set; } = false; // Si true, consolida todos los sublotes del lote
}

// ========== DTOs para Reporte Técnico Diario Separado por Sexo ==========

/// <summary>
/// DTO para datos diarios de reporte técnico específico de MACHOS
/// </summary>
public class ReporteTecnicoDiarioMachosDto
{
    // ========== IDENTIFICACIÓN ==========
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    
    // ========== SALDO ACTUAL ==========
    public int SaldoMachos { get; set; } // Saldo actual de machos
    
    // ========== MORTALIDAD ==========
    public int MortalidadMachos { get; set; } // Mortalidad diaria
    public int MortalidadMachosAcumulada { get; set; } // Mortalidad acumulada desde inicio
    public decimal MortalidadMachosPorcentajeDiario { get; set; } // % sobre aves antes de mortalidad
    public decimal MortalidadMachosPorcentajeAcumulado { get; set; } // % sobre aves iniciales
    
    // ========== SELECCIÓN/RETIRO (Valores Positivos) ==========
    public int SeleccionMachos { get; set; } // Solo valores positivos de SelM
    public int SeleccionMachosAcumulada { get; set; }
    public decimal SeleccionMachosPorcentajeDiario { get; set; }
    public decimal SeleccionMachosPorcentajeAcumulado { get; set; }
    
    // ========== TRASLADOS (Valores Negativos en Valor Absoluto) ==========
    public int TrasladosMachos { get; set; } // Valores negativos de SelM en valor absoluto
    public int TrasladosMachosAcumulados { get; set; }
    
    // ========== ERROR DE SEXAJE ==========
    public int ErrorSexajeMachos { get; set; } // Error sexaje diario
    public int ErrorSexajeMachosAcumulado { get; set; } // Error sexaje acumulado
    public decimal ErrorSexajeMachosPorcentajeDiario { get; set; }
    public decimal ErrorSexajeMachosPorcentajeAcumulado { get; set; }
    
    // ========== DESCARTE (Selección + Error Sexaje) ==========
    public int DescarteMachos { get; set; } // Selección + Error Sexaje
    public int DescarteMachosAcumulado { get; set; }
    public decimal DescarteMachosPorcentajeDiario { get; set; }
    public decimal DescarteMachosPorcentajeAcumulado { get; set; }
    
    // ========== CONSUMO DE ALIMENTO ==========
    public decimal ConsumoKgMachos { get; set; } // Consumo diario en kg
    public decimal ConsumoKgMachosAcumulado { get; set; } // Consumo acumulado en kg
    public decimal ConsumoGramosPorAveMachos { get; set; } // Gramos por ave por día
    
    // ========== PESO Y UNIFORMIDAD ==========
    public decimal? PesoPromedioMachos { get; set; } // Peso promedio en kg
    public decimal? UniformidadMachos { get; set; } // Uniformidad (%)
    public decimal? CoeficienteVariacionMachos { get; set; } // Coeficiente de variación (%)
    public decimal? GananciaPesoMachos { get; set; } // Ganancia de peso vs día anterior
    
    // ========== VALORES NUTRICIONALES ==========
    public double? KcalAlMachos { get; set; } // Kcal por kg de alimento
    public double? ProtAlMachos { get; set; } // % Proteína en alimento
    public double? KcalAveMachos { get; set; } // Kcal por ave por día
    public double? ProtAveMachos { get; set; } // Proteína por ave por día
    
    // ========== INGRESOS Y TRASLADOS DE ALIMENTO ==========
    public decimal IngresosAlimentoKilos { get; set; } // Ingresos de alimento del día
    public decimal TrasladosAlimentoKilos { get; set; } // Traslados de alimento del día
    
    // ========== OBSERVACIONES ==========
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO para datos diarios de reporte técnico específico de HEMBRAS
/// </summary>
public class ReporteTecnicoDiarioHembrasDto
{
    // ========== IDENTIFICACIÓN ==========
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    
    // ========== SALDO ACTUAL ==========
    public int SaldoHembras { get; set; } // Saldo actual de hembras
    
    // ========== MORTALIDAD ==========
    public int MortalidadHembras { get; set; }
    public int MortalidadHembrasAcumulada { get; set; }
    public decimal MortalidadHembrasPorcentajeDiario { get; set; }
    public decimal MortalidadHembrasPorcentajeAcumulado { get; set; }
    
    // ========== SELECCIÓN/RETIRO ==========
    public int SeleccionHembras { get; set; } // Solo valores positivos de SelH
    public int SeleccionHembrasAcumulada { get; set; }
    public decimal SeleccionHembrasPorcentajeDiario { get; set; }
    public decimal SeleccionHembrasPorcentajeAcumulado { get; set; }
    
    // ========== TRASLADOS ==========
    public int TrasladosHembras { get; set; } // Valores negativos de SelH en valor absoluto
    public int TrasladosHembrasAcumulados { get; set; }
    
    // ========== ERROR DE SEXAJE ==========
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeHembrasAcumulado { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeDiario { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeAcumulado { get; set; }
    
    // ========== DESCARTE (Selección + Error Sexaje) ==========
    public int DescarteHembras { get; set; } // Selección + Error Sexaje
    public int DescarteHembrasAcumulado { get; set; }
    public decimal DescarteHembrasPorcentajeDiario { get; set; }
    public decimal DescarteHembrasPorcentajeAcumulado { get; set; }
    
    // ========== CONSUMO DE ALIMENTO ==========
    public decimal ConsumoKgHembras { get; set; }
    public decimal ConsumoKgHembrasAcumulado { get; set; }
    public decimal ConsumoGramosPorAveHembras { get; set; }
    
    // ========== PESO Y UNIFORMIDAD ==========
    public decimal? PesoPromedioHembras { get; set; }
    public decimal? UniformidadHembras { get; set; }
    public decimal? CoeficienteVariacionHembras { get; set; }
    public decimal? GananciaPesoHembras { get; set; }
    
    // ========== VALORES NUTRICIONALES ==========
    public double? KcalAlHembras { get; set; }
    public double? ProtAlHembras { get; set; }
    public double? KcalAveHembras { get; set; } // Puede venir del seguimiento o calcularse
    public double? ProtAveHembras { get; set; } // Puede venir del seguimiento o calcularse
    
    // ========== INGRESOS Y TRASLADOS DE ALIMENTO ==========
    public decimal IngresosAlimentoKilos { get; set; } // Ingresos de alimento del día
    public decimal TrasladosAlimentoKilos { get; set; } // Traslados de alimento del día
    
    // ========== OBSERVACIONES ==========
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO completo para reporte técnico de Levante con estructura de tabs
/// Incluye datos diarios separados (machos y hembras) y datos semanales completos
/// </summary>
public class ReporteTecnicoLevanteConTabsDto
{
    public ReporteTecnicoLoteInfoDto InformacionLote { get; set; } = new();
    
    // Tab 1: Diario Machos
    public List<ReporteTecnicoDiarioMachosDto> DatosDiariosMachos { get; set; } = new();
    
    // Tab 2: Diario Hembras
    public List<ReporteTecnicoDiarioHembrasDto> DatosDiariosHembras { get; set; } = new();
    
    // Tab 3: Semanal (reutiliza DTO existente)
    public List<ReporteTecnicoLevanteSemanalDto> DatosSemanales { get; set; } = new();
    
    public bool EsConsolidado { get; set; }
    public List<string> SublotesIncluidos { get; set; } = new();
}


