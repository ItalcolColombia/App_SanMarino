// src/ZooSanMarino.Application/Interfaces/IItemInventarioService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IItemInventarioService
{
    Task<List<ItemInventarioDto>> GetAllAsync(string? q = null, string? tipoItem = null, bool? activo = null, CancellationToken ct = default);
    Task<ItemInventarioDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ItemInventarioDto> CreateAsync(ItemInventarioCreateRequest req, CancellationToken ct = default);
    Task<ItemInventarioDto?> UpdateAsync(int id, ItemInventarioUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default);
    Task<ItemInventarioCargaMasivaResult> CargaMasivaAsync(IReadOnlyList<ItemInventarioCargaMasivaRow> filas, CancellationToken ct = default);
}
