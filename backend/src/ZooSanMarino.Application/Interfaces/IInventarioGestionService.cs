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

    /// <summary>Traslados inter-granja en tránsito pendientes de recepción en la granja destino (opcional).</summary>
    Task<List<InventarioGestionTransitoPendienteDto>> GetTransitosPendientesAsync(int? farmIdDestino = null, CancellationToken ct = default);

    /// <summary>Completa el ingreso en destino de un traslado inter-granja (cierra el tránsito). Si la solicitud aún no descontó origen, descuenta aquí.</summary>
    Task<(InventarioGestionStockDto Destino, InventarioGestionMovimientoDto Movimiento)> RegistrarRecepcionTransitoAsync(InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default);

    /// <summary>Rechaza una solicitud inter-granja pendiente; no modifica stock.</summary>
    Task RechazarTransitoPendienteAsync(InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default);

    /// <summary>Ajusta cantidad (y opcionalmente unidad) de un registro de inventario_gestion_stock. Registra movimiento tipo AjusteStock.</summary>
    Task<InventarioGestionStockDto> ActualizarStockAsync(int stockId, InventarioGestionStockUpdateRequest req, CancellationToken ct = default);

    /// <summary>Elimina el registro de stock. Si había cantidad &gt; 0, registra salida antes de borrar.</summary>
    Task EliminarStockAsync(int stockId, CancellationToken ct = default);
}
