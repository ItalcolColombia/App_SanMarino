using ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Application.Interfaces;

public interface ILotePosturaLevanteService
{
    /// <summary>
    /// Obtiene todos los registros de lote_postura_levante de la empresa en sesión.
    /// </summary>
    Task<IEnumerable<LotePosturaLevanteDetailDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los lotes levante asociados a un lote (lote_id).
    /// </summary>
    Task<IEnumerable<LotePosturaLevanteDetailDto>> GetByLoteIdAsync(int loteId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un lote levante por ID con EdadMaximaSeguimiento (máx. edad en semanas con registros en seguimiento_diario).
    /// </summary>
    Task<LotePosturaLevanteDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Resumen para cerrar lote manualmente (aves actuales y si ya existe producción).</summary>
    Task<CierreLoteLevanteResumenDto?> GetResumenCierreAsync(int lotePosturaLevanteId, CancellationToken ct = default);

    /// <summary>Cierra el lote levante y crea el lote de producción (antes automático en semana 26).</summary>
    Task<LotePosturaLevanteDetailDto?> CerrarLoteYCrearProduccionAsync(int lotePosturaLevanteId, CerrarLoteLevanteRequest request, CancellationToken ct = default);

    /// <summary>Reabre el lote levante si la producción generada no tiene datos dependientes.</summary>
    Task<LotePosturaLevanteDetailDto?> AbrirLoteAsync(int lotePosturaLevanteId, AbrirLoteLevanteRequest request, CancellationToken ct = default);
}
