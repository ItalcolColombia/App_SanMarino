using ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para lotes de postura en etapa Producción (lote_postura_produccion).
/// </summary>
public interface ILotePosturaProduccionService
{
    /// <summary>
    /// Obtiene todos los registros de lote_postura_produccion de la empresa en sesión,
    /// filtrados por granjas asignadas al usuario.
    /// </summary>
    // paraDestino=true omite el alcance granular de ubicación (selección de DESTINO en traslados).
    Task<IEnumerable<LotePosturaProduccionDetailDto>> GetAllAsync(CancellationToken ct = default, bool paraDestino = false);

    /// <summary>
    /// Obtiene los lotes producción asociados a un lote (vía lote_postura_levante.lote_id).
    /// </summary>
    Task<IEnumerable<LotePosturaProduccionDetailDto>> GetByLoteIdAsync(int loteId, CancellationToken ct = default);

    /// <summary>Feature 14 — obtiene un LPP por ID con datos frescos.</summary>
    Task<LotePosturaProduccionDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
}
