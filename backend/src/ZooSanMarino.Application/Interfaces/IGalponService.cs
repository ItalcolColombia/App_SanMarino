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
    /// <summary>
    /// Galpones de la empresa y solo de las granjas indicadas (sin bypass de admin / todo el país).
    /// </summary>
    Task<IEnumerable<GalponDetailDto>> GetByFarmIdsForCompanyAsync(IReadOnlyList<int> farmIds, int companyId, CancellationToken ct = default);

    // paraDestino=true omite el alcance granular de ubicación (selección de DESTINO en traslados).
    Task<IEnumerable<GalponDetailDto>> GetAllAsync(bool paraDestino = false);
    Task<GalponDetailDto?>             GetByIdAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>> GetByGranjaAsync(int granjaId, bool paraDestino = false);
    Task<IEnumerable<GalponDetailDto>> GetByGranjaAndNucleoAsync(int granjaId, string nucleoId, bool paraDestino = false);
    Task<GalponDetailDto>              CreateAsync(CreateGalponDto dto);

    /// <summary>
    /// Siguiente Id libre para un galpón nuevo. Lo resuelve el backend porque <c>galpon_id</c> es PK
    /// GLOBAL: el front solo ve los galpones de sus granjas y proponía Ids ya ocupados en otra
    /// empresa/granja, con lo que el alta fallaba siempre para usuarios de alcance parcial.
    /// </summary>
    Task<string>                       GetNextGalponIdAsync();
    Task<GalponDetailDto?>             UpdateAsync(UpdateGalponDto dto);
    Task<bool>                         DeleteAsync(string galponId);     // Soft delete (bloquea si tiene lotes activos)
    Task<bool>                         HardDeleteAsync(string galponId); // Hard delete

    /// <summary>
    /// Mueve un galpón (y todo lo que contiene) a otro núcleo/granja, cascada transaccional.
    /// </summary>
    Task<MoverResultDto>               MoverAsync(MoverGalponDto dto);

    // ─────────────────────────────────────────────────────────────────────────────
    // BÚSQUEDA / DETALLE (nuevos métodos)
    // ─────────────────────────────────────────────────────────────────────────────
    Task<CommonDtos.PagedResult<GalponDetailDto>> SearchAsync(GalponSearchRequest req);
    Task<GalponDetailDto?>                        GetDetailByIdAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>>            GetAllDetailAsync();
    Task<GalponDetailDto?>                        GetDetailByIdSimpleAsync(string galponId);
    Task<IEnumerable<GalponDetailDto>>            GetDetailByGranjaAndNucleoAsync(int granjaId, string nucleoId);
}
