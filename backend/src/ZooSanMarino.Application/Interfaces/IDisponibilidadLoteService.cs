// src/ZooSanMarino.Application/Interfaces/IDisponibilidadLoteService.cs
using ZooSanMarino.Application.DTOs.Produccion;
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

    /// <summary>
    /// Obtiene disponibilidad de huevos desde espejo_huevo_produccion (flujo LPP).
    /// Usa huevo_*_dinamico como saldo disponible.
    /// </summary>
    Task<DisponibilidadLoteDto?> ObtenerDisponibilidadLoteLPPAsync(int lotePosturaProduccionId);

    /// <summary>
    /// Valida disponibilidad de huevos para un lote LPP (desde espejo).
    /// </summary>
    Task<bool> ValidarDisponibilidadHuevosLPPAsync(int lotePosturaProduccionId, Dictionary<string, int> cantidadesPorTipo);

    /// <summary>
    /// Disponibilidad por ÍTEM del catálogo de huevo (Santa Reyes) de un lote LPP: producido
    /// (<c>SeguimientoProduccion.Metadata.huevoItems</c>) menos ya transferido (<c>TrasladoHuevos
    /// .Metadata.huevoItems</c> de traslados <c>Completado</c>). Ver F10 §9 del plan de Santa Reyes.
    /// </summary>
    Task<IReadOnlyList<HuevoItemSeguimientoDto>> ObtenerDisponibilidadHuevoItemsLPPAsync(int lotePosturaProduccionId);

    /// <summary>
    /// Valida que cada ítem solicitado no exceda su disponible (ver
    /// <see cref="ObtenerDisponibilidadHuevoItemsLPPAsync"/>).
    /// </summary>
    Task<bool> ValidarDisponibilidadHuevoItemsLPPAsync(int lotePosturaProduccionId, IReadOnlyList<HuevoItemSeguimientoDto> solicitados);
}





