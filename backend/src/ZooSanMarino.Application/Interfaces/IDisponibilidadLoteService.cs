// src/ZooSanMarino.Application/Interfaces/IDisponibilidadLoteService.cs
using ZooSanMarino.Application.DTOs.Traslados;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para obtener información de disponibilidad de lotes (aves o huevos)
/// </summary>
public interface IDisponibilidadLoteService
{
    /// <summary>
    /// Obtiene la información completa de disponibilidad de un lote
    /// Si es levante: retorna información de aves vivas
    /// Si es producción: retorna información de huevos acumulados
    /// </summary>
    Task<DisponibilidadLoteDto?> ObtenerDisponibilidadLoteAsync(string loteId);
    
    /// <summary>
    /// Valida si hay suficientes aves disponibles para un traslado
    /// </summary>
    Task<bool> ValidarDisponibilidadAvesAsync(string loteId, int cantidadHembras, int cantidadMachos);
    
    /// <summary>
    /// Valida si hay suficientes huevos disponibles para un traslado
    /// </summary>
    Task<bool> ValidarDisponibilidadHuevosAsync(string loteId, Dictionary<string, int> cantidadesPorTipo);
}




