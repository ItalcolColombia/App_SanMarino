// src/ZooSanMarino.Domain/Entities/LoteGalpon.cs
namespace ZooSanMarino.Domain.Entities;
public class LoteGalpon
{
// PK compuesta (LoteId, ReproductoraId, GalponId)
public int LoteId { get; set; } // FK → LoteReproductora.LoteId
public string ReproductoraId { get; set; } = null!; // FK → LoteReproductora.ReproductoraId
public string GalponId { get; set; } = null!; // FK → Galpon.GalponId


public int? M { get; set; }
public int? H { get; set; }


// Navegación
public LoteReproductora LoteReproductora { get; set; } = null!;
public Galpon Galpon { get; set; } = null!;
}
