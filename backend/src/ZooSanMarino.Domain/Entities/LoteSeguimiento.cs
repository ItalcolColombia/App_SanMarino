// src/ZooSanMarino.Domain/Entities/LoteSeguimiento.cs
namespace ZooSanMarino.Domain.Entities;

public class LoteSeguimiento : AuditableEntity
{
public int Id { get; set; } // PK identity
public DateTime Fecha { get; set; }


public int LoteId { get; set; } // parte de FK → LoteReproductora
public string ReproductoraId { get; set; } = null!; // parte de FK → LoteReproductora


// Métricas (decimales con precisión)
public decimal? PesoInicial { get; set; }
public decimal? PesoFinal { get; set; }
public int? MortalidadM { get; set; }
public int? MortalidadH { get; set; }
public int? SelM { get; set; }
public int? SelH { get; set; }
public int? ErrorM { get; set; }
public int? ErrorH { get; set; }
public string? TipoAlimento { get; set; }
public decimal? ConsumoAlimento { get; set; }
public string? Observaciones { get; set; }


// Navegación
public LoteReproductora LoteReproductora { get; set; } = null!;
public Lote Lote { get; set; } = null!;
}