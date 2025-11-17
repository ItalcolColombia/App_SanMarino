using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly ZooSanMarinoContext _ctx;
    public RoleService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<RoleDto>> GetAllAsync() =>
        await _ctx.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus) // 👈 incluir menús
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.RolePermissions.Select(rp => rp.Permission.Key).ToArray(),
                r.RoleCompanies.Select(rc => rc.CompanyId).ToArray(),
                r.RoleMenus.Select(rm => rm.MenuId).ToArray() // 👈 proyectar MenuIds
            ))
            .ToListAsync();

    public async Task<RoleDto?> GetByIdAsync(int id) =>
        await _ctx.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus) // 👈 incluir menús
            .Where(r => r.Id == id)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.RolePermissions.Select(rp => rp.Permission.Key).ToArray(),
                r.RoleCompanies.Select(rc => rc.CompanyId).ToArray(),
                r.RoleMenus.Select(rm => rm.MenuId).ToArray() // 👈 proyectar MenuIds
            ))
            .SingleOrDefaultAsync();

    public async Task<RoleDto> CreateAsync(CreateRoleDto dto)
    {
        var name = dto.Name?.Trim() ?? throw new InvalidOperationException("El nombre del rol es requerido.");
        var permKeys = (dto.Permissions ?? Array.Empty<string>())
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .ToArray();
        var companyIds = (dto.CompanyIds ?? Array.Empty<int>())
            .Distinct()
            .ToArray();

        // Unicidad del nombre (global)
        var exists = await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Name == name);
        if (exists) throw new InvalidOperationException($"Ya existe un rol con el nombre '{name}'.");

        // Validación de compañías
        var existingCompanies = await _ctx.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var missingCompanies = companyIds.Except(existingCompanies).ToArray();
        if (missingCompanies.Length > 0)
            throw new InvalidOperationException($"Las compañías no existen: {string.Join(", ", missingCompanies)}");

        // Validación de permisos
        var permMap = await _ctx.Permissions
            .AsNoTracking()
            .Where(p => permKeys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id);

        var missingPerms = permKeys.Where(k => !permMap.ContainsKey(k)).ToArray();
        if (missingPerms.Length > 0)
            throw new InvalidOperationException($"Permisos inexistentes: {string.Join(", ", missingPerms)}");

        using var tx = await _ctx.Database.BeginTransactionAsync();

        var role = new Role { Name = name };
        _ctx.Roles.Add(role);
        await _ctx.SaveChangesAsync();

        if (permKeys.Length > 0)
        {
            var rolePerms = permKeys.Select(k => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permMap[k]
            });
            _ctx.RolePermissions.AddRange(rolePerms);
        }

        if (companyIds.Length > 0)
        {
            var roleCompanies = companyIds.Select(cid => new RoleCompany
            {
                RoleId = role.Id,
                CompanyId = cid
            });
            _ctx.RoleCompanies.AddRange(roleCompanies);
        }

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        var result = await GetByIdAsync(role.Id);
        if (result is null) throw new InvalidOperationException("No se pudo recuperar el rol tras crearlo.");
        return result;
    }

    public async Task<RoleDto?> UpdateAsync(UpdateRoleDto dto)
    {
        var role = await _ctx.Roles
            .Include(r => r.RolePermissions)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus) // 👈 mantener cargado (aunque no se editen aquí)
            .SingleOrDefaultAsync(r => r.Id == dto.Id);

        if (role is null) return null;

        var name = dto.Name?.Trim() ?? throw new InvalidOperationException("El nombre del rol es requerido.");
        var permKeys = (dto.Permissions ?? Array.Empty<string>())
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .ToArray();
        var companyIds = (dto.CompanyIds ?? Array.Empty<int>())
            .Distinct()
            .ToArray();

        // Unicidad del nombre excluyendo el mismo rol
        var nameTaken = await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Name == name && r.Id != role.Id);
        if (nameTaken) throw new InvalidOperationException($"Ya existe un rol con el nombre '{name}'.");

        // Validación de compañías
        var existingCompanies = await _ctx.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var missingCompanies = companyIds.Except(existingCompanies).ToArray();
        if (missingCompanies.Length > 0)
            throw new InvalidOperationException($"Las compañías no existen: {string.Join(", ", missingCompanies)}");

        // Validación de permisos
        var permMap = await _ctx.Permissions
            .AsNoTracking()
            .Where(p => permKeys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id);

        var missingPerms = permKeys.Where(k => !permMap.ContainsKey(k)).ToArray();
        if (missingPerms.Length > 0)
            throw new InvalidOperationException($"Permisos inexistentes: {string.Join(", ", missingPerms)}");

        using var tx = await _ctx.Database.BeginTransactionAsync();

        role.Name = name;

        // Reemplazar permisos
        _ctx.RolePermissions.RemoveRange(role.RolePermissions);
        if (permKeys.Length > 0)
        {
            var newPermissions = permKeys.Select(k => new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permMap[k]
            });
            _ctx.RolePermissions.AddRange(newPermissions);
        }

        // Reemplazar asociaciones con empresas
        _ctx.RoleCompanies.RemoveRange(role.RoleCompanies);
        if (companyIds.Length > 0)
        {
            var newCompanies = companyIds.Select(cid => new RoleCompany
            {
                RoleId = role.Id,
                CompanyId = cid
            });
            _ctx.RoleCompanies.AddRange(newCompanies);
        }

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        return await GetByIdAsync(role.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var role = await _ctx.Roles.FindAsync(id);
        if (role is null) return false;

        _ctx.Roles.Remove(role);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<RoleDto?> AddPermissionsAsync(int roleId, string[] permissionKeys)
    {
        var role = await _ctx.Roles
            .Include(r => r.RolePermissions)
            .Include(r => r.RoleMenus) // 👈 por si luego proyectamos
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var keys = (permissionKeys ?? Array.Empty<string>())
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!) // non-null
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToArray();

        var permMap = await _ctx.Permissions
            .AsNoTracking()
            .Where(p => keys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id);

        var missing = keys.Where(k => !permMap.ContainsKey(k)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Permisos inexistentes: {string.Join(", ", missing)}");

        using var tx = await _ctx.Database.BeginTransactionAsync();

        // Agregar solo los que no estén
        var currentSet = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var toAdd = keys
            .Select(k => permMap[k])
            .Where(pid => !currentSet.Contains(pid))
            .Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid });

        _ctx.RolePermissions.AddRange(toAdd);
        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        return await GetByIdAsync(role.Id);
    }

    public async Task<RoleDto?> RemovePermissionsAsync(int roleId, string[] permissionKeys)
    {
        var role = await _ctx.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleMenus) // 👈 por si luego proyectamos
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var keys = (permissionKeys ?? Array.Empty<string>())
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!) // non-null
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToArray();

        using var tx = await _ctx.Database.BeginTransactionAsync();

        var toRemove = role.RolePermissions
            .Where(rp => keys.Contains(rp.Permission.Key.ToLower()))
            .ToList();

        if (toRemove.Count > 0)
        {
            _ctx.RolePermissions.RemoveRange(toRemove);
            await _ctx.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return await GetByIdAsync(role.Id);
    }

    public async Task<RoleDto?> ReplacePermissionsAsync(int roleId, string[] permissionKeys)
    {
        var role = await _ctx.Roles
            .Include(r => r.RolePermissions)
            .Include(r => r.RoleMenus) // 👈 por si luego proyectamos
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var keys = (permissionKeys ?? Array.Empty<string>())
            .Select(k => k?.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!) // non-null
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToArray();

        var permMap = await _ctx.Permissions
            .AsNoTracking()
            .Where(p => keys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Id);

        var missing = keys.Where(k => !permMap.ContainsKey(k)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Permisos inexistentes: {string.Join(", ", missing)}");

        using var tx = await _ctx.Database.BeginTransactionAsync();

        _ctx.RolePermissions.RemoveRange(role.RolePermissions);

        var newLinks = keys.Select(k => new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permMap[k]
        });

        _ctx.RolePermissions.AddRange(newLinks);
        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        return await GetByIdAsync(role.Id);
    }
}
