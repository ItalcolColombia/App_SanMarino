// src/ZooSanMarino.Application/Interfaces/IItemInventarioEcuadorService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IItemInventarioEcuadorService
{
    Task<List<ItemInventarioEcuadorDto>> GetAllAsync(string? q = null, string? tipoItem = null, bool? activo = null, CancellationToken ct = default);
    Task<ItemInventarioEcuadorDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ItemInventarioEcuadorDto> CreateAsync(ItemInventarioEcuadorCreateRequest req, CancellationToken ct = default);
    Task<ItemInventarioEcuadorDto?> UpdateAsync(int id, ItemInventarioEcuadorUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default);
    Task<ItemInventarioEcuadorCargaMasivaResult> CargaMasivaAsync(IReadOnlyList<ItemInventarioEcuadorCargaMasivaRow> filas, CancellationToken ct = default);
}
