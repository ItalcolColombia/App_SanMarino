using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly ZooSanMarinoContext _ctx;
    public PermissionService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<PermissionDto>> GetAllAsync() =>
        await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description))
            .ToListAsync();

    public async Task<string[]> GetAllKeysAsync() =>
        await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => p.Key)
            .ToArrayAsync();

    public async Task<PermissionDto?> GetByIdAsync(int id) =>
        await _ctx.Permissions
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description))
            .SingleOrDefaultAsync();

    public async Task<PermissionDto> CreateAsync(CreatePermissionDto dto)
    {
        var key = (dto.Key ?? throw new InvalidOperationException("Key es requerida."))
                  .Trim().ToLowerInvariant();
        var desc = dto.Description?.Trim();

        var exists = await _ctx.Permissions.AsNoTracking().AnyAsync(p => p.Key == key);
        if (exists) throw new InvalidOperationException($"Ya existe un permiso con key '{key}'.");

        var entity = new Domain.Entities.Permission { Key = key, Description = desc ?? string.Empty };
        _ctx.Permissions.Add(entity);
        await _ctx.SaveChangesAsync();

        return new PermissionDto(entity.Id, entity.Key, entity.Description);
    }

    public async Task<PermissionDto?> UpdateAsync(UpdatePermissionDto dto)
    {
        var entity = await _ctx.Permissions.SingleOrDefaultAsync(p => p.Id == dto.Id);
        if (entity is null) return null;

        var key = (dto.Key ?? throw new InvalidOperationException("Key es requerida."))
                  .Trim().ToLowerInvariant();
        var desc = dto.Description?.Trim();

        var taken = await _ctx.Permissions.AsNoTracking()
                        .AnyAsync(p => p.Key == key && p.Id != dto.Id);
        if (taken) throw new InvalidOperationException($"Ya existe un permiso con key '{key}'.");

        entity.Key = key;
        entity.Description = desc ?? string.Empty;

        await _ctx.SaveChangesAsync();
        return new PermissionDto(entity.Id, entity.Key, entity.Description);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Restringimos eliminar si está asignado a algún rol (puedes cambiar a cascade si lo prefieres)
        var assigned = await _ctx.RolePermissions.AsNoTracking().AnyAsync(rp => rp.PermissionId == id);
        if (assigned) throw new InvalidOperationException("No se puede eliminar: el permiso está asignado a uno o más roles.");

        var entity = await _ctx.Permissions.FindAsync(id);
        if (entity is null) return false;

        _ctx.Permissions.Remove(entity);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
