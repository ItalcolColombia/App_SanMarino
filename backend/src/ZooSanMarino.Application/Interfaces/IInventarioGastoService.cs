// src/ZooSanMarino.Application/Interfaces/IInventarioGastoService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IInventarioGastoService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    Task<List<InventarioGastoListItemDto>> SearchAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    Task<List<InventarioGastoExportRowDto>> ExportAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    Task<InventarioGastoDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<InventarioGastoDto> CreateAsync(CreateInventarioGastoRequest req, CancellationToken ct = default);

    Task DeleteAsync(int id, string? motivo, CancellationToken ct = default);

    Task<List<string>> GetConceptosAsync(CancellationToken ct = default);

    Task<List<InventarioGastoItemStockDto>> GetItemsWithStockAsync(int farmId, string concepto, CancellationToken ct = default);
}

