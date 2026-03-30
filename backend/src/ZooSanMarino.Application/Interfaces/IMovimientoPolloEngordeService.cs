// src/ZooSanMarino.Application/Interfaces/IMovimientoPolloEngordeService.cs
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface IMovimientoPolloEngordeService
{
    Task<MovimientoPolloEngordeDto> CreateAsync(CreateMovimientoPolloEngordeDto dto);
    Task<MovimientoPolloEngordeDto?> GetByIdAsync(int id);
    Task<IEnumerable<MovimientoPolloEngordeDto>> GetAllAsync();
    Task<ZooSanMarino.Application.DTOs.Common.PagedResult<MovimientoPolloEngordeDto>> SearchAsync(MovimientoPolloEngordeSearchRequest request);
    Task<MovimientoPolloEngordeDto?> UpdateAsync(int id, UpdateMovimientoPolloEngordeDto dto);
    Task<bool> CancelAsync(int id, string motivo);

    /// <summary>
    /// Elimina el registro (soft-delete). Si estaba <c>Completado</c>, revierte el efecto en lotes
    /// (devuelve aves al origen y resta del destino si había traslado).
    /// </summary>
    Task<bool> EliminarAsync(int id, string? motivo);

    /// <summary>
    /// Completa el movimiento: descuenta aves del lote origen y suma al destino (si existe).
    /// El lote queda actualizado y el movimiento pasa a estado Completado.
    /// </summary>
    Task<MovimientoPolloEngordeDto?> CompleteAsync(int id);

    /// <summary>
    /// Resumen para reportes: aves con que inició el lote, cuántas salieron (completados), cuántas vendidas (tipo Venta), aves actuales.
    /// tipoLote: "LoteAveEngorde" | "LoteReproductoraAveEngorde"; loteId: PK del lote.
    /// </summary>
    Task<ResumenAvesLoteDto?> GetResumenAvesLoteAsync(string tipoLote, int loteId);

    /// <summary>Resúmenes de varios lotes en una sola llamada (una fila por id solicitado).</summary>
    Task<ResumenAvesLotesResponse> GetResumenAvesLotesAsync(ResumenAvesLotesRequest request);

    /// <summary>Venta por granja: varios movimientos Pendiente con la misma cabecera de despacho, en una transacción.</summary>
    Task<VentaGranjaDespachoResultDto> CreateVentaGranjaDespachoAsync(CreateVentaGranjaDespachoDto dto);

    /// <summary>Completa varios movimientos Pendiente en una transacción (descuenta inventario por lote).</summary>
    Task<IReadOnlyList<MovimientoPolloEngordeDto>> CompletarBatchAsync(IReadOnlyList<int> movimientoIds);
}
