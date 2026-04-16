// src/ZooSanMarino.Application/Interfaces/IInventarioGestionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>Servicio de Gestión de Inventario (Panama/Ecuador): ingresos y traslados. Alimento → Granja/Núcleo/Galpón; otros → solo Granja.</summary>
public interface IInventarioGestionService
{
    Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default);

    /// <summary>Lotes en granjas asignadas y valores distintos de concepto/tipo/estado ya presentes en movimientos (histórico).</summary>
    Task<InventarioGestionHistoricoFiltrosDto> GetHistoricoFiltrosAsync(CancellationToken ct = default);

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
        string? nucleoId = null,
        string? galponId = null,
        int? loteId = null,
        string? search = null,
        string? concepto = null,
        string? tipoItem = null,
        string? tipoOperacion = null,
        string? unit = null,
        string? referenceContains = null,
        string? reasonContains = null,
        string? transferGroupId = null,
        int? itemInventarioEcuadorId = null,
        int? fromFarmId = null,
        string? fromNucleoId = null,
        string? fromGalponId = null,
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

    /// <summary>
    /// Anula un registro del histórico (solo <c>Consumo</c> o <c>Ingreso</c>): revierte el efecto en <c>inventario_gestion_stock</c> y elimina la fila del movimiento.
    /// No aplica a traslados ni tránsito inter-granja.
    /// </summary>
    Task AnularMovimientoHistoricoAsync(int movimientoId, string? motivo, CancellationToken ct = default);

    // ─── TRASLADOS ───────────────────────────────────────────────────────────

    /// <summary>Lista de traslados agrupados por TransferGroupId en granjas asignadas al usuario.</summary>
    Task<List<InventarioGestionTrasladoListDto>> GetTrasladosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default);

    /// <summary>Actualiza la fecha de movimiento de un traslado (aplica a todos los registros del TransferGroupId).</summary>
    Task<InventarioGestionTrasladoListDto> ActualizarFechaTrasladoAsync(
        Guid transferGroupId,
        InventarioGestionActualizarFechaTrasladoRequest req,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina todos los movimientos de un traslado, revierte stock según tipo (salida → devuelve a origen,
    /// entrada → descuenta de destino) y marca anulado=true en lote_registro_historico_unificado.
    /// </summary>
    Task EliminarTrasladoAsync(Guid transferGroupId, CancellationToken ct = default);

    // ─── INGRESOS ────────────────────────────────────────────────────────────

    /// <summary>Lista de ingresos (Ingreso, TrasladoEntrada, TrasladoInterGranjaEntrada) en granjas asignadas al usuario.</summary>
    Task<List<InventarioGestionIngresoListDto>> GetIngresosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default);

    /// <summary>Actualiza la fecha de movimiento de un ingreso.</summary>
    Task<InventarioGestionIngresoListDto> ActualizarFechaIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarFechaIngresoRequest req,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina un ingreso (Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada), revierte stock
    /// y marca anulado=true en lote_registro_historico_unificado.
    /// </summary>
    Task EliminarIngresoAsync(int movimientoId, CancellationToken ct = default);
}
