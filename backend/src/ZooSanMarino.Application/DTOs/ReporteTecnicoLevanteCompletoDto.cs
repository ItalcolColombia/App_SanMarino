// src/ZooSanMarino.Application/DTOs/ReporteTecnicoLevanteCompletoDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO completo para reporte técnico de Levante con estructura Excel
/// Incluye todos los campos manuales, calculados y de guía
/// </summary>
public class ReporteTecnicoLevanteCompletoDto
{
    public ReporteTecnicoLoteInfoDto InformacionLote { get; set; } = new();
    public List<ReporteTecnicoLevanteSemanalDto> DatosSemanales { get; set; } = new();
    public bool EsConsolidado { get; set; }
    public List<string> SublotesIncluidos { get; set; } = new();
}

/// <summary>
/// DTO para datos semanales del reporte técnico de Levante (estructura Excel)
/// </summary>
public class ReporteTecnicoLevanteSemanalDto
{
    // ========== IDENTIFICACIÓN Y DATOS MANUALES ==========
    public string? CodGuia { get; set; } // Manual
    public string? IdLoteRAP { get; set; } // Manual
    public string? Regional { get; set; } // Manual
    public string? Granja { get; set; } // Manual
    public string? Lote { get; set; } // Manual
    public string? Raza { get; set; } // Manual
    public int? AnoG { get; set; } // Manual
    public int HembraIni { get; set; } // Desde lote
    public int MachoIni { get; set; } // Desde lote
    public int? Traslado { get; set; } // Manual
    public string? NucleoL { get; set; } // Manual
    public int? Anon { get; set; } // Manual
    public int Edad { get; set; } // Calculado: edad en días
    public DateTime Fecha { get; set; } // Fecha de la semana
    public int SemAno { get; set; } // Semana del año
    public int Semana { get; set; } // Semana de levante (1-25)
    
    // ========== DATOS DE HEMBRAS ==========
    public int Hembra { get; set; } // Saldo actual de hembras
    public int MortH { get; set; } // Mortalidad hembras semana
    public int SelH { get; set; } // Selección hembras semana
    public int ErrorH { get; set; } // Error sexaje hembras semana
    public double ConsKgH { get; set; } // Consumo kg hembras semana
    public double? PesoH { get; set; } // Peso promedio hembras
    public double? UniformH { get; set; } // Uniformidad hembras
    public double? CvH { get; set; } // %CV hembras
    public double? KcalAlH { get; set; } // Kcal/kg alimento hembras
    public double? ProtAlH { get; set; } // %Proteína alimento hembras
    
    // ========== DATOS DE MACHOS ==========
    public int SaldoMacho { get; set; } // Saldo actual de machos
    public int MortM { get; set; } // Mortalidad machos semana
    public int SelM { get; set; } // Selección machos semana
    public int ErrorM { get; set; } // Error sexaje machos semana
    public double ConsKgM { get; set; } // Consumo kg machos semana
    public double? PesoM { get; set; } // Peso promedio machos
    public double? UniformM { get; set; } // Uniformidad machos
    public double? CvM { get; set; } // %CV machos
    public double? KcalAlM { get; set; } // Kcal/kg alimento machos
    public double? ProtAlM { get; set; } // %Proteína alimento machos
    
    // ========== CÁLCULOS DE EFICIENCIA Y RENDIMIENTO ==========
    public double? KcalAveH { get; set; } // =SI(Hembra>0; (KcalAlH*ConsKgH)/(Hembra); 0)
    public double? ProtAveH { get; set; } // =SI(Hembra>0; (%ProtAlH*ConsKgH)/(Hembra); 0)
    public double? KcalAveM { get; set; } // =SI(SaldoMacho>0; (KcalAlM*ConsKgM)/(SaldoMacho); 0)
    public double? ProtAveM { get; set; } // =SI(SaldoMacho>0; (%ProtAlM*ConsKgM)/(SaldoMacho); 0)
    
    public double? RelMH { get; set; } // %RelM/H =SI(Hembra>0; (SaldoMacho/Hembra*100); "")
    public double? PorcMortH { get; set; } // %MortH = (MortH/HEMBRAINI)*100
    public double? PorcMortHGUIA { get; set; } // Manual
    public double? DifMortH { get; set; } // =%MortH-%MortHGUIA
    public int? ACMortH { get; set; } // Acumulado de MortH
    
    public double? PorcSelH { get; set; } // %SelH = (SelH/HEMBRAINI)*100
    public int? ACSelH { get; set; } // Acumulado de SelH
    public double? PorcErrH { get; set; } // %ErrH = (ErrorH/HEMBRAINI)*100
    public int? ACErrH { get; set; } // Acumulado de ErrorH
    
    public int? MSEH { get; set; } // M+S+EH = MortH+SelH+ErrorH
    public int? RetAcH { get; set; } // RetAcH = ACMortH+ACSelH+ACErrH
    public double? PorcRetiroH { get; set; } // %RetiroH = (RetAcH/HEMBRAINI)*100
    public double? RetiroHGUIA { get; set; } // Manual
    
