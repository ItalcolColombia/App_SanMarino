using ZooSanMarino.Application.DTOs;           // LoteDto, Create/Update DTOs
using ZooSanMarino.Application.DTOs.Lotes;     // LoteDetailDto, LoteSearchRequest
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteService
{
    // Compat existentes - ACTUALIZADO para devolver información completa de relaciones
    Task<IEnumerable<LoteDetailDto>> GetAllAsync();
    Task<bool> DeleteAsync(int loteId);

    // Nuevos / detallados
    Task<CommonDtos.PagedResult<LoteDetailDto>> SearchAsync(LoteSearchRequest req);
    Task<LoteDetailDto?> GetByIdAsync(int loteId);
    Task<LoteDetailDto> CreateAsync(CreateLoteDto dto);
    Task<LoteDetailDto?> UpdateAsync(UpdateLoteDto dto);

    // Limpieza dura (opcional)
    Task<bool> HardDeleteAsync(int loteId);
    
        /// <summary>
        /// Devuelve el resumen de mortalidad y saldos (hembras/machos) de un lote de levante.
        /// Solo resta mortalidad a las bases del lote (y descuenta MortCaja si aplica).
        /// </summary>
        Task<LoteMortalidadResumenDto?> GetMortalidadResumenAsync(int loteId);
}
