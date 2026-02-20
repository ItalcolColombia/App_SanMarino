// src/ZooSanMarino.Domain/Entities/LoteAveEngorde.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Lote de ave de engorde. Misma estructura de datos que Lote pero en tabla independiente lote_ave_engorde.
/// </summary>
public class LoteAveEngorde : AuditableEntity
{
    public int? LoteAveEngordeId { get; set; }
    public string LoteNombre { get; set; } = null!;
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }

    public string? Regional { get; set; }
    public DateTime? FechaEncaset { get; set; }
    public int? HembrasL { get; set; }
    public int? MachosL { get; set; }

    public double? PesoInicialH { get; set; }
    public double? PesoInicialM { get; set; }
    public double? UnifH { get; set; }
    public double? UnifM { get; set; }

    public int? MortCajaH { get; set; }
    public int? MortCajaM { get; set; }
    public string? Raza { get; set; }
    public int? AnoTablaGenetica { get; set; }
    public string? Linea { get; set; }
    public string? TipoLinea { get; set; }
    public string? CodigoGuiaGenetica { get; set; }
    public int? LineaGeneticaId { get; set; }
    public string? Tecnico { get; set; }

    public int? Mixtas { get; set; }
    public double? PesoMixto { get; set; }
    public int? AvesEncasetadas { get; set; }
    public int? EdadInicial { get; set; }
    public string? LoteErp { get; set; }
    public string? EstadoTraslado { get; set; }

    public int? PaisId { get; set; }
    public string? PaisNombre { get; set; }
    public string? EmpresaNombre { get; set; }

    public Farm Farm { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
}
