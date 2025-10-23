using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public class UserPermissionService : IUserPermissionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public UserPermissionService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<PaisDto>> GetAssignedCountriesAsync(int userId)
    {
        // Verificar si es el super admin
        if (await IsSuperAdminAsync(userId))
        {
            // Super admin puede ver todos los países
            return await _context.Set<Pais>()
                .AsNoTracking()
                .Select(p => new PaisDto(p.PaisId, p.PaisNombre))
                .ToListAsync();
        }

        // Convertir userId de int a Guid para la consulta
        var userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        
        // Obtener los países de las granjas asignadas al usuario
        var countries = await _context.UserFarms
            .AsNoTracking()
            .Where(uf => uf.UserId == userIdGuid)
            .Select(uf => new { uf.Farm.DepartamentoId })
            .Distinct()
            .ToListAsync();

        var departamentoIds = countries.Select(c => c.DepartamentoId).ToList();
        
        var paises = await _context.Set<Departamento>()
            .AsNoTracking()
            .Where(d => departamentoIds.Contains(d.DepartamentoId))
            .Select(d => new PaisDto(d.PaisId, d.Pais.PaisNombre))
            .Distinct()
            .ToListAsync();

        return paises;
    }

    public async Task<bool> CanCreateFarmInCountryAsync(int userId, int paisId)
    {
        // Verificar si es el super admin
        if (await IsSuperAdminAsync(userId))
        {
            return true; // Super admin puede crear granjas en cualquier país
        }

        var assignedCountries = await GetAssignedCountriesAsync(userId);
        return assignedCountries.Any(c => c.PaisId == paisId);
    }

    public async Task<IEnumerable<UserBasicDto>> GetUsersFromAssignedCompaniesAsync(int userId)
    {
        // Verificar si es el super admin
        if (await IsSuperAdminAsync(userId))
        {
            // Super admin puede ver todos los usuarios
            return await _context.Users
                .AsNoTracking()
                .Select(u => new UserBasicDto(
                    u.Id,
                    u.surName,
                    u.firstName,
                    u.cedula,
                    u.telefono,
                    u.ubicacion,
                    u.IsActive,
                    u.IsLocked,
                    u.CreatedAt,
                    u.LastLoginAt
                ))
                .ToListAsync();
        }

        // Convertir userId de int a Guid para la consulta
        var userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        
        // Obtener las empresas asignadas al usuario actual
        var userCompanies = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userIdGuid)
            .Select(uc => uc.CompanyId)
            .ToListAsync();

        // Obtener todos los usuarios que pertenecen a esas empresas
        var users = await _context.UserCompanies
            .AsNoTracking()
            .Include(uc => uc.User)
            .Where(uc => userCompanies.Contains(uc.CompanyId))
            .Select(uc => new UserBasicDto(
                uc.User.Id,
                uc.User.surName,
                uc.User.firstName,
                uc.User.cedula,
                uc.User.telefono,
                uc.User.ubicacion,
                uc.User.IsActive,
                uc.User.IsLocked,
                uc.User.CreatedAt,
                uc.User.LastLoginAt
            ))
            .Distinct()
            .ToListAsync();

        return users;
    }

    public async Task<bool> CanAssignUserToFarmAsync(int currentUserId, Guid targetUserId)
    {
        // Verificar si el usuario actual es super admin
        if (await IsSuperAdminAsync(currentUserId))
        {
            return true; // Super admin puede asignar cualquier usuario
        }

        // Convertir currentUserId de int a Guid para la consulta
        var currentUserIdGuid = new Guid(currentUserId.ToString("D32").PadLeft(32, '0'));
        
        // Verificar que ambos usuarios pertenecen a las mismas empresas
        var currentUserCompanies = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == currentUserIdGuid)
            .Select(uc => uc.CompanyId)
            .ToListAsync();

        var targetUserCompanies = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == targetUserId)
            .Select(uc => uc.CompanyId)
            .ToListAsync();

        // El usuario actual puede asignar si comparten al menos una empresa
        return currentUserCompanies.Any(c => targetUserCompanies.Contains(c));
    }

    /// <summary>
    /// Verifica si el usuario es el super admin (moiesbbuga@gmail.com)
    /// </summary>
    private async Task<bool> IsSuperAdminAsync(int userId)
    {
        // Convertir userId de int a Guid para la consulta
        var userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        
        // Buscar el email del usuario
        var userEmail = await _context.UserLogins
            .AsNoTracking()
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userIdGuid)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync();

        return userEmail?.ToLower() == "moiesbbuga@gmail.com";
    }
}
