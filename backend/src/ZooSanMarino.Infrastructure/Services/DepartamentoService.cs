// ZooSanMarino.Infrastructure/Services/DepartamentoService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class DepartamentoService : IDepartamentoService
{
    private readonly ZooSanMarinoContext _ctx;
    public DepartamentoService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<DepartamentoDto>> GetAllAsync()
        => await _ctx.Set<Departamento>()
            .Select(d => new DepartamentoDto(d.DepartamentoId, d.DepartamentoNombre, d.PaisId, d.Active))
            .ToListAsync();

    public async Task<IEnumerable<DepartamentoDto>> GetByPaisIdAsync(int paisId) // ⬅️
        => await _ctx.Set<Departamento>()
            .Where(d => d.PaisId == paisId)
            .Select(d => new DepartamentoDto(d.DepartamentoId, d.DepartamentoNombre, d.PaisId, d.Active))
            .ToListAsync();

    public async Task<DepartamentoDto?> GetByIdAsync(int id)
        => await _ctx.Set<Departamento>()
            .Where(d => d.DepartamentoId == id)
            .Select(d => new DepartamentoDto(d.DepartamentoId, d.DepartamentoNombre, d.PaisId, d.Active))
            .SingleOrDefaultAsync();

    public async Task<DepartamentoDto> CreateAsync(CreateDepartamentoDto dto)
    {
        var entity = new Departamento
        {
            DepartamentoNombre = dto.DepartamentoNombre,
            PaisId             = dto.PaisId,
            Active             = dto.Active
        };
        _ctx.Add(entity);
        await _ctx.SaveChangesAsync();
        return new DepartamentoDto(entity.DepartamentoId, entity.DepartamentoNombre, entity.PaisId, entity.Active);
    }

    public async Task<bool> UpdateAsync(UpdateDepartamentoDto dto)
    {
        var entity = await _ctx.Set<Departamento>().FindAsync(dto.DepartamentoId);
        if (entity is null) return false;
        entity.DepartamentoNombre = dto.DepartamentoNombre;
        entity.PaisId             = dto.PaisId;
        entity.Active             = dto.Active;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.Set<Departamento>().FindAsync(id);
        if (entity is null) return false;
        _ctx.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
