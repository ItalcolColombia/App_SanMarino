// src/ZooSanMarino.Application/Interfaces/IFarmInventoryService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IFarmInventoryService
{
    Task<List<FarmInventoryDto>> GetByFarmAsync(int farmId, string? q, CancellationToken ct = default);
    Task<FarmInventoryDto?> GetByIdAsync(int farmId, int id, CancellationToken ct = default);
    Task<FarmInventoryDto?> GetByFarmAndCatalogItemAsync(int farmId, int catalogItemId, CancellationToken ct = default);

    // Upsert por (farmId, catalogItemId) para inventario inicial
    Task<FarmInventoryDto> CreateOrReplaceAsync(int farmId, FarmInventoryCreateRequest req, CancellationToken ct = default);

    Task<FarmInventoryDto?> UpdateAsync(int farmId, int id, FarmInventoryUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(int farmId, int id, bool hard = false, CancellationToken ct = default);
}
