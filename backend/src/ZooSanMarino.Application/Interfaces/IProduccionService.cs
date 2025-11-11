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
    Task<ListaSeguimientoResponse> ListarSeguimientoAsync(int loteId, DateTime? desde, DateTime? hasta, int page, int size);
    Task<SeguimientoItemDto?> ObtenerSeguimientoPorIdAsync(int seguimientoId);
    Task<bool> EliminarSeguimientoAsync(int seguimientoId);
    Task<IEnumerable<LoteDtos.LoteDetailDto>> ObtenerLotesProduccionAsync(); // Lotes con semana >= 26
}



