// src/ZooSanMarino.Domain/Entities/LoteReproductoraAveEngorde.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de lote reproductora asociado a un Lote Aves de Engorde.
/// Permite crear varios lotes reproductora por lote ave engorde, distribuyendo las aves encasetadas.
/// </summary>
public class LoteReproductoraAveEngorde
{
    public int Id { get; set; }
    public int LoteAveEngordeId { get; set; }
    public string ReproductoraId { get; set; } = null!;

    public string NombreLote { get; set; } = null!;
    public DateTime? FechaEncasetamiento { get; set; }

    public int? M { get; set; }
    public int? H { get; set; }
    public int? AvesInicioHembras { get; set; }
    public int? AvesInicioMachos { get; set; }
    public int? Mixtas { get; set; }
    public int? MortCajaH { get; set; }
    public int? MortCajaM { get; set; }
    public int? UnifH { get; set; }
    public int? UnifM { get; set; }

    public decimal? PesoInicialM { get; set; }
    public decimal? PesoInicialH { get; set; }
    public decimal? PesoMixto { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public LoteAveEngorde LoteAveEngorde { get; set; } = null!;
}
