// src/ZooSanMarino.Domain/Entities/MovimientoAves.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Representa un movimiento o traslado de aves entre ubicaciones
/// </summary>
public class MovimientoAves : AuditableEntity
{
    public int Id { get; set; }
    
    // Información del movimiento
    public string NumeroMovimiento { get; set; } = string.Empty; // Número único del movimiento
    public DateTime FechaMovimiento { get; set; }
    public string TipoMovimiento { get; set; } = null!; // Traslado, Ajuste, Liquidacion
    
    // Origen del movimiento
    public int? InventarioOrigenId { get; set; }
    public int? LoteOrigenId { get; set; }  // Changed from string? to int?
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }
    
    // Destino del movimiento
    public int? InventarioDestinoId { get; set; }
    public int? LoteDestinoId { get; set; }  // Changed from string? to int?
    public int? GranjaDestinoId { get; set; }
    public string? NucleoDestinoId { get; set; }
    public string? GalponDestinoId { get; set; }
    public string? PlantaDestino { get; set; }  // Para traslados a plantas
    
    // Cantidades movidas
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public string? MotivoMovimiento { get; set; }
    public string? Descripcion { get; set; }  // Para ventas
    public string? Observaciones { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Completado, Cancelado
    
    // Usuario que realizó el movimiento
    public int UsuarioMovimientoId { get; set; }
    public string? UsuarioNombre { get; set; }
    
    // Fechas de procesamiento
    public DateTime? FechaProcesamiento { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    
    // Campos específicos para despacho (Ecuador)
    public int? EdadAves { get; set; } // Edad de las aves en días
    public string? Raza { get; set; } // Raza de las aves
    public string? Placa { get; set; } // Placa del vehículo
    public TimeOnly? HoraSalida { get; set; } // Hora de salida
    public string? GuiaAgrocalidad { get; set; } // Guía Agrocalidad
    public string? Sellos { get; set; } // Información de sellos
    public string? Ayuno { get; set; } // Información sobre ayuno (horas o indicador)
    public string? Conductor { get; set; } // Nombre del conductor
    public int? TotalPollosGalpon { get; set; } // Total de pollos por galpón
    public double? PesoBruto { get; set; } // Peso bruto en kg
    public double? PesoTara { get; set; } // Peso tara en kg
    
    // Propiedades calculadas
    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;
    
    // Propiedades calculadas de peso (no se guardan en BD)
    public double? PesoNeto => PesoBruto.HasValue && PesoTara.HasValue 
        ? PesoBruto.Value - PesoTara.Value 
        : null;
    
    public double? PromedioPesoAve => PesoNeto.HasValue && TotalAves > 0 
        ? PesoNeto.Value / TotalAves 
        : null;
    
    // Navegación
    public InventarioAves? InventarioOrigen { get; set; }
    public InventarioAves? InventarioDestino { get; set; }
    public Lote? LoteOrigen { get; set; }
    public Lote? LoteDestino { get; set; }
    public Farm? GranjaOrigen { get; set; }
    public Farm? GranjaDestino { get; set; }
    // Removed Nucleo and Galpon navigation properties to avoid EF auto-generated columns
    
    // Métodos de dominio
    public bool EsMovimientoValido()
    {
        var tieneOrigen = InventarioOrigenId.HasValue || LoteOrigenId != null;
        var tieneDestino = InventarioDestinoId.HasValue || LoteDestinoId != null || !string.IsNullOrWhiteSpace(PlantaDestino);
        var esVentaORetiro = TipoMovimiento?.Equals("Venta", StringComparison.OrdinalIgnoreCase) == true ||
                             TipoMovimiento?.Equals("Retiro", StringComparison.OrdinalIgnoreCase) == true;
        
        return TotalAves > 0 && 
               tieneOrigen &&
               (esVentaORetiro || tieneDestino) &&
               Estado == "Pendiente";
    }
    
    public void Procesar()
    {
        if (!EsMovimientoValido())
            throw new InvalidOperationException("El movimiento no es válido para procesar");
            
        Estado = "Completado";
        FechaProcesamiento = DateTime.UtcNow;
    }
    
    public void Cancelar(string motivo)
    {
        if (Estado == "Completado")
            throw new InvalidOperationException("No se puede cancelar un movimiento ya completado");
            
        Estado = "Cancelado";
        FechaCancelacion = DateTime.UtcNow;
        Observaciones = $"{Observaciones} | Cancelado: {motivo}";
    }
    
    public string GenerarNumeroMovimiento()
    {
        return $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Id:D6}";
    }
    
    public bool EsMovimientoInterno()
    {
        return GranjaOrigenId == GranjaDestinoId;
    }
    
    public bool EsMovimientoEntreGranjas()
    {
        return GranjaOrigenId != GranjaDestinoId;
    }
}
