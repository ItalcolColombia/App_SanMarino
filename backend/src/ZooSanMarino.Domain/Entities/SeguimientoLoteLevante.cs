/// file: backend/src/ZooSanMarino.Domain/Entities/SeguimientoLoteLevante.cs
namespace ZooSanMarino.Domain.Entities;

public class SeguimientoLoteLevante
{
   public int Id { get; set; }
    public int LoteId { get; set; }
    public DateTime FechaRegistro { get; set; }

    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeMachos { get; set; }

    public double ConsumoKgHembras { get; set; }
    public string TipoAlimento { get; set; } = null!;
    public string? Observaciones { get; set; }

    public double? KcalAlH { get; set; }
    public double? ProtAlH { get; set; }
    public double? KcalAveH { get; set; }
    public double? ProtAveH { get; set; }

    public string Ciclo { get; set; } = "Normal";

    // NUEVOS (double precision en PG → double?)
    public double? ConsumoKgMachos { get; set; }
    public double? PesoPromH { get; set; }
    public double? PesoPromM { get; set; }
    public double? UniformidadH { get; set; }
    public double? UniformidadM { get; set; }
    public double? CvH { get; set; }
    public double? CvM { get; set; }

    public Lote Lote { get; set; } = null!;
}
