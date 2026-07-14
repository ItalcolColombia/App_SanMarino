// src/ZooSanMarino.Application/Interfaces/IVacunacionCronogramaService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IVacunacionCronogramaService
{
    /// <summary>Granjas asignadas + lotes de las 3 líneas + vacunas del catálogo, para armar los combos del formulario.</summary>
    Task<VacunacionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    /// <summary>Cronograma completo del lote (encadena Levante↔Producción cuando corresponde), ordenado por franja.</summary>
    Task<List<VacunacionCronogramaItemDto>> GetCronogramaLoteAsync(VacunacionCronogramaLoteRequest req, CancellationToken ct = default);

    Task<VacunacionCronogramaItemDto> CreateAsync(VacunacionCronogramaItemCreateRequest req, CancellationToken ct = default);
    Task<VacunacionCronogramaItemDto?> UpdateAsync(int id, VacunacionCronogramaItemUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
