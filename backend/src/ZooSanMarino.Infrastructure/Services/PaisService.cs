// src/ZooSanMarino.Infrastructure/Services/PaisService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class PaisService : IPaisService
{
    private readonly ZooSanMarinoContext _ctx;
    public PaisService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<PaisDto>> GetAllAsync() =>
        await _ctx.Paises
            .Select(x => new PaisDto(x.PaisId, x.PaisNombre))
            .ToListAsync();

    public async Task<PaisDto?> GetByIdAsync(int id) =>
        await _ctx.Paises
            .Where(x => x.PaisId == id)
            .Select(x => new PaisDto(x.PaisId, x.PaisNombre))
            .SingleOrDefaultAsync();

    public async Task<PaisDto> CreateAsync(CreatePaisDto dto)
    {
        var ent = new Pais {
            PaisNombre = dto.PaisNombre
        };
        _ctx.Paises.Add(ent);
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.PaisId)!;
    }

    public async Task<PaisDto?> UpdateAsync(UpdatePaisDto dto)
    {
        var ent = await _ctx.Paises.FindAsync(dto.PaisId);
        if (ent is null) return null;
        ent.PaisNombre = dto.PaisNombre;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.PaisId)!;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ent = await _ctx.Paises.FindAsync(id);
        if (ent is null) return false;
        _ctx.Paises.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
