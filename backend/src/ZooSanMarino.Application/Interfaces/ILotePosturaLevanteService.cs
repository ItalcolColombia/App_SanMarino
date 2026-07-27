using ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Application.Interfaces;

public interface ILotePosturaLevanteService
{
    /// <summary>
    /// Obtiene todos los registros de lote_postura_levante de la empresa en sesión.
    /// </summary>
    // paraDestino=true omite el alcance granular de ubicación (selección de DESTINO en traslados).
    Task<IEnumerable<LotePosturaLevanteDetailDto>> GetAllAsync(CancellationToken ct = default, bool paraDestino = false);

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

    /// <summary>
    /// Resumen previo a reabrir: si se puede, por qué no, y qué se va a eliminar si se confirma.
    /// Para el modal del front; <see cref="AbrirLoteAsync"/> revalida lo mismo del lado del servidor.
    /// </summary>
    Task<ReaperturaLoteLevanteResumenDto?> GetResumenReaperturaAsync(int lotePosturaLevanteId, CancellationToken ct = default);

    /// <summary>
    /// Reabre el lote levante.
    /// <para>
    /// <b>Rechaza</b> la reapertura si el lote de producción generado por el cierre tiene seguimiento
    /// diario capturado por el usuario (hay que eliminarlo antes desde Seguimiento Diario de
    /// Producción) o si ese lote está cerrado (hay que reabrirlo antes).
    /// </para>
    /// <para>
    /// Si no lo tiene, el lote de producción se marca como eliminado (soft delete) junto con las
    /// filas que generó el propio cierre, y se vuelve a crear —actualizado— al cerrar el levante de
    /// nuevo.
    /// </para>
    /// </summary>
    Task<LotePosturaLevanteDetailDto?> AbrirLoteAsync(int lotePosturaLevanteId, AbrirLoteLevanteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retorna qué letras (A-F) ya están ocupadas y cuáles están disponibles
    /// para un prefijo de lote en un galpón específico.
    /// </summary>
    Task<LetrasDisponiblesDto> GetLetrasDisponiblesAsync(
        string galponId, string loteBase, CancellationToken ct = default);
}
