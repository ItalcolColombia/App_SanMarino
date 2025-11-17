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
    public MasterListService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<MasterListDto>> GetAllAsync() =>
        await _ctx.MasterLists
            .Include(ml => ml.Options)
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options
                  .OrderBy(o => o.Order)
                  .Select(o => o.Value)
            ))
            .ToListAsync();

    public async Task<MasterListDto?> GetByIdAsync(int id) =>
        await _ctx.MasterLists
            .Include(ml => ml.Options)
            .Where(ml => ml.Id == id)
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options
                  .OrderBy(o => o.Order)
                  .Select(o => o.Value)
            ))
            .SingleOrDefaultAsync();

    public async Task<MasterListDto?> GetByKeyAsync(string key) =>
        await _ctx.MasterLists
            .Include(ml => ml.Options)
            .Where(ml => ml.Key == key)
            .Select(ml => new MasterListDto(
                ml.Id,
                ml.Key,
                ml.Name,
                ml.Options
                  .OrderBy(o => o.Order)
                  .Select(o => o.Value)
            ))
            .SingleOrDefaultAsync();

    public async Task<MasterListDto> CreateAsync(CreateMasterListDto dto)
    {
        var ml = new MasterList {
            Key  = dto.Key,
            Name = dto.Name
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
