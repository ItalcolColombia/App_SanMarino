using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ICatalogItemService
{

    Task<List<CatalogItemDto>> GetAllAsync(string? q, CancellationToken ct = default);
    Task<PagedResult<CatalogItemDto>> GetAsync(string? q, int page, int pageSize, CancellationToken ct = default);
    Task<CatalogItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CatalogItemDto?> CreateAsync(CatalogItemCreateRequest dto, CancellationToken ct = default);
    Task<CatalogItemDto?> UpdateAsync(int id, CatalogItemUpdateRequest dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default);
}
