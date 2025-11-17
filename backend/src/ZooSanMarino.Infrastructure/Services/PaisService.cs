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
        var result = await GetByIdAsync(ent.PaisId);
        return result ?? throw new InvalidOperationException("Error al crear País: no se pudo recuperar el registro creado");
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

        // Validar que no haya empresas asignadas a este país
        var hasCompanies = await _ctx.CompanyPaises
            .AnyAsync(cp => cp.PaisId == id);

        if (hasCompanies)
        {
            throw new InvalidOperationException(
                "No se puede eliminar el país porque tiene empresas asignadas. " +
                "Por favor, remueva las empresas del país antes de eliminarlo.");
        }

        // Validar que no haya usuarios asignados a este país
        var hasUsers = await _ctx.UserCompanies
            .AnyAsync(uc => uc.PaisId == id);

        if (hasUsers)
        {
            throw new InvalidOperationException(
                "No se puede eliminar el país porque tiene usuarios asignados. " +
                "Por favor, remueva las asignaciones de usuarios antes de eliminarlo.");
        }

        _ctx.Paises.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
