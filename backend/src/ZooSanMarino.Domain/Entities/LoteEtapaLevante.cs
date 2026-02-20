// src/ZooSanMarino.Domain/Entities/LoteEtapaLevante.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Historial de la etapa Levante por lote: aves con que inicia y con que termina
/// (al pasar a Producción). Una fila por lote. Los descuentos durante Levante
/// se registran en SeguimientoLoteLevante.
/// </summary>
public class LoteEtapaLevante
{
    public int Id { get; set; }
    public int LoteId { get; set; }
    public int AvesInicioHembras { get; set; }
    public int AvesInicioMachos { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int? AvesFinHembras { get; set; }
    public int? AvesFinMachos { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Lote Lote { get; set; } = null!;
}
