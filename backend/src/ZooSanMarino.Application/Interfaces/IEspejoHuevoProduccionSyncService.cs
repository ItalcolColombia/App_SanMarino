namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Mantiene <c>espejo_huevo_produccion</c> alineado con
/// la suma de <c>produccion_diaria</c> (seguimiento) menos los traslados completados.
/// </summary>
public interface IEspejoHuevoProduccionSyncService
{
    /// <summary>
    /// Recalcula histórico (suma producción) y dinámico (histórico − traslados Completado) para un LPP.
    /// Crea la fila en espejo si no existe.
    /// </summary>
    Task RecalcularEspejoHuevoProduccionAsync(int lotePosturaProduccionId, CancellationToken cancellationToken = default);
}
