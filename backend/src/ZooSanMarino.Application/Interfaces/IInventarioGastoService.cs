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

    /// <param name="siloId">
    /// Silo o bodega del que se va a consumir (empresas con inventario por silo): acota el saldo
    /// ofrecido al de ESE silo. Sin él, el saldo es el total de la granja —ya agregado, una fila por
    /// ítem—, que es lo que ven las empresas sin el flag.
    /// </param>
    Task<List<InventarioGastoItemStockDto>> GetItemsWithStockAsync(int farmId, string concepto, int? siloId = null, CancellationToken ct = default);
}

