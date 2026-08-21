// src/ZooSanMarino.Domain/Entities/TrasladoHuevos.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Representa un traslado o venta de huevos desde un lote
/// </summary>
public class TrasladoHuevos : AuditableEntity
{
    public int Id { get; set; }
    
    // Información del traslado
    public string NumeroTraslado { get; set; } = string.Empty;
    public DateTime FechaTraslado { get; set; }
    public string TipoOperacion { get; set; } = null!; // "Venta" o "Traslado"
    
    // Lote origen
    public string LoteId { get; set; } = null!; // VARCHAR en BD (legacy)
    public int? LotePosturaProduccionId { get; set; } // LPP: cuando está presente, usar espejo para descuentos
    public int GranjaOrigenId { get; set; }
    
    // Destino (si es traslado)
    public int? GranjaDestinoId { get; set; } // null si es venta
    public string? LoteDestinoId { get; set; } // null si es venta o si no se especifica
    public string? TipoDestino { get; set; } // "Granja", "Planta", null si es venta
    
    // Motivo y descripción (especialmente para venta)
    public string? Motivo { get; set; }
    public string? Descripcion { get; set; }
    
    // Cantidades por tipo de huevo
    public int CantidadLimpio { get; set; }
    public int CantidadTratado { get; set; }
    public int CantidadSucio { get; set; }
    public int CantidadDeforme { get; set; }
    public int CantidadBlanco { get; set; }
    public int CantidadDobleYema { get; set; }
    public int CantidadPiso { get; set; }
    public int CantidadPequeno { get; set; }
    public int CantidadRoto { get; set; }
    public int CantidadDesecho { get; set; }
    public int CantidadOtro { get; set; }

    /// <summary>
    /// Total de huevos del traslado. Antes era una propiedad calculada (suma de las 11
    /// <c>Cantidad*</c>, nunca mapeada por EF); pasó a columna real porque un traslado por ÍTEMS del
    /// catálogo (<see cref="Metadata"/>, Santa Reyes) deja las 11 en 0 — el SERVICE la fija
    /// explícitamente en los dos casos (legacy: misma suma de siempre; por ítems:
    /// <c>HuevoItemsCalculos.SumarTotal</c>).
    /// </summary>
    public int TotalHuevos { get; set; }

    /// <summary>
    /// Desglose por ÍTEM del catálogo (<c>huevoItems</c>, empresas con
    /// <c>companies.clasificacion_huevo_por_items = true</c>) — mismo patrón que
    /// <c>SeguimientoProduccion.Metadata</c>. <c>null</c> para traslados del flujo legacy (11
    /// columnas fijas).
    /// </summary>
    public JsonDocument? Metadata { get; set; }

    // Estado
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Completado, Cancelado
    
    // Usuario que realizó el traslado
    public int UsuarioTrasladoId { get; set; }
    public string? UsuarioNombre { get; set; }
    
    // Fechas de procesamiento
    public DateTime? FechaProcesamiento { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    
    // Observaciones adicionales
    public string? Observaciones { get; set; }
    
    // Navegación
    public Company Company { get; set; } = null!;
    
    // Métodos de dominio
    public bool EsVenta()
    {
        return TipoOperacion == "Venta";
    }
    
    public bool EsTraslado()
    {
        return TipoOperacion == "Traslado";
    }
    
    public bool EsValido()
    {
        return TotalHuevos > 0 && 
               !string.IsNullOrWhiteSpace(LoteId) &&
               Estado == "Pendiente";
    }
    
    public void Procesar()
    {
        if (!EsValido())
            throw new InvalidOperationException("El traslado no es válido para procesar");
            
        Estado = "Completado";
        FechaProcesamiento = DateTime.UtcNow;
    }
    
    public void Cancelar(string motivo)
    {
        if (Estado == "Completado")
            throw new InvalidOperationException("No se puede cancelar un traslado ya completado");
            
        Estado = "Cancelado";
        FechaCancelacion = DateTime.UtcNow;
        Observaciones = $"{Observaciones} | Cancelado: {motivo}";
    }
    
    public string GenerarNumeroTraslado()
    {
        return $"HUE-{DateTime.UtcNow:yyyyMMdd}-{Id:D6}";
    }
}

