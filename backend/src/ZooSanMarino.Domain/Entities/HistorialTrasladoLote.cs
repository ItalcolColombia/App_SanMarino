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

    /// <summary>
    /// Día REAL en que el lote se movió, que no es lo mismo que <see cref="CreatedAt"/> —el instante
    /// en que alguien lo registró—. El Reporte Diario de Costos de POSTURA usa esta fecha como la
    /// efectiva del traslado; antes usaba <c>created_at</c>, así que un lote movido la semana pasada
    /// y registrado hoy le atribuía costos a la granja equivocada durante esos días.
    /// <para>Nullable con fallback a hoy: un cliente que no la mande se comporta como antes.</para>
    /// </summary>
    public DateOnly? FechaTraslado { get; set; }

    // Relaciones
    public Lote? LoteOriginal { get; set; }
    public Lote? LoteNuevo { get; set; }
    public Farm? GranjaOrigen { get; set; }
    public Farm? GranjaDestino { get; set; }
}

