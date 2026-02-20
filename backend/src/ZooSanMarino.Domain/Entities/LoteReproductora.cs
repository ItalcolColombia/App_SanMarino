// src/ZooSanMarino.Domain/Entities/LoteReproductora.cs
namespace ZooSanMarino.Domain.Entities;

public class LoteReproductora
{
// PK compuesta (LoteId, ReproductoraId)
// LoteId es string porque en la BD es character varying(64)
public string LoteId { get; set; } = null!;
public string ReproductoraId { get; set; } = null!;


public string NombreLote { get; set; } = null!;
public DateTime? FechaEncasetamiento { get; set; }


// Cantidades (no negativas): M/H son actuales; aves_inicio es historial al abrir
public int? M { get; set; }
public int? H { get; set; }
public int? AvesInicioHembras { get; set; }
public int? AvesInicioMachos { get; set; }
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
    // Nota: La relación con Lote está comentada debido al desajuste de tipos
    // (lote_id es string en lote_reproductoras pero integer en lotes)
    // public Lote Lote { get; set; } = null!; // Comentado temporalmente
    public List<LoteGalpon> LoteGalpones { get; set; } = new(); // FK -> (LoteId, ReproductoraId)
    public List<LoteSeguimiento> LoteSeguimientos { get; set; } = new(); // FK -> (LoteId, ReproductoraId)
}