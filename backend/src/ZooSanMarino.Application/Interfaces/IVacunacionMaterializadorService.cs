// src/ZooSanMarino.Application/Interfaces/IVacunacionMaterializadorService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// El puente entre el plan de vacunación de la empresa y el cronograma de cada lote.
///
/// <para>
/// Cada <c>Preview</c> y su <c>Aplicar</c> salen del <b>mismo</b> cálculo puro
/// (<c>VacunacionMaterializadorCalculos</c>): lo que se ve antes de confirmar es exactamente lo que
/// se escribe. Nada acá borra filas —ni las que creó él mismo—: lo que el plan dejó de reclamar se
/// reporta como sobrante y se decide a mano.
/// </para>
/// </summary>
public interface IVacunacionMaterializadorService
{
    /// <summary>Qué pasaría con el cronograma de un lote. No escribe nada.</summary>
    Task<VacunacionMaterializacionLoteDto> PreviewLoteAsync(string lineaProductiva, int loteId, CancellationToken ct = default);

    /// <summary>Aplica el plan a un lote. Idempotente: correrlo de nuevo no escribe.</summary>
    Task<VacunacionMaterializacionLoteDto> AplicarLoteAsync(string lineaProductiva, int loteId, CancellationToken ct = default);

    /// <summary>Qué pasaría con todos los lotes vivos a los que hoy les toca esta plantilla. No escribe nada.</summary>
    Task<VacunacionMaterializacionMasivaDto> PreviewPlantillaAsync(int plantillaId, CancellationToken ct = default);

    /// <summary>
    /// Aplica la plantilla a todos los lotes vivos que resuelven a ella, <b>uno por transacción</b>:
    /// el que falle queda reportado con su error y los demás se aplican igual.
    /// </summary>
    Task<VacunacionMaterializacionMasivaDto> AplicarPlantillaAsync(int plantillaId, CancellationToken ct = default);

    /// <summary>
    /// Materialización disparada al crear un lote. <b>Nunca lanza</b>: un plan sanitario que no se
    /// pudo copiar no puede impedir que se cree un lote. Devuelve cuántas filas escribió.
    /// </summary>
    Task<int> MaterializarAlCrearLoteAsync(string lineaProductiva, int loteId, CancellationToken ct = default);
}
