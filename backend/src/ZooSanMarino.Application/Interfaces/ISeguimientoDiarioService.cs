// src/ZooSanMarino.Application/Interfaces/ISeguimientoDiarioService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio de CRUD y filtrado para la tabla unificada seguimiento_diario.
/// </summary>
public interface ISeguimientoDiarioService
{
    Task<SeguimientoDiarioDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<SeguimientoDiarioDto>> GetFilteredAsync(SeguimientoDiarioFilterRequest filter, CancellationToken ct = default);
    Task<SeguimientoDiarioDto> CreateAsync(CreateSeguimientoDiarioDto dto, CancellationToken ct = default);
    Task<SeguimientoDiarioDto?> UpdateAsync(UpdateSeguimientoDiarioDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
