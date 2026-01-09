// src/ZooSanMarino.Domain/Entities/ReporteTecnicoGuia.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Entidad para almacenar valores de guía (GUIA) manuales para el reporte técnico
/// Estos valores se usan para comparación con los valores reales calculados
/// </summary>
public class ReporteTecnicoGuia : AuditableEntity
{
    public int Id { get; set; }
    
    // Identificación del lote y semana
    public int LoteId { get; set; }
    public int Semana { get; set; } // Semana de levante (1-25)
    
    // ========== VALORES GUÍA HEMBRAS ==========
    public double? PorcMortHGUIA { get; set; }
    public double? RetiroHGUIA { get; set; }
    public double? ConsAcGrHGUIA { get; set; }
    public double? GrAveDiaGUIAH { get; set; }
    public double? IncrConsHGUIA { get; set; }
    public double? PesoHGUIA { get; set; }
    public double? UnifHGUIA { get; set; }
    
    // ========== VALORES GUÍA MACHOS ==========
    public double? PorcMortMGUIA { get; set; }
    public double? RetiroMGUIA { get; set; }
    public double? ConsAcGrMGUIA { get; set; }
    public double? GrAveDiaMGUIA { get; set; }
    public double? IncrConsMGUIA { get; set; }
    public double? PesoMGUIA { get; set; }
    public double? UnifMGUIA { get; set; }
    
    // ========== VALORES GUÍA NUTRICIONALES HEMBRAS ==========
    public string? AlimHGUIA { get; set; }
    public double? KcalSemHGUIA { get; set; }
    public double? ProtSemHGUIA { get; set; }
    
    // ========== VALORES GUÍA NUTRICIONALES MACHOS ==========
    public string? AlimMGUIA { get; set; }
    public double? KcalSemMGUIA { get; set; }
    public double? ProtSemMGUIA { get; set; }
    
    // ========== ERROR SEXAJE ACUMULADO ==========
    public int? ErrSexAcH { get; set; }
    public int? ErrSexAcM { get; set; }
    
    // ========== DATOS MANUALES ADICIONALES ==========
    public string? CodGuia { get; set; }
    public string? IdLoteRAP { get; set; }
    public int? Traslado { get; set; }
    public string? NucleoL { get; set; }
    public int? Anon { get; set; }
    
    // Navegación
    public Lote Lote { get; set; } = null!;
}

