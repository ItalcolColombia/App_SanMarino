// file: src/ZooSanMarino.Infrastructure/Services/NucleoService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.DTOs;                    // NucleoDto, Create/Update
using NucleoDtos   = ZooSanMarino.Application.DTOs.Nucleos;
using AppInterfaces = ZooSanMarino.Application.Interfaces; // INucleoService, ICurrentUser
using CommonDtos   = ZooSanMarino.Application.DTOs.Common; // PagedResult<>

using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.DTOs.Nucleos;

namespace ZooSanMarino.Infrastructure.Services
{
    public class NucleoService : AppInterfaces.INucleoService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly AppInterfaces.ICurrentUser _current;
        private readonly AppInterfaces.ICompanyResolver _companyResolver;
        private readonly AppInterfaces.IUserPermissionService _userPermissionService;

        public NucleoService(
            ZooSanMarinoContext ctx, 
            AppInterfaces.ICurrentUser current,
            AppInterfaces.ICompanyResolver companyResolver,
            AppInterfaces.IUserPermissionService userPermissionService)
        {
            _ctx = ctx;
            _current = current;
            _companyResolver = companyResolver;
            _userPermissionService = userPermissionService;
        }

        /// <summary>
        /// Obtiene el CompanyId efectivo basado en el header o token JWT
        /// </summary>
        private async Task<int> GetEffectiveCompanyIdAsync()
        {
            // Si hay una empresa activa especificada en el header, usarla
            if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            {
                var companyId = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
                if (companyId.HasValue)
                {
                    return companyId.Value;
                }
            }

            // Fallback al CompanyId del token JWT
            return _current.CompanyId;
        }

