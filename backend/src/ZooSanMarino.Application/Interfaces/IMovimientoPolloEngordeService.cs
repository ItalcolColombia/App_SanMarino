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
    /// <param name="omitirGateLiquidado">
    /// Bypass EXPLÍCITO del gate de liquidación congelada — solo la corrección de aves
    /// disponibles (repara lotes liquidados y re-congela al terminar).
    /// </param>
    Task<MovimientoPolloEngordeDto?> CompleteAsync(int id, bool omitirGateLiquidado = false);

    /// <summary>
    /// Resumen para reportes: aves con que inició el lote, cuántas salieron (completados), cuántas vendidas (tipo Venta), aves actuales.
    /// tipoLote: "LoteAveEngorde" | "LoteReproductoraAveEngorde"; loteId: PK del lote.
    /// </summary>
    /// <summary>
    /// Edades presentes en un lote de engorde: las aves propias (por su <c>fecha_encaset</c>) más cada
    /// cohorte recibida por traslado, con su edad heredada del lote origen y la ubicación de procedencia.
    /// <c>null</c> si el lote no existe o no es de la empresa activa.
    /// </summary>
    Task<ZooSanMarino.Application.DTOs.Traslados.LoteCohortesDto?> GetCohortesLoteEngordeAsync(
        int loteAveEngordeId, CancellationToken ct = default);

    Task<ResumenAvesLoteDto?> GetResumenAvesLoteAsync(string tipoLote, int loteId);

    /// <summary>Resúmenes de varios lotes en una sola llamada (una fila por id solicitado).</summary>
    Task<ResumenAvesLotesResponse> GetResumenAvesLotesAsync(ResumenAvesLotesRequest request);

    /// <summary>
    /// Disponibilidad para ventas por lote (incluye reservas en estado Pendiente para evitar sobreventa).
    /// </summary>
    Task<AvesDisponiblesLotesResponse> GetAvesDisponiblesLotesAsync(AvesDisponiblesLotesRequest request);

    /// <summary>
    /// Auditoría de coherencia de ventas vs disponibilidad por lote (y corrección opcional).
    /// </summary>
    Task<AuditoriaVentasEngordeResponse> AuditarVentasEngordeAsync(AuditoriaVentasEngordeRequest request);

    /// <summary>
    /// Corrige incoherencias en ventas ya Completadas ajustando cantidades (devuelve al lote solo la diferencia).
    /// No elimina movimientos; actualiza cantidades y observaciones.
    /// </summary>
    Task<CorregirVentasCompletadasResponse> CorregirVentasCompletadasAsync(CorregirVentasCompletadasRequest request);

    /// <summary>Venta por granja: varios movimientos Pendiente con la misma cabecera de despacho, en una transacción.</summary>
    Task<VentaGranjaDespachoResultDto> CreateVentaGranjaDespachoAsync(CreateVentaGranjaDespachoDto dto);

    /// <summary>Completa varios movimientos Pendiente en una transacción (descuenta inventario por lote).</summary>
    Task<IReadOnlyList<MovimientoPolloEngordeDto>> CompletarBatchAsync(IReadOnlyList<int> movimientoIds);

    /// <summary>
    /// Corrección masiva de peso: recalcula PesoNeto prorrateado para ventas históricas donde el peso
    /// global se guardó igual en todos los movimientos de un mismo despacho.
    /// Agrupa por NumeroDespacho y distribuye el peso proporcional a las aves de cada movimiento.
    /// </summary>
    Task<OrganizarPesoResponse> OrganizarPesoAsync(OrganizarPesoRequest request);

    /// <summary>
    /// Carga el peso báscula de un despacho COMPLETO (por <c>FacturaId</c>) y lo prorratea entre sus
    /// líneas, con la misma aritmética de la creación. Pensado para empresas con
    /// <c>venta_engorde_peso_diferido</c>, donde la báscula llega al día siguiente:
    /// con <c>Confirmar = true</c> además completa las líneas Pendiente en la misma transacción
    /// («confirmar la venta cargando el peso»); con <c>false</c> solo corrige el peso de un despacho
    /// ya completado, sin tocar estados ni saldos de aves.
    /// </summary>
    Task<RegistrarPesoFacturaResponse> RegistrarPesoFacturaAsync(Guid facturaId, RegistrarPesoFacturaRequest request);
}
