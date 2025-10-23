// src/ZooSanMarino.Domain/Entities/LoteReproductora.cs
namespace ZooSanMarino.Domain.Entities;

public class LoteReproductora
{
// PK compuesta (LoteId, ReproductoraId)
public int LoteId { get; set; }
public string ReproductoraId { get; set; } = null!;


public string NombreLote { get; set; } = null!;
public DateTime? FechaEncasetamiento { get; set; }


// Cantidades (no negativas)
public int? M { get; set; }
public int? H { get; set; }
public int? Mixtas { get; set; }
public int? MortCajaH { get; set; }
public int? MortCajaM { get; set; }
public int? UnifH { get; set; }
public int? UnifM { get; set; }


// Pesos (decimal con precisión)
public decimal? PesoInicialM { get; set; }
public decimal? PesoInicialH { get; set; }
public decimal? PesoMixto { get; set; }


// Navegación
public Lote Lote { get; set; } = null!; // FK -> Lote(LoteId)
public List<LoteGalpon> LoteGalpones { get; set; } = new(); // FK -> (LoteId, ReproductoraId)
public List<LoteSeguimiento> LoteSeguimientos { get; set; } = new(); // FK -> (LoteId, ReproductoraId)
}