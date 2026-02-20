// src/ZooSanMarino.Application/Interfaces/ILoteSeguimientoService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteSeguimientoService
{
    Task<IEnumerable<LoteSeguimientoDto>> GetAllAsync();
    /// <summary>Filtra por lote, reproductora y rango de fechas (opcional). Usado por el listado del módulo Seguimiento Diario Lote Reproductora.</summary>
    Task<IEnumerable<LoteSeguimientoDto>> GetByLoteYReproAsync(string loteId, string reproductoraId, DateTime? desde = null, DateTime? hasta = null);
    Task<LoteSeguimientoDto?>             GetByIdAsync(int id);
    Task<LoteSeguimientoDto>              CreateAsync(CreateLoteSeguimientoDto dto);
    Task<LoteSeguimientoDto?>             UpdateAsync(UpdateLoteSeguimientoDto dto);
    Task<bool>                           DeleteAsync(int id);
}
