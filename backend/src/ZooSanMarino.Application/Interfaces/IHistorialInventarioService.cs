// src/ZooSanMarino.Application/Interfaces/IHistorialInventarioService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestión del historial de inventario
/// </summary>
public interface IHistorialInventarioService
{
    // Consultas de historial
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<HistorialInventarioDto>> SearchAsync(HistorialInventarioSearchRequest request);
    Task<IEnumerable<HistorialInventarioDto>> GetByInventarioIdAsync(int inventarioId);
    Task<IEnumerable<HistorialInventarioDto>> GetByLoteIdAsync(int loteId);
    Task<IEnumerable<HistorialInventarioDto>> GetByMovimientoIdAsync(int movimientoId);
    
    // Trazabilidad
    Task<TrazabilidadLoteDto> GetTrazabilidadLoteAsync(int loteId);
    Task<IEnumerable<EventoTrazabilidadDto>> GetEventosLoteAsync(int loteId, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    
    // Resúmenes y estadísticas
    Task<ResumenCambiosDto> GetResumenCambiosAsync(DateTime fechaDesde, DateTime fechaHasta, int? granjaId = null);
    Task<IEnumerable<HistorialInventarioDto>> GetCambiosRecientesAsync(int dias = 7);
    Task<IEnumerable<HistorialInventarioDto>> GetCambiosPorUsuarioAsync(int usuarioId, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    
    // Auditoría
    Task<IEnumerable<HistorialInventarioDto>> GetAjustesInventarioAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    Task<IEnumerable<HistorialInventarioDto>> GetMovimientosGrandesAsync(int minimoAves = 1000);
    
    // Registro de cambios (usado internamente por otros servicios)
    Task RegistrarCambioAsync(int inventarioId, string tipoCambio, int? movimientoId, 
        int hembrasAnterior, int machosAnterior, int mixtasAnterior,
        int hembrasNueva, int machosNueva, int mixtasNueva,
        string? motivo = null, string? observaciones = null);
}
