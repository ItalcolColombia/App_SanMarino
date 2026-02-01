// src/ZooSanMarino.Application/Interfaces/ITrasladoHuevosService.cs
using ZooSanMarino.Application.DTOs.Traslados;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestionar traslados de huevos
/// </summary>
public interface ITrasladoHuevosService
{
    /// <summary>
    /// Crea un nuevo traslado de huevos (venta o traslado)
    /// </summary>
    Task<TrasladoHuevosDto> CrearTrasladoHuevosAsync(CrearTrasladoHuevosDto dto, int usuarioId);
    
    /// <summary>
    /// Procesa un traslado pendiente (aplica las reducciones en el inventario)
    /// </summary>
    Task<bool> ProcesarTrasladoAsync(int trasladoId);
    
    /// <summary>
    /// Cancela un traslado pendiente
    /// </summary>
    Task<bool> CancelarTrasladoAsync(int trasladoId, string motivo);
    
    /// <summary>
    /// Obtiene todos los traslados de un lote
    /// </summary>
    Task<IEnumerable<TrasladoHuevosDto>> ObtenerTrasladosPorLoteAsync(string loteId);
    
    /// <summary>
    /// Actualiza un traslado de huevos existente (solo si está en estado "Pendiente")
    /// </summary>
    Task<TrasladoHuevosDto> ActualizarTrasladoHuevosAsync(int trasladoId, ActualizarTrasladoHuevosDto dto, int usuarioId);
    
    /// <summary>
    /// Obtiene un traslado de huevos por ID
    /// </summary>
    Task<TrasladoHuevosDto?> ObtenerTrasladoPorIdAsync(int trasladoId);
}





