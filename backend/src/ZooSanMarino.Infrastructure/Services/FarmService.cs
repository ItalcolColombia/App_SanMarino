// src/ZooSanMarino.Infrastructure/Services/FarmService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.DTOs;                          // FarmDto, Create/Update
using AppInterfaces = ZooSanMarino.Application.Interfaces;   // IFarmService, ICurrentUser
using CommonDtos   = ZooSanMarino.Application.DTOs.Common;   // PagedResult<>
using FarmDtos     = ZooSanMarino.Application.DTOs.Farms;    // Farm* DTOs (Tree, Detail, Search)

using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services
{
    public class FarmService : IFarmService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly ICurrentUser _current;
        private readonly ICompanyResolver _companyResolver;
        private readonly IUserPermissionService _userPermissionService;

        public FarmService(ZooSanMarinoContext ctx, ICurrentUser current, ICompanyResolver companyResolver, IUserPermissionService userPermissionService)
        {
            _ctx = ctx;
            _current = current;
            _companyResolver = companyResolver;
            _userPermissionService = userPermissionService;
        }

        // ======================================================
        // HELPER METHODS
        // ======================================================
        
        /// <summary>
        /// Obtiene el CompanyId efectivo (validación por país deshabilitada temporalmente)
        /// </summary>
        private async Task<int> GetEffectiveCompanyIdAsync()
        {
            // Si hay una empresa activa especificada en el header, usarla
            if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            {
                var companyId = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
                if (companyId.HasValue)
                {
                    // Validación por país deshabilitada temporalmente
                    return companyId.Value;
                }
            }

            // Fallback al CompanyId del token JWT
            // Validación por país deshabilitada temporalmente
            
            return _current.CompanyId;
        }

        // ======================================================
        // BÚSQUEDA / LISTADO AVANZADO
        // ======================================================
        public async Task<CommonDtos.PagedResult<FarmDetailDto>> SearchAsync(FarmSearchRequest req)
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            
            var q = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == effectiveCompanyId);

            if (req.SoloActivos) q = q.Where(f => f.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim().ToLower();
                q = q.Where(f =>
                    f.Name.ToLower().Contains(term) ||
                    f.Id.ToString().Contains(req.Search!.Trim())
                );
            }

            if (req.RegionalId.HasValue)      q = q.Where(f => f.RegionalId     == req.RegionalId.Value);
            if (req.DepartamentoId.HasValue)  q = q.Where(f => f.DepartamentoId == req.DepartamentoId.Value);
            if (req.CiudadId.HasValue)        q = q.Where(f => f.MunicipioId    == req.CiudadId.Value);
            if (!string.IsNullOrWhiteSpace(req.Status))
            {
                var s = NormalizeStatus(req.Status);
                q = q.Where(f => f.Status == s);
            }

            // ⬅️ NUEVO: filtro por País (via Departamento.PaisId)
            if (req.PaisId.HasValue)
            {
                var paisId = req.PaisId.Value;
                q = q.Where(f =>
                    _ctx.Set<Departamento>().Any(d => d.DepartamentoId == f.DepartamentoId && d.PaisId == paisId)
                );
            }

            q = ApplyOrder(q, req.SortBy, req.SortDesc);

            var page     = req.Page     <= 0 ? 1  : req.Page;
            var pageSize = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 200);

            var total = await q.LongCountAsync();
            var items = await ProjectToDetail(q)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CommonDtos.PagedResult<FarmDetailDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            };
        }

        // ======================================================
        // DETALLE
        // ======================================================
        public async Task<FarmDetailDto?> GetDetailByIdAsync(int id)
        {
            var q = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == _current.CompanyId
                         && f.Id == id
                         && f.DeletedAt == null);

            return await ProjectToDetail(q).SingleOrDefaultAsync();
        }

        // ======================================================
        // ÁRBOL para cascadas (Farm → Núcleos → Galpones)
        // ======================================================
    public async Task<FarmTreeDto?> GetTreeByIdAsync(int farmId, bool soloActivos = true)
    {
        var farm = await _ctx.Farms
            .AsNoTracking()
            .Where(f => f.CompanyId == _current.CompanyId
                    && f.Id == farmId
                    && (!soloActivos || f.DeletedAt == null))
            .Select(f => new FarmLiteDto(
                f.Id,
                f.Name,
                f.RegionalId,     // ⬅️ es int? y el DTO acepta int?, no forzar 0
                f.DepartamentoId,
                f.MunicipioId
            ))
            .SingleOrDefaultAsync();

        if (farm is null) return null;

        var nucleos = await _ctx.Nucleos
            .AsNoTracking()
            .Where(n => n.GranjaId == farmId
                    && (!soloActivos || n.DeletedAt == null))
            .Select(n => new NucleoNodeDto(
                n.NucleoId, // ⬅️ si es int, no uses int.Parse
                n.GranjaId,
                n.NucleoNombre,
                _ctx.Galpones.Count(g =>
                    g.NucleoId == n.NucleoId &&
                    g.GranjaId == n.GranjaId &&
                    (!soloActivos || g.DeletedAt == null)
                ),
                _ctx.Lotes.Count(l =>
                    l.NucleoId == n.NucleoId &&
                    l.GranjaId == n.GranjaId &&
                    (!soloActivos || l.DeletedAt == null)
                )
            ))
            .OrderBy(n => n.NucleoNombre)
            .ToListAsync();

        // ⬅️ Faltaba este return
        return new FarmTreeDto(farm, nucleos);
    }

        // ======================================================
        // CRUD BÁSICO (compat)
        // ======================================================
        public async Task<IEnumerable<FarmDto>> GetAllAsync(Guid? userId = null)
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            
            var query = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == effectiveCompanyId && f.DeletedAt == null);

            // Si se proporciona un userId, filtrar SOLO por las granjas asignadas directamente al usuario
            if (userId.HasValue)
            {
                Console.WriteLine($"=== FarmService.GetAllAsync - Filtrando por userId: {userId} ===");
                
                // Obtener SOLO las granjas asignadas directamente al usuario en UserFarms
                var userFarmIds = await _ctx.UserFarms
                    .AsNoTracking()
                    .Where(uf => uf.UserId == userId.Value)
                    .Select(uf => uf.FarmId)
                    .ToListAsync();

                Console.WriteLine($"=== Granjas asignadas directamente al usuario: {string.Join(", ", userFarmIds)} ===");
                
                // Si el usuario tiene granjas asignadas, filtrar por esas
                // Si no tiene ninguna, devolver lista vacía
                if (userFarmIds.Any())
                {
                    Console.WriteLine($"✅ Filtrando por {userFarmIds.Count} granjas asignadas: [{string.Join(", ", userFarmIds)}]");
                    query = query.Where(f => userFarmIds.Contains(f.Id));
                }
                else
                {
                    Console.WriteLine("⚠️ Usuario no tiene granjas asignadas - devolviendo lista vacía");
                    // Devolver lista vacía filtrando por un ID que no existe
                    query = query.Where(f => f.Id == -1);
                }
            }
            else
            {
                Console.WriteLine("=== FarmService.GetAllAsync - Sin filtro de usuario, devolviendo todas las granjas ===");
            }

            var result = await query
                .Select(f => new FarmDto(
                    f.Id,
                    f.CompanyId,
                    f.Name,
                    f.RegionalId,        // ⬅️ int?
                    f.Status,
                    f.DepartamentoId,
                    f.MunicipioId        // → CiudadId
                ))
                .ToListAsync();

            Console.WriteLine($"=== FarmService.GetAllAsync - Devolviendo {result.Count} granjas ===");
            return result;
        }

        public async Task<FarmDto?> GetByIdAsync(int id) =>
            await _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == _current.CompanyId && f.DeletedAt == null && f.Id == id)
                .Select(f => new FarmDto(
                    f.Id,
                    f.CompanyId,
                    f.Name,
                    f.RegionalId,        // ⬅️ int?
                    f.Status,
                    f.DepartamentoId,
                    f.MunicipioId        // → CiudadId
                ))
                .SingleOrDefaultAsync();

        public async Task<FarmDto> CreateAsync(CreateFarmDto dto)
        {
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre es obligatorio.", nameof(dto.Name));

            if (!dto.DepartamentoId.HasValue)
                throw new ArgumentException("DepartamentoId es obligatorio.", nameof(dto.DepartamentoId));

            if (!dto.CiudadId.HasValue)
                throw new ArgumentException("CiudadId es obligatorio.", nameof(dto.CiudadId));

            // NUEVA VALIDACIÓN: Verificar que el usuario puede crear granjas en este país
            var departamento = await _ctx.Set<Departamento>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DepartamentoId == dto.DepartamentoId.Value);
            
            if (departamento == null)
                throw new ArgumentException("El departamento especificado no existe.", nameof(dto.DepartamentoId));

            var canCreateInCountry = await _userPermissionService.CanCreateFarmInCountryAsync(_current.UserId, departamento.PaisId);
            if (!canCreateInCountry)
                throw new UnauthorizedAccessException("No tienes permisos para crear granjas en este país.");

            var normalizedStatus = NormalizeStatus(dto.Status);
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

            var dup = await _ctx.Farms
                .AsNoTracking()
                .AnyAsync(f => f.CompanyId == effectiveCompanyId &&
                               f.Name.ToLower() == name.ToLower() &&
                               f.DeletedAt == null);
            if (dup) throw new InvalidOperationException("Ya existe una granja con ese nombre en la compañía.");

            var entity = new Farm
            {
                CompanyId       = effectiveCompanyId,
                Name            = name,
                RegionalId      = dto.RegionalId,            // null OK
                Status          = normalizedStatus,          // 'A'/'I'
                DepartamentoId  = dto.DepartamentoId!.Value,
                MunicipioId     = dto.CiudadId!.Value,       // DTO ciudadId → entidad MunicipioId
                CreatedByUserId = _current.UserId,
                CreatedAt       = DateTime.UtcNow
            };

            _ctx.Farms.Add(entity);
            await _ctx.SaveChangesAsync();

            return new FarmDto(
                entity.Id,
                entity.CompanyId,
                entity.Name,
                entity.RegionalId,
                entity.Status,
                entity.DepartamentoId,
                entity.MunicipioId
            );
        }

        public async Task<FarmDto?> UpdateAsync(UpdateFarmDto dto)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == dto.Id && f.CompanyId == _current.CompanyId);

            if (entity is null || entity.DeletedAt != null) return null;

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre es obligatorio.", nameof(dto.Name));

            if (!dto.DepartamentoId.HasValue)
                throw new ArgumentException("DepartamentoId es obligatorio.", nameof(dto.DepartamentoId));

            if (!dto.CiudadId.HasValue)
                throw new ArgumentException("CiudadId es obligatorio.", nameof(dto.CiudadId));

            if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var dup = await _ctx.Farms
                    .AsNoTracking()
                    .AnyAsync(f => f.CompanyId == _current.CompanyId &&
                                   f.Id != dto.Id &&
                                   f.Name.ToLower() == name.ToLower() &&
                                   f.DeletedAt == null);
                if (dup) throw new InvalidOperationException("Ya existe otra granja con ese nombre en la compañía.");
            }

            entity.Name           = name;
            entity.RegionalId     = dto.RegionalId;                 // null OK
            entity.Status         = NormalizeStatus(dto.Status);    // 'A'/'I'
            entity.DepartamentoId = dto.DepartamentoId!.Value;
            entity.MunicipioId    = dto.CiudadId!.Value;
            entity.UpdatedByUserId= _current.UserId;
            entity.UpdatedAt      = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();

            return new FarmDto(
                entity.Id,
                entity.CompanyId,
                entity.Name,
                entity.RegionalId,
                entity.Status,
                entity.DepartamentoId,
                entity.MunicipioId
            );
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == id && f.CompanyId == _current.CompanyId);
            if (entity is null || entity.DeletedAt != null) return false;

            entity.DeletedAt       = DateTime.UtcNow;
            entity.UpdatedByUserId = _current.UserId;
            entity.UpdatedAt       = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HardDeleteAsync(int id)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == id && f.CompanyId == _current.CompanyId);
            if (entity is null) return false;

            _ctx.Farms.Remove(entity);
            await _ctx.SaveChangesAsync();
            return true;
        }

        // ======================================================
        // Helpers
        // ======================================================
        private static string NormalizeStatus(string? status)
        {
            var s = (status ?? "A").Trim().ToUpperInvariant();
            return (s == "A" || s == "I") ? s : "A";
        }

        private static IQueryable<FarmDetailDto> ProjectToDetail(IQueryable<Farm> q)
        {
            return q.Select(f => new FarmDetailDto(
                f.Id,
                f.CompanyId,
                f.Name,
                f.RegionalId,                    // ⬅️ int?
                f.Status,
                f.DepartamentoId,
                f.MunicipioId,                   // → CiudadId
                f.CreatedByUserId,
                f.CreatedAt,
                f.UpdatedByUserId,
                f.UpdatedAt,
                f.Nucleos.Count(),
                f.Nucleos.SelectMany(n => n.Galpones).Count(),
                f.Lotes.Count()
            ));
        }

        private static IQueryable<Farm> ApplyOrder(IQueryable<Farm> q, string sortBy, bool desc)
        {
            Expression<Func<Farm, object>> key = (sortBy ?? "").ToLower() switch
            {
                "name"             => f => f.Name,
                "regional_id"      => f => (object?)f.RegionalId ?? 0,         // null-safe
                "departamento_id"  => f => f.DepartamentoId,
                "ciudad_id"        => f => f.MunicipioId,
                "created_at"       => f => (object?)f.CreatedAt ?? DateTime.MinValue,
                _                  => f => f.Name
            };
            return desc ? q.OrderByDescending(key) : q.OrderBy(key);
        }
    }
}
