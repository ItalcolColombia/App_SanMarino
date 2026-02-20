// src/ZooSanMarino.Application/DTOs/ReporteMovimientosHuevosDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para representar un registro diario de movimientos de huevos
/// </summary>
public record MovimientoHuevoDiarioDto
{
    public DateTime Fecha { get; init; }
    public string LoteId { get; init; } = string.Empty;
    public string LoteNombre { get; init; } = string.Empty;
    
    // Producción diaria (POSTURA)
    public int Postura { get; init; } // HuevoTot del seguimiento diario
    
    // Categorías de huevos
    public int HvtoFertil { get; init; } // HuevoInc (huevo incubable/fértil)
    public int HvoComercial { get; init; } // Limpio + Tratado (huevo comercial)
    public int HuevoDesecho { get; init; } // Desecho
    
    // Detalle de categorías
    public int Limpio { get; init; }
    public int Tratado { get; init; }
    public int Sucio { get; init; }
    public int Deforme { get; init; }
    public int Blanco { get; init; }
    public int DobleYema { get; init; }
    public int Piso { get; init; }
    public int Pequeno { get; init; }
    public int Roto { get; init; }
    public int Otro { get; init; }
    
    // Movimientos
    public int Entrada { get; init; } // Traslados recibidos (entrada)
    public int CapturaInfo { get; init; } // Producción diaria registrada
    public int Venta { get; init; } // Ventas de huevos
    public int Salida { get; init; } // Traslados enviados (salida)
    public int TrasladoAPlanta { get; init; } // Traslados a planta
    public int Descarte { get; init; } // Descartados
    
    // Categorías técnicas (si aplica)
    public int? TecnicoAAA { get; init; }
    public int? TecnicoAA { get; init; }
    public int? TecnicoB { get; init; }
    public int? TecnicoC { get; init; }
    
    // Categorías de picado (si aplica)
    public int? PicadoPecoso { get; init; }
    public int? PicadoManchado { get; init; }
    public int? PicadoSucio { get; init; }
}

/// <summary>
/// DTO para el reporte completo de movimientos de huevos
/// </summary>
public record ReporteMovimientosHuevosDto
{
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public int? SemanaContable { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
    
    public List<MovimientoHuevoDiarioDto> MovimientosDiarios { get; init; } = new();
    
    // Totales
    public int TotalPostura { get; init; }
    public int TotalHvtoFertil { get; init; }
    public int TotalHvoComercial { get; init; }
    public int TotalHuevoDesecho { get; init; }
    public int TotalEntrada { get; init; }
    public int TotalVenta { get; init; }
    public int TotalSalida { get; init; }
    public int TotalTrasladoAPlanta { get; init; }
    public int TotalDescarte { get; init; }
}

/// <summary>
/// Request DTO para obtener el reporte de movimientos de huevos
/// </summary>
public record ObtenerReporteMovimientosHuevosRequestDto
{
    public int LotePadreId { get; init; }
    public int? SemanaContable { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
}
