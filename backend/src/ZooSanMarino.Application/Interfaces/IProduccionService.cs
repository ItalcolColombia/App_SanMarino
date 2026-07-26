// src/ZooSanMarino.Application/Interfaces/IProduccionService.cs
using ZooSanMarino.Application.DTOs.Produccion;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;

namespace ZooSanMarino.Application.Interfaces;

public interface IProduccionService
{
    Task<bool> ExisteProduccionLoteAsync(int loteId);
    Task<int> CrearProduccionLoteAsync(CrearProduccionLoteRequest request);
    Task<ProduccionLoteDetalleDto?> ObtenerProduccionLoteAsync(int loteId);
    Task<int> CrearSeguimientoAsync(CrearSeguimientoRequest request);
    Task ActualizarSeguimientoAsync(int id, CrearSeguimientoRequest request);
    Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int? loteId, int? lotePosturaProduccionId, DateTime? desde, DateTime? hasta, int page, int size);
    Task<InformacionLoteResponse> ObtenerInformacionLoteAsync(int lotePosturaProduccionId);
    Task<SeguimientoItemDto?> ObtenerSeguimientoPorIdAsync(int seguimientoId);
    Task<bool> EliminarSeguimientoAsync(int seguimientoId);
    /// <summary>
    /// Lotes con semana &gt;= 26 (umbral real REQ-012b: 25 semanas). <paramref name="paraDestino"/> =
    /// true omite el alcance granular de ubicación (catálogo de lote DESTINO en traslados).
    /// </summary>
    Task<IEnumerable<LoteDtos.LoteDetailDto>> ObtenerLotesProduccionAsync(bool paraDestino = false);
}



