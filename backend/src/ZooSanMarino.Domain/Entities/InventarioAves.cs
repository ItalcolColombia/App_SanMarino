// src/ZooSanMarino.Domain/Entities/InventarioAves.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Representa el inventario actual de aves en una ubicación específica
/// </summary>
public class InventarioAves : AuditableEntity
{
    public int Id { get; set; }
    
    // Identificación del lote
    public int LoteId { get; set; }
    
    // Ubicación actual
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    
    // Cantidades actuales
    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }
    
    // Información adicional
    public DateTime FechaActualizacion { get; set; }
    public string? Observaciones { get; set; }
    
    // Estado del inventario
    public string Estado { get; set; } = "Activo"; // Activo, Trasladado, Liquidado
    
    // Propiedades calculadas
    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;
    
    // Navegación
    public Lote Lote { get; set; } = null!;
    public Farm Granja { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    
    // Historial de movimientos
    public ICollection<MovimientoAves> MovimientosOrigen { get; set; } = new List<MovimientoAves>();
    public ICollection<MovimientoAves> MovimientosDestino { get; set; } = new List<MovimientoAves>();
    
    // Métodos de dominio
    public bool PuedeRealizarMovimiento(int hembras, int machos, int mixtas)
    {
        return CantidadHembras >= hembras && 
               CantidadMachos >= machos && 
               CantidadMixtas >= mixtas &&
               Estado == "Activo";
    }
    
    public void AplicarMovimientoSalida(int hembras, int machos, int mixtas)
    {
        if (!PuedeRealizarMovimiento(hembras, machos, mixtas))
            throw new InvalidOperationException("No hay suficientes aves para el movimiento");
            
        CantidadHembras -= hembras;
        CantidadMachos -= machos;
        CantidadMixtas -= mixtas;
        FechaActualizacion = DateTime.UtcNow;
    }
    
    public void AplicarMovimientoEntrada(int hembras, int machos, int mixtas)
    {
        CantidadHembras += hembras;
        CantidadMachos += machos;
        CantidadMixtas += mixtas;
        FechaActualizacion = DateTime.UtcNow;
    }
    
    public void CambiarUbicacion(int granjaId, string? nucleoId, string? galponId)
    {
        GranjaId = granjaId;
        NucleoId = nucleoId;
        GalponId = galponId;
        FechaActualizacion = DateTime.UtcNow;
    }
}
