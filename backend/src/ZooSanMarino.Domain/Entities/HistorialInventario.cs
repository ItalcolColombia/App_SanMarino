// src/ZooSanMarino.Domain/Entities/HistorialInventario.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Representa el historial de cambios en el inventario de aves
/// </summary>
public class HistorialInventario : AuditableEntity
{
    public int Id { get; set; }
    
    // Referencia al inventario
    public int InventarioId { get; set; }
    public int LoteId { get; set; }
    
    // Información del cambio
    public DateTime FechaCambio { get; set; }
    public string TipoCambio { get; set; } = null!; // Entrada, Salida, Ajuste, Traslado
    public int? MovimientoId { get; set; } // Referencia al movimiento que causó el cambio
    
    // Cantidades antes del cambio
    public int CantidadHembrasAnterior { get; set; }
    public int CantidadMachosAnterior { get; set; }
    public int CantidadMixtasAnterior { get; set; }
    
    // Cantidades después del cambio
    public int CantidadHembrasNueva { get; set; }
    public int CantidadMachosNueva { get; set; }
    public int CantidadMixtasNueva { get; set; }
    
    // Diferencias
    public int DiferenciaHembras => CantidadHembrasNueva - CantidadHembrasAnterior;
    public int DiferenciaMachos => CantidadMachosNueva - CantidadMachosAnterior;
    public int DiferenciaMixtas => CantidadMixtasNueva - CantidadMixtasAnterior;
    public int DiferenciaTotal => DiferenciaHembras + DiferenciaMachos + DiferenciaMixtas;
    
    // Ubicación en el momento del cambio
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    
    // Usuario que realizó el cambio
    public int UsuarioCambioId { get; set; }
    public string? UsuarioNombre { get; set; }
    
    // Información adicional
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }
    
    // Navegación
    public InventarioAves Inventario { get; set; } = null!;
    public Lote Lote { get; set; } = null!;
    public MovimientoAves? Movimiento { get; set; }
    public Farm Granja { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    
    // Métodos de dominio
    public bool EsIncremento()
    {
        return DiferenciaTotal > 0;
    }
    
    public bool EsDecremento()
    {
        return DiferenciaTotal < 0;
    }
    
    public bool EsAjuste()
    {
        return TipoCambio == "Ajuste";
    }
    
    public string GenerarResumen()
    {
        var accion = DiferenciaTotal > 0 ? "Entrada" : "Salida";
        var cantidad = Math.Abs(DiferenciaTotal);
        return $"{accion} de {cantidad} aves - {TipoCambio}";
    }
}
