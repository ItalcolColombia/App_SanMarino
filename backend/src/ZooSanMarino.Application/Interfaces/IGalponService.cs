// file: src/ZooSanMarino.Application/Interfaces/IGalponService.cs

using ZooSanMarino.Application.DTOs;                      // CreateGalponDto, UpdateGalponDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common; // PagedResult<T>
using ZooSanMarino.Application.DTOs.Galpones;            // GalponDetailDto, GalponSearchRequest

namespace ZooSanMarino.Application.Interfaces;

public interface IGalponService
{
    // ─────────────────────────────────────────────────────────────────────────────
    // CRUD / LISTADOS con detalle consistente (lo que consume el GalponController)
    // ─────────────────────────────────────────────────────────────────────────────
    Task<IEnumerable<GalponDetailDto>> GetAllAsync();
    Task<GalponDetailDto?>             GetByIdAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>> GetByGranjaAsync(int granjaId);
    Task<IEnumerable<GalponDetailDto>> GetByGranjaAndNucleoAsync(int granjaId, string nucleoId);
    Task<GalponDetailDto>              CreateAsync(CreateGalponDto dto);
    Task<GalponDetailDto?>             UpdateAsync(UpdateGalponDto dto);
    Task<bool>                         DeleteAsync(string galponId);     // Soft delete
    Task<bool>                         HardDeleteAsync(string galponId); // Hard delete

    // ─────────────────────────────────────────────────────────────────────────────
    // BÚSQUEDA / DETALLE (nuevos métodos)
    // ─────────────────────────────────────────────────────────────────────────────
    Task<CommonDtos.PagedResult<GalponDetailDto>> SearchAsync(GalponSearchRequest req);
    Task<GalponDetailDto?>                        GetDetailByIdAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>>            GetAllDetailAsync();
    Task<GalponDetailDto?>                        GetDetailByIdSimpleAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>>            GetDetailByGranjaAndNucleoAsync(int granjaId, string nucleoId);
}
