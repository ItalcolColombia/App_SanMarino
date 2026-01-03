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


