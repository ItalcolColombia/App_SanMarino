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

    /// <summary>Resumen para el modal «Cerrar/Abrir lote» de Seguimiento Diario de Producción.</summary>
    Task<CierreLoteProduccionResumenDto?> GetResumenCierreAsync(int lotePosturaProduccionId, CancellationToken ct = default);

    /// <summary>
    /// Cierra el lote de producción: a partir de acá no se puede crear, editar ni eliminar
    /// seguimiento diario de ese lote. No borra ni modifica ningún registro existente.
    /// </summary>
    Task<LotePosturaProduccionDetailDto?> CerrarLoteAsync(int lotePosturaProduccionId, CerrarLoteProduccionRequest request, CancellationToken ct = default);

    /// <summary>Reabre un lote de producción cerrado y vuelve a habilitar la captura diaria.</summary>
    Task<LotePosturaProduccionDetailDto?> AbrirLoteAsync(int lotePosturaProduccionId, AbrirLoteProduccionRequest request, CancellationToken ct = default);
}
