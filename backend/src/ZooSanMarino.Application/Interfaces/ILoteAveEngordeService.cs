using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface ILoteAveEngordeService
{
    /// <summary>Listado simple con información completa de relaciones (solo no eliminados).</summary>
    Task<IEnumerable<LoteAveEngordeDetailDto>> GetAllAsync();
    /// <summary>Búsqueda avanzada paginada (filtros por granja, núcleo, galpón, fechas, raza, técnico, etc.).</summary>
    Task<CommonDtos.PagedResult<LoteAveEngordeDetailDto>> SearchAsync(LoteAveEngordeSearchRequest req);
    /// <summary>Detalle de un lote de engorde por ID (tenant-safe).</summary>
    Task<LoteAveEngordeDetailDto?> GetByIdAsync(int loteAveEngordeId);
    /// <summary>Crea un lote de engorde (valida granja, núcleo, galpón y guía genética).</summary>
    Task<LoteAveEngordeDetailDto> CreateAsync(CreateLoteAveEngordeDto dto);
    /// <summary>Actualiza un lote de engorde (mismas validaciones que crear).</summary>
    Task<LoteAveEngordeDetailDto?> UpdateAsync(UpdateLoteAveEngordeDto dto);
    /// <summary>Eliminación lógica (soft delete).</summary>
    Task<bool> DeleteAsync(int loteAveEngordeId);
    /// <summary>Eliminación física (hard delete).</summary>
    Task<bool> HardDeleteAsync(int loteAveEngordeId);
}
