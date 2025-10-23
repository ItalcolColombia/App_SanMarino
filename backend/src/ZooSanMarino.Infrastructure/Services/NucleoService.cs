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

        public NucleoService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
        {
            _ctx = ctx;
            _current = current;
        }

        // ===========================
        // BÚSQUEDA AVANZADA
        // ===========================
        public async Task<CommonDtos.PagedResult<NucleoDetailDto>> SearchAsync(NucleoSearchRequest req)
        {
            var q = _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId);

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
        public async Task<IEnumerable<NucleoDto>> GetAllAsync() =>
            await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId && n.DeletedAt == null)
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .ToListAsync();

        public async Task<NucleoDto?> GetByIdAsync(string nucleoId, int granjaId) =>
            await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId &&
                            n.DeletedAt == null &&
                            n.NucleoId == nucleoId &&
                            n.GranjaId == granjaId)
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .SingleOrDefaultAsync();

        public async Task<IEnumerable<NucleoDto>> GetByGranjaAsync(int granjaId) =>
            await _ctx.Nucleos.AsNoTracking()
                .Where(n => n.CompanyId == _current.CompanyId &&
                            n.DeletedAt == null &&
                            n.GranjaId == granjaId)
                .Select(n => new NucleoDto(n.NucleoId, n.GranjaId, n.NucleoNombre))
                .ToListAsync();

        public async Task<NucleoDto> CreateAsync(CreateNucleoDto dto)
        {
            await EnsureFarmExists(dto.GranjaId);

            var dup = await _ctx.Nucleos.AsNoTracking()
                .AnyAsync(n => n.CompanyId == _current.CompanyId &&
                               n.NucleoId == dto.NucleoId &&
                               n.GranjaId == dto.GranjaId);
            if (dup) throw new InvalidOperationException("Ya existe un Núcleo con ese Id para la granja.");

            var ent = new Nucleo
            {
                NucleoId        = dto.NucleoId,
                GranjaId        = dto.GranjaId,
                NucleoNombre    = dto.NucleoNombre,
                CompanyId       = _current.CompanyId,
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
            var exists = await _ctx.Farms.AsNoTracking()
                .AnyAsync(f => f.Id == granjaId && f.CompanyId == _current.CompanyId);
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
