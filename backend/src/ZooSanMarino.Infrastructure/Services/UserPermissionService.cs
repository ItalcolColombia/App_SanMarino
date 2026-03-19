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
        // Sin Guid no podemos resolver permisos (token expirado o sesión inválida)
        var userIdGuid = _currentUser.UserGuid;
        if (!userIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        // Verificar si es el super admin
        if (await IsSuperAdminAsync(userId))
        {
            // Super admin puede ver todos los países
            return await _context.Set<Pais>()
                .AsNoTracking()
                .Select(p => new PaisDto(p.PaisId, p.PaisNombre))
                .ToListAsync();
        }
        
        var paisIds = new HashSet<int>();
        var guid = userIdGuid.Value;

        // 1. Obtener países de las granjas asignadas al usuario
        var farmCountries = await _context.UserFarms
            .AsNoTracking()
            .Where(uf => uf.UserId == guid)
            .Join(_context.Set<Departamento>(),
                uf => uf.Farm.DepartamentoId,
                d => d.DepartamentoId,
                (uf, d) => d.PaisId)
            .Distinct()
            .ToListAsync();
        
        foreach (var paisId in farmCountries)
        {
            paisIds.Add(paisId);
        }
        
        // 2. Obtener países de las empresas asignadas al usuario (a través de CompanyPais)
        // Esto permite que usuarios sin granjas asignadas puedan crear granjas en países de sus empresas
        var userCompanyIds = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == guid)
            .Select(uc => uc.CompanyId)
            .Distinct()
            .ToListAsync();
        
        if (userCompanyIds.Any())
        {
            var companyPaises = await _context.CompanyPaises
                .AsNoTracking()
                .Where(cp => userCompanyIds.Contains(cp.CompanyId))
                .Select(cp => cp.PaisId)
                .Distinct()
                .ToListAsync();
            
            foreach (var paisId in companyPaises)
            {
                paisIds.Add(paisId);
            }
        }
        
        // Obtener los DTOs de países
        if (!paisIds.Any())
        {
            return new List<PaisDto>();
        }
        
        var paises = await _context.Set<Pais>()
            .AsNoTracking()
            .Where(p => paisIds.Contains(p.PaisId))
            .Select(p => new PaisDto(p.PaisId, p.PaisNombre))
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

        var userIdGuid = _currentUser.UserGuid;
        if (!userIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        // Obtener empresas asignadas al usuario
        var userCompanyIds = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userIdGuid.Value)
            .Select(uc => uc.CompanyId)
            .Distinct()
            .ToListAsync();

        // Si el usuario tiene empresas asignadas, verificar si alguna de esas empresas tiene el país
        if (userCompanyIds.Any())
        {
            var canCreate = await _context.CompanyPaises
                .AsNoTracking()
                .AnyAsync(cp => userCompanyIds.Contains(cp.CompanyId) && cp.PaisId == paisId);
            
            if (canCreate)
            {
                return true;
            }
        }

        // También verificar países asignados a través de granjas existentes
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

        // Verificar si el usuario es admin o administrador
        var isAdmin = await IsUserAdminOrAdministratorAsync(userId);
        if (isAdmin)
        {
            // Admin/Administrador puede ver todos los usuarios de todas las empresas
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

        var userIdGuid = _currentUser.UserGuid;
        if (!userIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        // Obtener las empresas asignadas al usuario actual
        var userCompanies = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == userIdGuid.Value)
            .Select(uc => uc.CompanyId)
            .ToListAsync();

        // Si no tiene empresas asignadas, retornar lista vacía
        if (!userCompanies.Any())
        {
            return new List<UserBasicDto>();
        }

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

        var currentUserIdGuid = _currentUser.UserGuid;
        if (!currentUserIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        // Verificar que ambos usuarios pertenecen a las mismas empresas
        var currentUserCompanies = await _context.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserId == currentUserIdGuid.Value)
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
    /// <summary>
    /// Verifica si el usuario tiene rol "admin" o "administrador" (case-insensitive)
    /// </summary>
    private async Task<bool> IsUserAdminOrAdministratorAsync(int userId)
    {
        var userIdGuid = _currentUser.UserGuid;
        if (!userIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        var userRoles = await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userIdGuid.Value)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        // Verificar si tiene rol "admin" o "administrador" (case-insensitive)
        return userRoles.Any(role => 
            !string.IsNullOrWhiteSpace(role) && 
            (role.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
             role.Equals("administrador", StringComparison.OrdinalIgnoreCase))
        );
    }

    private async Task<bool> IsSuperAdminAsync(int userId)
    {
        // Sin Guid la sesión es inválida (token expirado o sin claim sub)
        var userIdGuid = _currentUser.UserGuid;
        if (!userIdGuid.HasValue)
        {
            throw new UnauthorizedAccessException("Sesión expirada o inválida. Inicie sesión de nuevo.");
        }

        // Buscar el email del usuario
        var userEmail = await _context.UserLogins
            .AsNoTracking()
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userIdGuid.Value)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync();

        return userEmail?.ToLower() == "moiesbbuga@gmail.com";
    }
}
