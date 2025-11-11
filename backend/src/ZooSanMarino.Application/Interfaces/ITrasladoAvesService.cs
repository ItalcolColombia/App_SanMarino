// src/ZooSanMarino.Application/Interfaces/ITrasladoAvesService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para gestionar traslados de aves (extiende MovimientoAvesService con funcionalidades de venta/traslado)
/// </summary>
public interface ITrasladoAvesService
{
    /// <summary>
    /// Crea un nuevo traslado de aves (venta o traslado)
    /// </summary>
    Task<MovimientoAvesDto> CrearTrasladoAvesAsync(CrearTrasladoAvesDto dto, int usuarioId);
    
    /// <summary>
    /// Procesa un traslado pendiente (aplica las reducciones en el inventario)
    /// </summary>
    Task<bool> ProcesarTrasladoAsync(int movimientoId);
    
    /// <summary>
    /// Cancela un traslado pendiente
    /// </summary>
    Task<bool> CancelarTrasladoAsync(int movimientoId, string motivo);
}

