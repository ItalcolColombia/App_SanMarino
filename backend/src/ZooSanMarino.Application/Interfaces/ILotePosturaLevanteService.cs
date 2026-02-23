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
}
