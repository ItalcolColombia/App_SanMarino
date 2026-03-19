// src/ZooSanMarino.Application/Interfaces/IInventarioGestionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>Servicio de Gestión de Inventario (Panama/Ecuador): ingresos y traslados. Alimento → Granja/Núcleo/Galpón; otros → solo Granja.</summary>
public interface IInventarioGestionService
{
    Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    Task<List<InventarioGestionStockDto>> GetStockAsync(
        int? farmId = null,
        string? nucleoId = null,
        string? galponId = null,
        string? itemType = null,
        string? search = null,
        CancellationToken ct = default);

    Task<InventarioGestionStockDto> RegistrarIngresoAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default);

    Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoAsync(InventarioGestionTrasladoRequest req, CancellationToken ct = default);

    /// <summary>Registra consumo (reduce stock). Para devolución usar RegistrarIngresoAsync.</summary>
    Task<InventarioGestionStockDto> RegistrarConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default);

    /// <summary>Histórico de movimientos (entradas, salidas, traslados) con filtros.</summary>
    Task<List<InventarioGestionMovimientoDto>> GetMovimientosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? estado = null,
        string? movementType = null,
        CancellationToken ct = default);
}
