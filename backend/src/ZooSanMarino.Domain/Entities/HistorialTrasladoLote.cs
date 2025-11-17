// src/ZooSanMarino.Domain/Entities/HistorialTrasladoLote.cs
namespace ZooSanMarino.Domain.Entities;

public class HistorialTrasladoLote
{
    public int Id { get; set; }
    public int LoteOriginalId { get; set; }
    public int LoteNuevoId { get; set; }
    public int GranjaOrigenId { get; set; }
    public int GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? Observaciones { get; set; }
    public int CompanyId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Relaciones
    public Lote? LoteOriginal { get; set; }
    public Lote? LoteNuevo { get; set; }
    public Farm? GranjaOrigen { get; set; }
    public Farm? GranjaDestino { get; set; }
}

