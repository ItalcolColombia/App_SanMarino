// src/ZooSanMarino.Infrastructure/Services/UserService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IPasswordHasher<Login> _hasher;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPermissionService _userPermissionService;

    public UserService(ZooSanMarinoContext ctx, IPasswordHasher<Login> hasher, ICurrentUser currentUser, IUserPermissionService userPermissionService)
    {
        _ctx = ctx;
        _hasher = hasher;
        _currentUser = currentUser;
        _userPermissionService = userPermissionService;
    }

    // ─────────────────────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────────────────────
    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        // Validaciones de entrada
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new InvalidOperationException("El email es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            throw new InvalidOperationException("La contraseña debe tener al menos 6 caracteres.");

        var companyIds = (dto.CompanyIds ?? Array.Empty<int>()).Distinct().ToArray();
        var roleIds    = (dto.RoleIds    ?? Array.Empty<int>()).Distinct().ToArray();
        var farmIds    = (dto.FarmIds    ?? Array.Empty<int>()).Distinct().ToArray();

        // (Opcional) exigir al menos una empresa y un rol para alta
        if (companyIds.Length == 0)
            throw new InvalidOperationException("Debes asignar al menos una compañía al usuario.");
        if (roleIds.Length == 0)
            throw new InvalidOperationException("Debes asignar al menos un rol al usuario.");

        // Unicidad de email
        var emailExists = await _ctx.Logins.AsNoTracking().AnyAsync(l => l.email == dto.Email);
        if (emailExists) throw new InvalidOperationException("El correo ya está registrado.");

        // Validaciones de FK
        await ValidateCompaniesAsync(companyIds);
        await ValidateRolesAsync(roleIds);
        await ValidateFarmsAsync(farmIds);

        using var tx = await _ctx.Database.BeginTransactionAsync();

        var user = new User
        {
            Id        = Guid.NewGuid(),
            surName   = dto.SurName?.Trim() ?? string.Empty,
            firstName = dto.FirstName?.Trim() ?? string.Empty,
            cedula    = dto.Cedula?.Trim() ?? string.Empty,
            telefono  = dto.Telefono?.Trim() ?? string.Empty,
            ubicacion = dto.Ubicacion?.Trim() ?? string.Empty,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Users.Add(user);

        var login = new Login
        {
            Id           = Guid.NewGuid(),
            email        = dto.Email.Trim(),
            PasswordHash = _hasher.HashPassword(null!, dto.Password),
            IsEmailLogin = true,
            IsDeleted    = false
        };
        _ctx.Logins.Add(login);
        _ctx.UserLogins.Add(new UserLogin { UserId = user.Id, LoginId = login.Id });

        // Guardar compañías primero (para cumplir FK en user_roles)
        var userCompanies = companyIds.Select(cid => new UserCompany { UserId = user.Id, CompanyId = cid });
        _ctx.UserCompanies.AddRange(userCompanies);
        await _ctx.SaveChangesAsync();

        // Construir producto cartesiano CompanyIds × RoleIds
        var pairs = from c in companyIds
                    from r in roleIds
                    select new UserRole { UserId = user.Id, CompanyId = c, RoleId = r };

        _ctx.UserRoles.AddRange(pairs);
        await _ctx.SaveChangesAsync();

        // Crear asociaciones con granjas
        if (farmIds.Length > 0)
        {
            var userFarms = farmIds.Select((farmId, index) => new UserFarm
            {
                UserId = user.Id,
                FarmId = farmId,
                IsAdmin = false,
                IsDefault = index == 0, // Primera granja como default
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id // El usuario se crea a sí mismo
            });
            _ctx.UserFarms.AddRange(userFarms);
            await _ctx.SaveChangesAsync();
        }

        await tx.CommitAsync();

        // Proyección
        var rolesNames = await _ctx.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .Where(n => n != null && n != "")
            .ToArrayAsync();

        var farms = await _ctx.UserFarms
            .Where(uf => uf.UserId == user.Id)
            .Include(uf => uf.Farm)
            .Select(uf => new UserFarmLiteDto(
                uf.FarmId,
                uf.Farm.Name,
                uf.IsAdmin,
                uf.IsDefault
            ))
            .ToArrayAsync();

        return new UserDto(
            user.Id,
            user.surName ?? string.Empty,
            user.firstName ?? string.Empty,
            user.cedula ?? string.Empty,
            user.telefono ?? string.Empty,
            user.ubicacion ?? string.Empty,
            rolesNames,
            companyIds,
            farms,
            user.IsActive,
            user.IsLocked,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET LIST (resumen)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        // Obtener usuarios que pertenecen a las empresas asignadas al usuario actual
        var usersFromAssignedCompanies = await _userPermissionService.GetUsersFromAssignedCompaniesAsync(_currentUser.UserId);
        
        // Convertir a lista de IDs para la consulta
        var userIds = usersFromAssignedCompanies.Select(u => u.Id).ToList();

        return await _ctx.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id)) // Filtrar solo usuarios de empresas asignadas
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserCompanies)
            .Include(u => u.UserFarms).ThenInclude(uf => uf.Farm)
            .Select(u => new UserDto(
                u.Id,
                u.surName ?? string.Empty,
                u.firstName ?? string.Empty,
                u.cedula ?? string.Empty,
                u.telefono ?? string.Empty,
                u.ubicacion ?? string.Empty,
                u.UserRoles.Select(ur => ur.Role.Name).Where(n => n != null && n != "").ToArray()!,
                u.UserCompanies.Select(uc => uc.CompanyId).ToArray(),
                u.UserFarms.Select(uf => new UserFarmLiteDto(
                    uf.FarmId,
                    uf.Farm.Name,
                    uf.IsAdmin,
                    uf.IsDefault
                )).ToArray(),
                u.IsActive,
                u.IsLocked,
                u.CreatedAt,
                u.LastLoginAt
            ))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET LIST (tarjetas para tabla)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<List<UserListDto>> GetUsersAsync()
    {
        return await _ctx.Users
            .AsNoTracking()
            .Include(u => u.UserLogins).ThenInclude(ul => ul.Login)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .Select(u => new UserListDto
            {
                Id        = u.Id,
                FirstName = u.firstName,
                SurName   = u.surName,
                Email     = u.UserLogins.Select(x => x.Login.email).FirstOrDefault() ?? string.Empty,
                IsActive  = u.IsActive,
                Cedula    = u.cedula,
                Telefono  = u.telefono,
                Ubicacion = u.ubicacion,

                Roles          = u.UserRoles.Select(ur => ur.Role.Name).Where(n => n != null && n != "").ToList()!,
                CompanyNames   = u.UserCompanies.Select(uc => uc.Company.Name).Where(n => n != null && n != "").ToList()!,

                PrimaryRole    = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(n => n != null && n != ""),
                PrimaryCompany = u.UserCompanies.Select(uc => uc.Company.Name).FirstOrDefault(n => n != null && n != "")
            })
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET BY ID (detalle)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _ctx.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserCompanies)
            .Include(u => u.UserFarms).ThenInclude(uf => uf.Farm)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.surName ?? string.Empty,
            user.firstName ?? string.Empty,
            user.cedula ?? string.Empty,
            user.telefono ?? string.Empty,
            user.ubicacion ?? string.Empty,
            user.UserRoles.Select(ur => ur.Role.Name).Where(n => n != null && n != "").ToArray()!,
            user.UserCompanies.Select(uc => uc.CompanyId).ToArray(),
            user.UserFarms.Select(uf => new UserFarmLiteDto(
                uf.FarmId,
                uf.Farm.Name,
                uf.IsAdmin,
                uf.IsDefault
            )).ToArray(),
            user.IsActive,
            user.IsLocked,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    // UPDATE / PATCH (parcial)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _ctx.Users
            .Include(u => u.UserCompanies)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        // Campos simples (parciales)
        if (dto.SurName   is not null) user.surName   = dto.SurName.Trim();
        if (dto.FirstName is not null) user.firstName = dto.FirstName.Trim();
        if (dto.Cedula    is not null) user.cedula    = dto.Cedula.Trim();
        if (dto.Telefono  is not null) user.telefono  = dto.Telefono.Trim();
        if (dto.Ubicacion is not null) user.ubicacion = dto.Ubicacion.Trim();
        if (dto.IsActive  is not null) user.IsActive  = dto.IsActive.Value;
        if (dto.IsLocked  is not null) user.IsLocked  = dto.IsLocked.Value;

        // Normalizar intención
        var companyIdsIncoming = (dto.CompanyIds ?? Array.Empty<int>()).Distinct().ToArray();
        var roleIdsIncoming    = (dto.RoleIds    ?? Array.Empty<int>()).Distinct().ToArray();

        // Validar FK sólo si vienen arreglos (semántica PATCH)
        if (dto.CompanyIds is not null)
            await ValidateCompaniesAsync(companyIdsIncoming);
        if (dto.RoleIds is not null)
            await ValidateRolesAsync(roleIdsIncoming);

        using var tx = await _ctx.Database.BeginTransactionAsync();

        // 1) Sincronizar compañías si se enviaron
        if (dto.CompanyIds is not null)
        {
            var currentSet = user.UserCompanies.Select(uc => uc.CompanyId).ToHashSet();

            var toRemove = user.UserCompanies.Where(uc => !companyIdsIncoming.Contains(uc.CompanyId)).ToList();
            if (toRemove.Count > 0) _ctx.UserCompanies.RemoveRange(toRemove);

            var toAdd = companyIdsIncoming
                .Where(cid => !currentSet.Contains(cid))
                .Select(cid => new UserCompany { UserId = user.Id, CompanyId = cid })
                .ToList();
            if (toAdd.Count > 0) _ctx.UserCompanies.AddRange(toAdd);

            await _ctx.SaveChangesAsync(); // 👈 asegura FK para user_roles
        }

        // 2) Sincronizar roles si se enviaron
        if (dto.RoleIds is not null)
        {
            // Tomar el set de compañías vigente (después de actualizar)
            var effectiveCompanies = await _ctx.UserCompanies
                .AsNoTracking()
                .Where(uc => uc.UserId == user.Id)
                .Select(uc => uc.CompanyId)
                .ToArrayAsync();

            // Si no quedan compañías, se eliminan todos los roles (no hay dónde colgarlos)
            if (effectiveCompanies.Length == 0)
            {
                if (user.UserRoles.Count > 0) _ctx.UserRoles.RemoveRange(user.UserRoles);
            }
            else
            {
                var incomingPairs = (from c in effectiveCompanies
                                     from r in roleIdsIncoming
                                     select (c, r)).ToHashSet();

                var currentPairs = user.UserRoles
                    .Select(ur => (ur.CompanyId, ur.RoleId))
                    .ToHashSet();

                var toRemove = user.UserRoles
                    .Where(ur => !incomingPairs.Contains((ur.CompanyId, ur.RoleId)))
                    .ToList();
                if (toRemove.Count > 0) _ctx.UserRoles.RemoveRange(toRemove);

                var toAdd = incomingPairs
                    .Where(p => !currentPairs.Contains(p))
                    .Select(p => new UserRole { UserId = user.Id, CompanyId = p.c, RoleId = p.r })
                    .ToList();
                if (toAdd.Count > 0) _ctx.UserRoles.AddRange(toAdd);
            }
        }

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();

        // Proyección final
        var rolesNames = await _ctx.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .Where(n => n != null && n != "")
            .ToArrayAsync();

        var companyIds = await _ctx.UserCompanies
            .Where(uc => uc.UserId == user.Id)
            .Select(uc => uc.CompanyId)
            .ToArrayAsync();

        var farms = await _ctx.UserFarms
            .Where(uf => uf.UserId == user.Id)
            .Include(uf => uf.Farm)
            .Select(uf => new UserFarmLiteDto(
                uf.FarmId,
                uf.Farm.Name,
                uf.IsAdmin,
                uf.IsDefault
            ))
            .ToArrayAsync();

        return new UserDto(
            user.Id,
            user.surName ?? string.Empty,
            user.firstName ?? string.Empty,
            user.cedula ?? string.Empty,
            user.telefono ?? string.Empty,
            user.ubicacion ?? string.Empty,
            rolesNames,
            companyIds,
            farms,
            user.IsActive,
            user.IsLocked,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    // DELETE (cascada controlada)
    // ─────────────────────────────────────────────────────────────────────
    public async Task DeleteAsync(Guid id)
    {
        await using var tx = await _ctx.Database.BeginTransactionAsync();

        var user = await _ctx.Users
            .Include(u => u.UserLogins).ThenInclude(ul => ul.Login)
            .Include(u => u.UserCompanies)
            .Include(u => u.UserRoles)
            .Include(u => u.UserFarms)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            throw new KeyNotFoundException("Usuario no encontrado");

        var logins = user.UserLogins.Select(ul => ul.Login).ToList();

        _ctx.UserLogins.RemoveRange(user.UserLogins);
        _ctx.UserRoles.RemoveRange(user.UserRoles);
        _ctx.UserCompanies.RemoveRange(user.UserCompanies);
        _ctx.UserFarms.RemoveRange(user.UserFarms);
        _ctx.Logins.RemoveRange(logins);
        _ctx.Users.Remove(user);

        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers de validación
    // ─────────────────────────────────────────────────────────────────────
    private async Task ValidateCompaniesAsync(int[] companyIds)
    {
        if (companyIds.Length == 0) return;
        var existing = await _ctx.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var missing = companyIds.Except(existing).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Compañías inexistentes: {string.Join(", ", missing)}");
    }

    private async Task ValidateRolesAsync(int[] roleIds)
    {
        if (roleIds.Length == 0) return;
        var existing = await _ctx.Roles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync();

        var missing = roleIds.Except(existing).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Roles inexistentes: {string.Join(", ", missing)}");
    }

    private async Task ValidateFarmsAsync(int[] farmIds)
    {
        if (farmIds.Length == 0) return;
        var existing = await _ctx.Farms
            .AsNoTracking()
            .Where(f => farmIds.Contains(f.Id))
            .Select(f => f.Id)
            .ToListAsync();

        var missing = farmIds.Except(existing).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Granjas inexistentes: {string.Join(", ", missing)}");
    }
}