        /// <summary>
        /// Verifica si el usuario es admin o administrador
        /// </summary>
        private async Task<bool> IsUserAdminOrAdministratorAsync(int userId)
        {
            var userIdGuid = _current.UserGuid;
            if (!userIdGuid.HasValue)
            {
                userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
            }
            
            var userRoles = await _ctx.UserRoles
                .AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userIdGuid.Value)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            return userRoles.Any(role => 
                !string.IsNullOrWhiteSpace(role) && 
                (role.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
                 role.Equals("administrador", StringComparison.OrdinalIgnoreCase))
            );
        }

        /// <summary>
        /// Verifica si el usuario es super admin
        /// </summary>
        private async Task<bool> IsSuperAdminAsync(int userId)
        {
            var userIdGuid = _current.UserGuid;
            if (!userIdGuid.HasValue)
            {
                userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
            }
            
            var userEmail = await _ctx.UserLogins
                .AsNoTracking()
                .Include(ul => ul.Login)
                .Where(ul => ul.UserId == userIdGuid.Value)
                .Select(ul => ul.Login.email)
                .FirstOrDefaultAsync();

            return userEmail?.ToLower() == "moiesbbuga@gmail.com";
        }

        // ===========================
        // BÚSQUEDA AVANZADA
        // ===========================
        public async Task<CommonDtos.PagedResult<NucleoDetailDto>> SearchAsync(NucleoSearchRequest req)
        {
            IQueryable<Nucleo> q = _ctx.Nucleos.AsNoTracking();

            // Verificar si el usuario es admin/administrador
            var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
            var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
            var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                         await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                         await IsSuperAdminAsync(_current.UserId);

            if (isAdmin)
            {
                Console.WriteLine($"=== NucleoService.SearchAsync - Usuario es admin/administrador, mostrando TODOS los núcleos ===");
                // No filtrar por empresa - mostrar todos los núcleos
            }
            else
            {
                // Si NO es admin, filtrar solo por los núcleos de su empresa
                var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
                Console.WriteLine($"=== NucleoService.SearchAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
                q = q.Where(n => n.CompanyId == effectiveCompanyId);
            }

            if (req.SoloActivos) q = q.Where(n => n.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim().ToLower();
                q = q.Where(n => n.NucleoId.ToLower().Contains(term) ||
                                 n.NucleoNombre.ToLower().Contains(term));
            }

            if (req.GranjaId.HasValue)
                q = q.Where(n => n.GranjaId == req.GranjaId.Value);

            q = ApplyOrder(q, req.SortBy, req.SortDesc);

            var total = await q.LongCountAsync();
            var items = await ProjectToDetail(q)
                .Skip((req.Page - 1) * req.PageSize)
                .Take(req.PageSize)
                .ToListAsync();

            return new CommonDtos.PagedResult<NucleoDetailDto>
            {
                Page = req.Page,
                PageSize = req.PageSize,
                Total = total,
                Items = items
            };
        }

        // ===========================
        // DETALLE POR PK COMPUESTA
        // ===========================
        public async Task<NucleoDetailDto?> GetDetailByIdAsync(string nucleoId, int granjaId)
        {
            var q = _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId &&
                            n.NucleoId == nucleoId &&
                            n.GranjaId == granjaId &&
                            n.DeletedAt == null);

            return await ProjectToDetail(q).SingleOrDefaultAsync();
        }

        // ===========================
        // COMPAT
        // ===========================
        public async Task<IEnumerable<NucleoDto>> GetAllAsync()
        {
            IQueryable<Nucleo> q = _ctx.Nucleos.AsNoTracking().Where(n => n.DeletedAt == null);

            // Verificar si el usuario es admin/administrador
            var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
            var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
            var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                         await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                         await IsSuperAdminAsync(_current.UserId);

            if (isAdmin)
            {
                Console.WriteLine($"=== NucleoService.GetAllAsync - Usuario es admin/administrador, mostrando TODOS los núcleos ===");
                // No filtrar por empresa - mostrar todos los núcleos
            }
            else
            {
                // Si NO es admin, filtrar solo por los núcleos de su empresa
                var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
                Console.WriteLine($"=== NucleoService.GetAllAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
                q = q.Where(n => n.CompanyId == effectiveCompanyId);
            }

            return await q
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .ToListAsync();
        }

        public async Task<NucleoDto?> GetByIdAsync(string nucleoId, int granjaId) =>
            await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId &&
                            n.DeletedAt == null &&
                            n.NucleoId == nucleoId &&
                            n.GranjaId == granjaId)
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .SingleOrDefaultAsync();

        public async Task<IEnumerable<NucleoDto>> GetByGranjaAsync(int granjaId)
        {
            IQueryable<Nucleo> q = _ctx.Nucleos.AsNoTracking()
                .Where(n => n.DeletedAt == null && n.GranjaId == granjaId);

            // Verificar si el usuario es admin/administrador
            var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
            var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
            var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                         await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                         await IsSuperAdminAsync(_current.UserId);

            if (!isAdmin)
            {
                // Si NO es admin, filtrar solo por los núcleos de su empresa
                var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
                q = q.Where(n => n.CompanyId == effectiveCompanyId);
            }

            return await q
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .ToListAsync();
        }

        public async Task<NucleoDto> CreateAsync(CreateNucleoDto dto)
        {
            await EnsureFarmExists(dto.GranjaId);

            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            var dup = await _ctx.Nucleos.AsNoTracking()
                .AnyAsync(n => n.CompanyId == effectiveCompanyId &&
                               n.NucleoId == dto.NucleoId &&
                               n.GranjaId == dto.GranjaId);
            if (dup) throw new InvalidOperationException("Ya existe un Núcleo con ese Id para la granja.");

            var ent = new Nucleo
            {
                NucleoId        = dto.NucleoId,
                GranjaId        = dto.GranjaId,
                NucleoNombre    = dto.NucleoNombre,
                CompanyId       = effectiveCompanyId,
                CreatedByUserId = _current.UserId,
                CreatedAt       = DateTime.UtcNow
            };

            _ctx.Nucleos.Add(ent);
            await _ctx.SaveChangesAsync();

            return new NucleoDto(ent.NucleoId, ent.GranjaId, ent.NucleoNombre);
        }

        public async Task<NucleoDto?> UpdateAsync(UpdateNucleoDto dto)
        {
            var ent = await _ctx.Nucleos
                .SingleOrDefaultAsync(n => n.CompanyId == _current.CompanyId &&
                                           n.NucleoId == dto.NucleoId &&
                                           n.GranjaId == dto.GranjaId);

            if (ent is null || ent.DeletedAt != null) return null;

            ent.NucleoNombre    = dto.NucleoNombre;
            ent.UpdatedByUserId = _current.UserId;
            ent.UpdatedAt       = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return new NucleoDto(ent.NucleoId, ent.GranjaId, ent.NucleoNombre);
        }

        public async Task<bool> DeleteAsync(string nucleoId, int granjaId)
        {
            var ent = await _ctx.Nucleos
                .SingleOrDefaultAsync(n => n.CompanyId == _current.CompanyId &&
                                           n.NucleoId == nucleoId &&
                                           n.GranjaId == granjaId);
            if (ent is null || ent.DeletedAt != null) return false;

            ent.DeletedAt       = DateTime.UtcNow;
            ent.UpdatedByUserId = _current.UserId;
            ent.UpdatedAt       = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HardDeleteAsync(string nucleoId, int granjaId)
        {
            var ent = await _ctx.Nucleos
                .SingleOrDefaultAsync(n => n.CompanyId == _current.CompanyId &&
                                           n.NucleoId == nucleoId &&
                                           n.GranjaId == granjaId);
            if (ent is null) return false;

            _ctx.Nucleos.Remove(ent);
            await _ctx.SaveChangesAsync();
            return true;
        }

        // ===========================
        // Helpers
        // ===========================
        private async Task EnsureFarmExists(int granjaId)
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            var exists = await _ctx.Farms.AsNoTracking()
                .AnyAsync(f => f.Id == granjaId && f.CompanyId == effectiveCompanyId);
            if (!exists) throw new InvalidOperationException("La granja no existe o no pertenece a la compañía.");
        }

        // Nota: método de instancia (no static) para poder usar _ctx en subconsultas
        private IQueryable<NucleoDetailDto> ProjectToDetail(IQueryable<Nucleo> q)
        {
            return q
                .Include(n => n.Farm)
                .Select(n => new NucleoDetailDto(
                    n.NucleoId,
                    n.GranjaId,
                    n.NucleoNombre,
                    n.CompanyId,
                    n.CreatedByUserId,
                    n.CreatedAt,
                    n.UpdatedByUserId,
                    n.UpdatedAt,
                    new FarmLiteDto(n.Farm.Id, n.Farm.Name, n.Farm.RegionalId, n.Farm.DepartamentoId,n.Farm.MunicipioId),
                    // Contadores posicionales (sin argumentos nombrados)
                    n.Galpones.Count(), // requiere Nucleo.Galpones
                    _ctx.Lotes.Count(l =>                // si Nucleo no tiene Lotes, usamos subconsulta
                        l.CompanyId == n.CompanyId &&
                        l.GranjaId  == n.GranjaId  &&
                        l.NucleoId  == n.NucleoId &&
                        l.DeletedAt == null)
                ));
        }

        private static IQueryable<Nucleo> ApplyOrder(IQueryable<Nucleo> q, string sortBy, bool desc)
        {
            Expression<Func<Nucleo, object>> key = sortBy?.ToLower() switch
            {
                "nucleo_id"     => n => n.NucleoId,
                "granja_id"     => n => n.GranjaId,
                "nucleo_nombre" => n => n.NucleoNombre,
                _               => n => n.NucleoNombre
            };
            return desc ? q.OrderByDescending(key) : q.OrderBy(key);
        }
    }
}
