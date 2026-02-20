// src/ZooSanMarino.Infrastructure/Services/MasterListService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MasterListService : IMasterListService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    
    public MasterListService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<MasterListDto>> GetAllAsync(int? companyId = null, int? countryId = null)
    {
        // Usar companyId del parámetro o del usuario actual
        var effectiveCompanyId = companyId ?? _currentUser.CompanyId;
        var effectiveCountryId = countryId ?? _currentUser.PaisId;

        var query = _ctx.MasterLists
            .Include(ml => ml.Options)
            .AsQueryable();

        // Filtrar por CompanyId y CountryId si están disponibles
        if (effectiveCompanyId > 0)
        {
            query = query.Where(ml => ml.CompanyId == effectiveCompanyId || ml.CompanyId == null);
        }

        if (effectiveCountryId.HasValue && effectiveCountryId.Value > 0)
        {
            query = query.Where(ml => ml.CountryId == effectiveCountryId.Value || ml.CountryId == null);
        }

        return await query
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options.OrderBy(o => o.Order).Select(o => new MasterListOptionItemDto(o.Id, o.Value)),
                ml.Options.OrderBy(o => o.Order).Select(o => o.Value),
                ml.CompanyId,
                ml.CompanyName,
                ml.CountryId,
                ml.CountryName
            ))
            .ToListAsync();
    }

    public async Task<MasterListDto?> GetByIdAsync(int id) =>
        await _ctx.MasterLists
            .Include(ml => ml.Options)
            .Where(ml => ml.Id == id)
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options.OrderBy(o => o.Order).Select(o => new MasterListOptionItemDto(o.Id, o.Value)),
                ml.Options.OrderBy(o => o.Order).Select(o => o.Value),
                ml.CompanyId,
                ml.CompanyName,
                ml.CountryId,
                ml.CountryName
            ))
            .SingleOrDefaultAsync();

    public async Task<MasterListDto?> GetByKeyAsync(string key, int? companyId = null, int? countryId = null)
    {
        // Usar companyId del parámetro o del usuario actual
        var effectiveCompanyId = companyId ?? _currentUser.CompanyId;
        var effectiveCountryId = countryId ?? _currentUser.PaisId;

        var query = _ctx.MasterLists
            .Include(ml => ml.Options)
            .Where(ml => ml.Key == key)
            .AsQueryable();

        // Filtrar por CompanyId y CountryId si están disponibles
        if (effectiveCompanyId > 0)
        {
            query = query.Where(ml => ml.CompanyId == effectiveCompanyId || ml.CompanyId == null);
        }

        if (effectiveCountryId.HasValue && effectiveCountryId.Value > 0)
        {
            query = query.Where(ml => ml.CountryId == effectiveCountryId.Value || ml.CountryId == null);
        }

        return await query
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options.OrderBy(o => o.Order).Select(o => new MasterListOptionItemDto(o.Id, o.Value)),
                ml.Options.OrderBy(o => o.Order).Select(o => o.Value),
                ml.CompanyId,
                ml.CompanyName,
                ml.CountryId,
                ml.CountryName
            ))
            .SingleOrDefaultAsync();
    }

    public async Task<MasterListDto> CreateAsync(CreateMasterListDto dto)
    {
        // Obtener CompanyId del DTO o del usuario actual
        int? companyId = dto.CompanyId ?? (_currentUser.CompanyId > 0 ? _currentUser.CompanyId : null);
        int? countryId = dto.CountryId ?? _currentUser.PaisId;

        // Obtener nombres de compañía y país
        string? companyName = null;
        string? countryName = null;

        if (companyId.HasValue && companyId.Value > 0)
        {
            companyName = await _ctx.Companies
                .AsNoTracking()
                .Where(c => c.Id == companyId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
        }

        if (countryId.HasValue && countryId.Value > 0)
        {
            countryName = await _ctx.Set<Pais>()
                .AsNoTracking()
                .Where(p => p.PaisId == countryId.Value)
                .Select(p => p.PaisNombre)
                .FirstOrDefaultAsync();
        }

        var ml = new MasterList {
            Key  = dto.Key,
            Name = dto.Name,
            CompanyId = companyId,
            CompanyName = companyName,
            CountryId = countryId,
            CountryName = countryName
        };
        _ctx.MasterLists.Add(ml);
        await _ctx.SaveChangesAsync();

        // Insertar opciones
        var options = dto.Options
            .Select((value, idx) => new MasterListOption {
                MasterListId = ml.Id,
                Value        = value,
                Order        = idx
            }).ToList();
        _ctx.MasterListOptions.AddRange(options);
        await _ctx.SaveChangesAsync();

        var result = await GetByIdAsync(ml.Id);
        return result ?? throw new InvalidOperationException("Error al crear MasterList: no se pudo recuperar el registro creado");
    }

    public async Task<MasterListDto?> UpdateAsync(UpdateMasterListDto dto)
    {
        var ml = await _ctx.MasterLists
                    .Include(x => x.Options)
                    .SingleOrDefaultAsync(x => x.Id == dto.Id);
        if (ml is null) return null;

        ml.Key  = dto.Key;
        ml.Name = dto.Name;

        // Actualizar CompanyId y CountryId si se proporcionan
        if (dto.CompanyId.HasValue)
        {
            ml.CompanyId = dto.CompanyId.Value;
            // Obtener nombre de la compañía
            ml.CompanyName = await _ctx.Companies
                .AsNoTracking()
                .Where(c => c.Id == dto.CompanyId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
        }

        if (dto.CountryId.HasValue)
        {
            ml.CountryId = dto.CountryId.Value;
            // Obtener nombre del país
            ml.CountryName = await _ctx.Set<Pais>()
                .AsNoTracking()
                .Where(p => p.PaisId == dto.CountryId.Value)
                .Select(p => p.PaisNombre)
                .FirstOrDefaultAsync();
        }

        // Reemplazar opciones
        _ctx.MasterListOptions.RemoveRange(ml.Options);

        var options = dto.Options
            .Select((value, idx) => new MasterListOption {
                MasterListId = ml.Id,
                Value        = value,
                Order        = idx
            }).ToList();
        _ctx.MasterListOptions.AddRange(options);

        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ml.Id)!;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ml = await _ctx.MasterLists.FindAsync(id);
        if (ml is null) return false;
        _ctx.MasterLists.Remove(ml);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
