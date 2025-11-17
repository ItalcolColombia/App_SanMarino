// src/ZooSanMarino.Infrastructure/Services/RegionalService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class RegionalService : IRegionalService
{
    private readonly ZooSanMarinoContext _ctx;
    public RegionalService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<RegionalDto>> GetAllAsync() =>
        await _ctx.Regionales
            .Select(x => new RegionalDto(
                x.RegionalCia,
                x.RegionalId,
                x.RegionalNombre,
                x.RegionalEstado,
                x.RegionalCodigo))
            .ToListAsync();

    public async Task<RegionalDto?> GetByIdAsync(int cia, int id) =>
        await _ctx.Regionales
            .Where(x => x.RegionalCia == cia && x.RegionalId == id)
            .Select(x => new RegionalDto(
                x.RegionalCia,
                x.RegionalId,
                x.RegionalNombre,
                x.RegionalEstado,
                x.RegionalCodigo))
            .SingleOrDefaultAsync();

    public async Task<RegionalDto> CreateAsync(CreateRegionalDto dto)
    {
        var ent = new Regional {
            RegionalCia    = dto.RegionalCia,
            RegionalId     = dto.RegionalId,
            RegionalNombre = dto.RegionalNombre,
            RegionalEstado = dto.RegionalEstado,
            RegionalCodigo = dto.RegionalCodigo
        };
        _ctx.Regionales.Add(ent);
        await _ctx.SaveChangesAsync();
        var result = await GetByIdAsync(ent.RegionalCia, ent.RegionalId);
        return result ?? throw new InvalidOperationException("Error al crear Regional: no se pudo recuperar el registro creado");
    }

    public async Task<RegionalDto?> UpdateAsync(UpdateRegionalDto dto)
    {
        var ent = await _ctx.Regionales.FindAsync(dto.RegionalCia, dto.RegionalId);
        if (ent is null) return null;
        ent.RegionalNombre = dto.RegionalNombre;
        ent.RegionalEstado = dto.RegionalEstado;
        ent.RegionalCodigo = dto.RegionalCodigo;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.RegionalCia, ent.RegionalId)!;
    }

    public async Task<bool> DeleteAsync(int cia, int id)
    {
        var ent = await _ctx.Regionales.FindAsync(cia, id);
        if (ent is null) return false;
        _ctx.Regionales.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
