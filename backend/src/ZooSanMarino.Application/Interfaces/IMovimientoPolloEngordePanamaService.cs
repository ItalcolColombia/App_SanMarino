using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Procesos de venta de pollo engorde específicos de Panamá. Lógica separada del servicio genérico
/// para aislar el comportamiento por país (R-Panamá): venta por despacho con asignación H/M sobre
/// las mixtas del lote (el inventario se descuenta de mixtas; el reporte muestra el split).
/// </summary>
public interface IMovimientoPolloEngordePanamaService
{
    /// <summary>
    /// Venta Panamá por galpón: crea un movimiento Pendiente por cada lote con cantidad asignada
    /// (EsVentaMixta=true), compartiendo cabecera de despacho/factura, en una transacción.
    /// </summary>
    Task<VentaGranjaDespachoResultDto> CreateVentaPanamaDespachoAsync(CreateVentaPanamaDespachoDto dto);
}