    public double? AcConsH { get; set; } // Acumulado de ConsKgH
    public double? ConsAcGrH { get; set; } // = (AcConsH*1000)/HEMBRAINI
    public double? ConsAcGrHGUIA { get; set; } // Manual
    public double? GrAveDiaH { get; set; } // =SI(Hembra>0; (ConsKgH*1000)/Hembra/7; 0)
    public double? GrAveDiaGUIAH { get; set; } // Manual
    public double? IncrConsH { get; set; } // Diferencia semana anterior
    public double? IncrConsHGUIA { get; set; } // Manual
    public double? PorcDifConsH { get; set; } // =SI(ConsAcGrHGUIA>0; (ConsAcGrH-ConsAcGrHGUIA)/(ConsAcGrHGUIA*100); 0)
    
    public double? PesoHGUIA { get; set; } // Manual
    public double? PorcDifPesoH { get; set; } // =SI(PesoHGUIA>0; (PesoH-PesoHGUIA)/(PesoHGUIA*100); 0)
    public double? UnifHGUIA { get; set; } // Manual
    
    public double? PorcMortM { get; set; } // %MortM = (MortM/MACHOINI)*100
    public double? PorcMortMGUIA { get; set; } // Manual
    public double? DifMortM { get; set; } // =%MortM-%MortMGUIA
    public int? ACMortM { get; set; } // Acumulado de MortM
    
    public double? PorcSelM { get; set; } // %SelM = (SelM/MACHOINI)*100
    public int? ACSelM { get; set; } // Acumulado de SelM
    public double? PorcErrM { get; set; } // %ErrM = (ErrorM/MACHOINI)*100
    public int? ACErrM { get; set; } // Acumulado de ErrorM
    
    public int? MSEM { get; set; } // M+S+EM = MortM+SelM+ErrorM
    public int? RetAcM { get; set; } // RetAcM = ACMortM+ACSelM+ACErrM
    public double? PorcRetAcM { get; set; } // %RetAcM = (RetAcM/MACHOINI)*100
    public double? RetiroMGUIA { get; set; } // Manual
    
    public double? AcConsM { get; set; } // Acumulado de ConsKgM
    public double? ConsAcGrM { get; set; } // = (AcConsM*1000)/MACHOINI
    public double? ConsAcGrMGUIA { get; set; } // Manual
    public double? GrAveDiaM { get; set; } // =SI(SaldoMacho>0; (ConsKgM*1000)/SaldoMacho/7; 0)
    public double? GrAveDiaMGUIA { get; set; } // Manual
    public double? IncrConsM { get; set; } // Diferencia semana anterior
    public double? IncrConsMGUIA { get; set; } // Manual
    public double? DifConsM { get; set; } // =ConsAcGrM-ConsAcGrMGUIA
    
    public double? PesoMGUIA { get; set; } // Manual
    public double? PorcDifPesoM { get; set; } // =SI(PesoMGUIA>0; (PesoM-PesoMGUIA)/(PesoMGUIA*100); 0)
    public double? UnifMGUIA { get; set; } // Manual
    
    public int? ErrSexAcH { get; set; } // Manual - Error sexaje acumulado hembras
    public double? PorcErrSxAcH { get; set; } // = (ErrSexAcH/HEMBRAINI)*100
    public int? ErrSexAcM { get; set; } // Manual - Error sexaje acumulado machos
    public double? PorcErrSxAcM { get; set; } // = (ErrSexAcM/MACHOINI)*100
    
    public double? DifConsAcH { get; set; } // =AcConsH-ConsAcGrHGUIA
    public double? DifConsAcM { get; set; } // =AcConsM-ConsAcGrMGUIA
    
    // ========== DATOS NUTRICIONALES Y GUÍA ==========
    public string? AlimHGUIA { get; set; } // Manual
    public double? KcalSemH { get; set; } // =KcalAlH*ConsKgH
    public double? KcalSemAcH { get; set; } // Acumulado de KcalSemH
    public double? KcalSemHGUIA { get; set; } // Manual
    public double? KcalSemAcHGUIA { get; set; } // Acumulado de KcalSemHGUIA
    public double? ProtSemH { get; set; } // =(%ProtAlH/100)*ConsKgH
    public double? ProtSemAcH { get; set; } // Acumulado de ProtSemH
    public double? ProtSemHGUIA { get; set; } // Manual
    public double? ProtSemAcHGUIA { get; set; } // Acumulado de ProtSemAcHGUIA
    
    public string? AlimMGUIA { get; set; } // Manual
    public double? KcalSemM { get; set; } // =KcalAlM*ConsKgM
    public double? KcalSemAcM { get; set; } // Acumulado de KcalSemM
    public double? KcalSemMGUIA { get; set; } // Manual
    public double? KcalSemAcMGUIA { get; set; } // Acumulado de KcalSemMGUIA
    public double? ProtSemM { get; set; } // =(%ProtAlM/100)*ConsKgM
    public double? ProtSemAcM { get; set; } // Acumulado de ProtSemM
    public double? ProtSemMGUIA { get; set; } // Manual
    public double? ProtSemAcMGUIA { get; set; } // Acumulado de ProtSemAcMGUIA
    
    // ========== OBSERVACIONES ==========
    public string? Observaciones { get; set; } // Manual
}

