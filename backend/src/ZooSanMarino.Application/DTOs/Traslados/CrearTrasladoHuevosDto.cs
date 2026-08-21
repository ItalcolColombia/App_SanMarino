// src/ZooSanMarino.Application/DTOs/Traslados/CrearTrasladoHuevosDto.cs
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// DTO para crear un traslado de huevos
/// </summary>
public record CrearTrasladoHuevosDto
{
    public string LoteId { get; init; } = string.Empty;
    /// <summary>Si se envía, se usa flujo LPP (espejo) en lugar de LoteId legacy.</summary>
    public int? LotePosturaProduccionId { get; init; }
    public DateTime FechaTraslado { get; init; }
    public string TipoOperacion { get; init; } = string.Empty; // "Venta" o "Traslado"
    
    // Cantidades por tipo de huevo
    public int CantidadLimpio { get; init; }
    public int CantidadTratado { get; init; }
    public int CantidadSucio { get; init; }
    public int CantidadDeforme { get; init; }
    public int CantidadBlanco { get; init; }
    public int CantidadDobleYema { get; init; }
    public int CantidadPiso { get; init; }
    public int CantidadPequeno { get; init; }
    public int CantidadRoto { get; init; }
    public int CantidadDesecho { get; init; }
    public int CantidadOtro { get; init; }
    
    // Destino (si es traslado)
    public int? GranjaDestinoId { get; init; }
    public string? LoteDestinoId { get; init; }
    public string? TipoDestino { get; init; } // "Granja" o "Planta"
    
    // Motivo y descripción (especialmente para venta)
    public string? Motivo { get; init; }
    public string? Descripcion { get; init; }
    public string? Observaciones { get; init; }

    /// <summary>
    /// Clasificación por ÍTEM del catálogo (Santa Reyes, <c>companies.clasificacion_huevo_por_items
    /// = true</c>). Si viene con filas, reemplaza a las 11 <c>Cantidad*</c> de arriba (que deben
    /// llegar en 0) y requiere <see cref="LotePosturaProduccionId"/> — no hay flujo legacy por
    /// ítems. <c>null</c>/vacío = flujo legacy de siempre, sin cambios.
    /// </summary>
    public IReadOnlyList<HuevoItemSeguimientoDto>? HuevoItems { get; init; }

    // Total calculado (flujo legacy; con HuevoItems el service usa HuevoItemsCalculos.SumarTotal)
    public int TotalHuevos => CantidadLimpio + CantidadTratado + CantidadSucio +
                              CantidadDeforme + CantidadBlanco + CantidadDobleYema +
                              CantidadPiso + CantidadPequeno + CantidadRoto +
                              CantidadDesecho + CantidadOtro;
}





