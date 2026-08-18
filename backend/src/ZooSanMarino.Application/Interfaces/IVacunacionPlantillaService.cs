// src/ZooSanMarino.Application/Interfaces/IVacunacionPlantillaService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Administración del plan de vacunación estándar de la empresa (W1.3).
///
/// <para>
/// Es un servicio aparte del cronograma a propósito: <c>IVacunacionCronogramaService</c> escribe el
/// plan <b>de un lote</b> y este el <b>de la empresa</b>. Son dos sujetos y dos permisos distintos.
/// </para>
///
/// <para>
/// <b>Nada de acá escribe en <c>vacunacion_cronograma_items</c>.</b> Materializar la plantilla al
/// cronograma de los lotes es W2; mientras tanto una empresa sin plantillas se comporta exactamente
/// como antes de que estas tablas existieran.
/// </para>
/// </summary>
public interface IVacunacionPlantillaService
{
    /// <summary>Plantillas vivas de la empresa activa, con el conteo de ítems de cada una.</summary>
    /// <param name="lineaProductiva">Filtro opcional por línea.</param>
    /// <param name="soloActivas">Deja fuera las apagadas.</param>
    Task<List<VacunacionPlantillaDto>> GetAllAsync(string? lineaProductiva = null, bool soloActivas = false, CancellationToken ct = default);

    /// <summary>Plantilla con sus ítems, o <c>null</c> si no existe o es de otra empresa.</summary>
    Task<VacunacionPlantillaDetalleDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<VacunacionPlantillaDetalleDto> CreateAsync(VacunacionPlantillaCreateRequest req, CancellationToken ct = default);
    Task<VacunacionPlantillaDetalleDto?> UpdateAsync(int id, VacunacionPlantillaUpdateRequest req, CancellationToken ct = default);

    /// <summary>Soft-delete de la plantilla <b>y de sus ítems</b>, todos con el mismo sello de fecha.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<VacunacionPlantillaItemDto> AddItemAsync(int plantillaId, VacunacionPlantillaItemCreateRequest req, CancellationToken ct = default);
    Task<VacunacionPlantillaItemDto?> UpdateItemAsync(int plantillaId, int itemId, VacunacionPlantillaItemUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteItemAsync(int plantillaId, int itemId, CancellationToken ct = default);

    /// <summary>
    /// Qué plantilla le tocaría a un lote y por qué. <b>Solo lectura</b>: la vista previa de W2.
    /// </summary>
    Task<VacunacionPlantillaEfectivaDto> GetEfectivaAsync(string lineaProductiva, int loteId, CancellationToken ct = default);
}
