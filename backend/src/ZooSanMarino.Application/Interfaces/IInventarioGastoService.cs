// src/ZooSanMarino.Application/Interfaces/IInventarioGastoService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IInventarioGastoService
{
    Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    Task<List<InventarioGastoListItemDto>> SearchAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    /// <summary>Filas del reporte (una por línea de consumo). NUNCA incluye gastos eliminados.</summary>
    Task<List<InventarioGastoExportRowDto>> ExportAsync(InventarioGastoSearchRequest req, CancellationToken ct = default);

    /// <summary>
    /// Existencias de TODOS los ítems no-alimento del catálogo (tengan o no consumo) con su saldo
    /// actual y lo consumido en el rango. Es la hoja de control de inventario del reporte.
    /// </summary>
    Task<List<InventarioGastoExistenciaDto>> GetExistenciasAsync(InventarioGastoExistenciasRequest req, CancellationToken ct = default);

    Task<InventarioGastoDto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<InventarioGastoDto> CreateAsync(CreateInventarioGastoRequest req, CancellationToken ct = default);

    Task DeleteAsync(int id, string? motivo, CancellationToken ct = default);

    Task<List<string>> GetConceptosAsync(int? farmId = null, CancellationToken ct = default);

    Task<List<InventarioGastoItemStockDto>> GetItemsWithStockAsync(int farmId, string concepto, CancellationToken ct = default);
}

