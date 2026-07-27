using ZooSanMarino.Application.DTOs.Traslados;

namespace ZooSanMarino.Application.Interfaces;

public interface ITrasladoAvesDesdeSegService
{
    /// <summary>Devuelve el stock actual de aves (H y M) del lote para validar el traslado.</summary>
    Task<DisponibilidadAvesDto?> GetDisponibilidadAvesAsync(int loteId, string tipo, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta el traslado: actualiza seguimiento diario, decrementa lote origen,
    /// incrementa lote destino e inserta un MovimientoAves de auditoría.
    /// </summary>
    Task<TrasladoAvesResultDto> EjecutarTrasladoDesdeSegAsync(
        TrasladoAvesDesdeSegDiarioDto dto,
        int usuarioId,
        CancellationToken ct = default);

    /// <summary>
    /// Edades presentes en un lote base: la cohorte propia (por <c>lotes.fecha_encaset</c>) más las
    /// cohortes recibidas por traslado, con su edad actual en días y semanas.
    /// Devuelve <c>null</c> si el lote no existe o no pertenece a la empresa efectiva.
    /// </summary>
    Task<LoteCohortesDto?> GetCohortesLoteAsync(int loteId, CancellationToken ct = default);
}
