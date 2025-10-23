// ZooSanMarino.Infrastructure/Services/MunicipioService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MunicipioService : IMunicipioService
{
    private readonly ZooSanMarinoContext _ctx;
    public MunicipioService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<MunicipioDto>> GetAllAsync()
        => await _ctx.Set<Municipio>()
            .Select(m => new MunicipioDto(m.MunicipioId, m.MunicipioNombre, m.DepartamentoId))
            .ToListAsync();

    public async Task<IEnumerable<MunicipioDto>> GetByDepartamentoIdAsync(int departamentoId) // ⬅️
        => await _ctx.Set<Municipio>()
            .Where(m => m.DepartamentoId == departamentoId)
            .Select(m => new MunicipioDto(m.MunicipioId, m.MunicipioNombre, m.DepartamentoId))
            .ToListAsync();

    public async Task<MunicipioDto?> GetByIdAsync(int id)
        => await _ctx.Set<Municipio>()
            .Where(m => m.MunicipioId == id)
            .Select(m => new MunicipioDto(m.MunicipioId, m.MunicipioNombre, m.DepartamentoId))
            .SingleOrDefaultAsync();

    public async Task<MunicipioDto> CreateAsync(CreateMunicipioDto dto)
    {
        var entity = new Municipio { MunicipioNombre = dto.MunicipioNombre, DepartamentoId = dto.DepartamentoId };
        _ctx.Add(entity);
        await _ctx.SaveChangesAsync();
        return new MunicipioDto(entity.MunicipioId, entity.MunicipioNombre, entity.DepartamentoId);
    }

    public async Task<bool> UpdateAsync(UpdateMunicipioDto dto)
    {
        var entity = await _ctx.Set<Municipio>().FindAsync(dto.MunicipioId);
        if (entity is null) return false;
        entity.MunicipioNombre = dto.MunicipioNombre;
        entity.DepartamentoId  = dto.DepartamentoId;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.Set<Municipio>().FindAsync(id);
        if (entity is null) return false;
        _ctx.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
