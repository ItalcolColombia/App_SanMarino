// src/ZooSanMarino.Infrastructure/Services/RoleCompositeService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Exceptions;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class RoleCompositeService : IRoleCompositeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public RoleCompositeService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    // =========================
    // Utilidades internas
    // =========================

    // Registro fuertemente tipado para armar el árbol de menús
    private sealed record FlatMenu(int Id, string? Label, string? Icon, string? Route, int? Order, int? ParentId);

    // Ejecuta operaciones con estrategia de reintento + transacción
    private async Task<TResult> ExecInTxAsync<TResult>(Func<Task<TResult>> action)
    {
        var strategy = _ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            try
            {
                var result = await action();
                await tx.CommitAsync();
                return result;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    private static string[] NormalizeKeys(string[]? keys) =>
        (keys ?? Array.Empty<string>())
        .Select(k => k?.Trim())
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Select(k => k!.ToLowerInvariant())
        .Distinct()
        .ToArray();

    /// <summary>
    /// Rechaza los permisos que las empresas del rol no habilitan (<c>company_permissions</c>).
    ///
    /// <para>
    /// Es el gate de ESCRITURA, hermano del de runtime (<c>AuthService.PermisosEfectivosAsync</c>):
    /// sin él, el permiso se guardaba igual y quedaba inerte, lo que confunde — el rol "lo tiene" pero
    /// no hace nada. Solo juzga lo que se AGREGA, así que conservar o quitar lo ya asignado nunca
    /// falla y un rol con huérfanos se sigue pudiendo editar y limpiar.
    /// </para>
    /// <para>
    /// FAIL-CLOSED: un rol sin empresas, o con una empresa sin configurar, no puede recibir permisos
    /// nuevos. El mensaje dice qué permiso y por qué, para que sea accionable.
    /// </para>
    /// </summary>
    private async Task EnsurePermisosHabilitadosPorEmpresaAsync(
        int[] companyIds,
        string[] keysSolicitadas,
        string[] yaAsignadas)
    {
        if (keysSolicitadas.Length == 0) return;

        var habilitadasPorEmpresa = companyIds.Length == 0
            ? new Dictionary<int, IReadOnlyCollection<string>>()
            : (await _ctx.CompanyPermissions
                    .AsNoTracking()
                    .Where(cp => companyIds.Contains(cp.CompanyId) && cp.IsEnabled)
                    .Select(cp => new { cp.CompanyId, cp.Permission.Key })
                    .ToListAsync())
                .GroupBy(x => x.CompanyId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<string>)g.Select(x => x.Key).Distinct().ToList());

        var rechazadas = CompanyPermissionCalculos.ResolverNoPermitidas(
            keysSolicitadas, yaAsignadas, habilitadasPorEmpresa, companyIds);

        if (rechazadas.Count == 0) return;

        if (companyIds.Length == 0)
            throw new PermisoNoHabilitadoException(
                $"No se pueden asignar permisos a un rol sin empresa: {string.Join(", ", rechazadas)}. " +
                "Asigná primero la empresa del rol.");

        var nombres = await _ctx.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => c.Name)
            .ToListAsync();

        throw new PermisoNoHabilitadoException(
            $"Permisos no habilitados para {string.Join(" / ", nombres)}: {string.Join(", ", rechazadas)}. " +
            "Habilitalos en Empresas → Permisos de la empresa, o quitalos del rol.");
    }

    /// <summary>
    /// Empresas a las que está vinculado un rol ya existente. La empresa llega al rol por
    /// <c>role_companies</c> o por <c>user_roles.company_id</c>; los dos caminos están poblados en
    /// producción, así que se unen (con uno solo, el gate rechazaría permisos legítimos).
    /// </summary>
    private async Task<int[]> CompanyIdsDelRolAsync(int roleId)
    {
        var porRoleCompanies = await _ctx.RoleCompanies
            .AsNoTracking()
            .Where(rc => rc.RoleId == roleId)
            .Select(rc => rc.CompanyId)
            .ToListAsync();

        var porUserRoles = await _ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.CompanyId)
            .ToListAsync();

        return porRoleCompanies.Concat(porUserRoles).Distinct().ToArray();
    }

    private async Task EnsureCompaniesExist(int[] companyIds)
    {
        if (companyIds.Length == 0) return;

        var existing = await _ctx.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var missing = companyIds.Except(existing).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Las compañías no existen: {string.Join(", ", missing)}");
    }

    // Match case-insensitive y error claro si faltan keys
    private async Task<Dictionary<string, int>> EnsurePermissionsExistAndMap(string[] permKeys)
    {
        var norm = NormalizeKeys(permKeys);
        if (norm.Length == 0) return new Dictionary<string, int>();

        var existing = await _ctx.Permissions
            .AsNoTracking()
            .Select(p => new { p.Id, KeyLower = p.Key.ToLower() })
            .Where(p => norm.Contains(p.KeyLower))
            .ToListAsync();

        var map = existing.ToDictionary(p => p.KeyLower, p => p.Id);
        var missing = norm.Where(k => !map.ContainsKey(k)).ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException($"Permisos inexistentes: {string.Join(", ", missing)}");

        return map;
    }

    private static IEnumerable<MenuItemDto> BuildTree(IEnumerable<FlatMenu> flat)
    {
        // Orden estable: primero por Parent y luego por Order (nulos al final)
        var ordered = flat
            .OrderBy(x => x.ParentId.HasValue ? 1 : 0)
            .ThenBy(x => x.ParentId)
            .ThenBy(x => x.Order ?? int.MaxValue)
            .ToList();

        var nodes = ordered.ToDictionary(
            n => n.Id,
            n => new MenuItemDto(n.Id, n.Label ?? string.Empty, n.Icon, n.Route, n.Order ?? int.MaxValue, Array.Empty<MenuItemDto>())
        );

        var childrenMap = ordered.ToDictionary(x => x.Id, _ => new List<MenuItemDto>());

        foreach (var n in ordered)
        {
            if (n.ParentId is int pid && nodes.ContainsKey(pid))
                childrenMap[pid].Add(nodes[n.Id]);
        }

        foreach (var kvp in childrenMap)
        {
            var pid = kvp.Key;
            var kids = kvp.Value.OrderBy(c => c.Order).ToArray();
            if (nodes.TryGetValue(pid, out var parent))
                nodes[pid] = parent with { Children = kids };
        }

        return ordered
            .Where(n => n.ParentId is null)
            .Select(n => nodes[n.Id])
            .OrderBy(n => n.Order)
            .ToArray();
    }

    // =========================
    // ROLES
    // =========================

    public async Task<IEnumerable<RoleDto>> Roles_GetAllAsync(string? q, int page, int pageSize)
    {
        // Pedir de más devuelve el tope, no el default (ver PaginacionCalculos).
        page = PaginacionCalculos.NormalizarPage(page);
        pageSize = PaginacionCalculos.NormalizarPageSize(
            pageSize, PaginacionCalculos.MaximoListadoTransaccional, porDefecto: 50);
        var term = (q ?? string.Empty).Trim().ToLowerInvariant();

        var query = _ctx.Roles
            .AsNoTracking()
            .AsSplitQuery()
            .TagWith("Roles_GetAllAsync")
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus)
            .AsQueryable();

        // Si el usuario no es admin/administrador, solo roles de la empresa activa (X-Active-Company-Id)
        var isAdmin = await IsCurrentUserAdminOrAdministratorAsync();
        if (!isAdmin && _currentUser.CompanyId > 0)
        {
            var companyId = _currentUser.CompanyId;
            query = query.Where(r => r.RoleCompanies.Any(rc => rc.CompanyId == companyId));
        }

        if (!string.IsNullOrEmpty(term))
            query = query.Where(r => EF.Functions.ILike(r.Name, $"%{term}%"));

        var list = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.RolePermissions.Select(rp => rp.Permission.Key).ToArray(),
                r.RoleCompanies.Select(rc => rc.CompanyId).ToArray(),
                r.RoleMenus.Select(rm => rm.MenuId).ToArray(),
                r.IsCompanyAdmin
            ))
            .ToListAsync();

        return list;
    }

    private async Task<bool> IsCurrentUserAdminOrAdministratorAsync()
    {
        var userGuid = _currentUser.UserGuid;
        if (!userGuid.HasValue) return false;

        var roleNames = await _ctx.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userGuid.Value)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        return roleNames.Any(name =>
            !string.IsNullOrWhiteSpace(name) &&
            (string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(name, "administrador", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Verifica si el usuario actual es el Super Admin (email hardcodeado). Solo el Super Admin
    /// puede activar/desactivar el flag <c>is_company_admin</c> de un rol.
    /// </summary>
    private async Task<bool> IsCurrentUserSuperAdminAsync()
    {
        return await SuperAdminLookup.EsSuperAdminAsync(_ctx, _currentUser.UserGuid);
    }

    public async Task<RoleDto?> Roles_GetByIdAsync(int id)
    {
        if (id <= 0) return null;

        return await _ctx.Roles
            .AsNoTracking()
            .AsSplitQuery()
            .TagWith("Roles_GetByIdAsync")
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus)
            .Where(r => r.Id == id)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.RolePermissions.Select(rp => rp.Permission.Key).ToArray(),
                r.RoleCompanies.Select(rc => rc.CompanyId).ToArray(),
                r.RoleMenus.Select(rm => rm.MenuId).ToArray(),
                r.IsCompanyAdmin
            ))
            .SingleOrDefaultAsync();
    }

    public async Task<RoleDto> Roles_CreateAsync(CreateRoleDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("El nombre del rol es requerido.");

        var permKeys   = NormalizeKeys(dto.Permissions);
        var companyIds = (dto.CompanyIds ?? Array.Empty<int>()).Distinct().ToArray();
        var menuIds    = (dto.MenuIds    ?? Array.Empty<int>()).Distinct().ToArray();

        if (await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Name == name))
            throw new InvalidOperationException($"Ya existe un rol con el nombre '{name}'.");

        await EnsureCompaniesExist(companyIds);
        var permMap = await EnsurePermissionsExistAndMap(permKeys);

        // Rol nuevo: no hay nada "ya asignado", así que se juzgan todos los permisos que vienen.
        await EnsurePermisosHabilitadosPorEmpresaAsync(companyIds, permKeys, Array.Empty<string>());

        // Solo el Super Admin puede marcar un rol como Administrador de Empresa/País.
        var esSuperAdmin = await IsCurrentUserSuperAdminAsync();
        var isCompanyAdmin = RoleAdminCalculos.ResolverIsCompanyAdminEnCreacion(esSuperAdmin, dto.IsCompanyAdmin);

        // Validar menús si vinieron
        if (menuIds.Length > 0)
        {
            var existingMenuIds = await _ctx.Menus.AsNoTracking()
                .Where(m => menuIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();
            var missing = menuIds.Except(existingMenuIds).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Menús inexistentes: {string.Join(", ", missing)}");
        }

        return await ExecInTxAsync(async () =>
        {
            var role = new Role { Name = name!, IsCompanyAdmin = isCompanyAdmin };
            _ctx.Roles.Add(role);
            await _ctx.SaveChangesAsync();

            if (permKeys.Length > 0)
                _ctx.RolePermissions.AddRange(permKeys.Select(k => new RolePermission { RoleId = role.Id, PermissionId = permMap[k] }));

            if (companyIds.Length > 0)
                _ctx.RoleCompanies.AddRange(companyIds.Select(cid => new RoleCompany { RoleId = role.Id, CompanyId = cid }));

            if (menuIds.Length > 0)
                _ctx.RoleMenus.AddRange(menuIds.Select(id => new RoleMenu { RoleId = role.Id, MenuId = id }));

            await _ctx.SaveChangesAsync();

            var created = await Roles_GetByIdAsync(role.Id);
            if (created is null) throw new InvalidOperationException("No se pudo recuperar el rol tras crearlo.");
            return created;
        });
    }

    public async Task<RoleDto?> Roles_UpdateAsync(UpdateRoleDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RolePermissions)
            .Include(r => r.RoleCompanies)
            .Include(r => r.RoleMenus)
            .SingleOrDefaultAsync(r => r.Id == dto.Id);

        if (role is null) return null;

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("El nombre del rol es requerido.");

        var permKeys = NormalizeKeys(dto.Permissions);
        var companyIds = (dto.CompanyIds ?? Array.Empty<int>()).Distinct().ToArray();

        if (await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Name == name && r.Id != role.Id))
            throw new InvalidOperationException($"Ya existe un rol con el nombre '{name}'.");

        await EnsureCompaniesExist(companyIds);
        var permMap = await EnsurePermissionsExistAndMap(permKeys);

        // Se juzga contra las empresas que el rol VA A TENER (las del dto) y solo lo que se agrega:
        // conservar o quitar un permiso ya asignado nunca falla, así un rol con huérfanos se puede
        // seguir editando y limpiando.
        var keysActuales = await _ctx.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission.Key)
            .ToArrayAsync();
        await EnsurePermisosHabilitadosPorEmpresaAsync(companyIds, permKeys, keysActuales);

        // Solo el Super Admin puede cambiar el flag Administrador de Empresa/País (null = no tocar).
        var esSuperAdmin = await IsCurrentUserSuperAdminAsync();
        var isCompanyAdmin = RoleAdminCalculos.ResolverIsCompanyAdminEnEdicion(esSuperAdmin, dto.IsCompanyAdmin, role.IsCompanyAdmin);

        // Si MenuIds viene null => no tocar menús. Si viene array => aplicar delta.
        int[]? incomingMenuIds = dto.MenuIds is null
            ? null
            : (dto.MenuIds ?? Array.Empty<int>()).Distinct().ToArray();

        return await ExecInTxAsync(async () =>
        {
            role.Name = name!;
            role.IsCompanyAdmin = isCompanyAdmin;

            // ===== Permisos (replace)
            _ctx.RolePermissions.RemoveRange(role.RolePermissions);
            if (permKeys.Length > 0)
            {
                var newLinks = permKeys.Select(k => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permMap[k]
                });
                _ctx.RolePermissions.AddRange(newLinks);
            }

            // ===== Compañías (replace)
            _ctx.RoleCompanies.RemoveRange(role.RoleCompanies);
            if (companyIds.Length > 0)
            {
                var rc = companyIds.Select(cid => new RoleCompany { RoleId = role.Id, CompanyId = cid });
                _ctx.RoleCompanies.AddRange(rc);
            }

            // ===== Menús (delta only si dto.MenuIds != null)
            if (incomingMenuIds is not null)
            {
                // Validar existencia de menús
                var existingMenuIds = await _ctx.Menus.AsNoTracking()
                    .Where(m => incomingMenuIds.Contains(m.Id))
                    .Select(m => m.Id)
                    .ToListAsync();

                var missing = incomingMenuIds.Except(existingMenuIds).ToArray();
                if (missing.Length > 0)
                    throw new InvalidOperationException($"Menús inexistentes: {string.Join(", ", missing)}");

                var current = role.RoleMenus.Select(rm => rm.MenuId).ToHashSet();
                var desired = incomingMenuIds.ToHashSet();

                var toAddIds = desired.Except(current).ToArray();
                var toRemoveIds = current.Except(desired).ToArray();

                if (toRemoveIds.Length > 0)
                {
                    var toRemove = role.RoleMenus.Where(rm => toRemoveIds.Contains(rm.MenuId)).ToList();
                    _ctx.RoleMenus.RemoveRange(toRemove);
                }

                if (toAddIds.Length > 0)
                {
                    var toAdd = toAddIds.Select(id => new RoleMenu { RoleId = role.Id, MenuId = id });
                    _ctx.RoleMenus.AddRange(toAdd);
                }
            }

            await _ctx.SaveChangesAsync();
            return await Roles_GetByIdAsync(role.Id);
        });
    }

    public async Task<bool> Roles_DeleteAsync(int id)
    {
        if (id <= 0) return false;

        var role = await _ctx.Roles.FindAsync(id);
        if (role is null) return false;

        return await ExecInTxAsync(async () =>
        {
            _ctx.Roles.Remove(role);
            await _ctx.SaveChangesAsync();
            return true;
        });
    }

    public async Task<string[]?> Roles_GetPermissionsAsync(int roleId)
    {
        var exists = await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Id == roleId);
        if (!exists) return null;

        return await _ctx.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .OrderBy(k => k)
            .ToArrayAsync();
    }

    public async Task<RoleDto?> Roles_AddPermissionsAsync(int roleId, string[] keys)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var norm = NormalizeKeys(keys);
        var permMap = await EnsurePermissionsExistAndMap(norm);

        await EnsurePermisosHabilitadosPorEmpresaAsync(
            await CompanyIdsDelRolAsync(roleId),
            norm,
            role.RolePermissions.Select(rp => rp.Permission.Key).ToArray());

        return await ExecInTxAsync(async () =>
        {
            var current = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            var toAdd = norm
                .Select(k => permMap[k])
                .Where(pid => !current.Contains(pid))
                .Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid });

            _ctx.RolePermissions.AddRange(toAdd);
            await _ctx.SaveChangesAsync();

            return await Roles_GetByIdAsync(role.Id);
        });
    }

    // OJO: quitar permisos NO pasa por EnsurePermisosHabilitadosPorEmpresaAsync, a propósito. Es
    // justamente el camino para limpiar un permiso que la empresa dejó de habilitar; validarlo dejaría
    // los huérfanos pegados al rol para siempre.
    public async Task<RoleDto?> Roles_RemovePermissionsAsync(int roleId, string[] keys)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var norm = NormalizeKeys(keys);

        return await ExecInTxAsync(async () =>
        {
            var toRemove = role.RolePermissions.Where(rp => norm.Contains(rp.Permission.Key.ToLower())).ToList();
            if (toRemove.Count > 0)
            {
                _ctx.RolePermissions.RemoveRange(toRemove);
                await _ctx.SaveChangesAsync();
            }
            return await Roles_GetByIdAsync(role.Id);
        });
    }

    public async Task<RoleDto?> Roles_ReplacePermissionsAsync(int roleId, string[] keys)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var norm = NormalizeKeys(keys);
        var permMap = await EnsurePermissionsExistAndMap(norm);

        await EnsurePermisosHabilitadosPorEmpresaAsync(
            await CompanyIdsDelRolAsync(roleId),
            norm,
            role.RolePermissions.Select(rp => rp.Permission.Key).ToArray());

        return await ExecInTxAsync(async () =>
        {
            _ctx.RolePermissions.RemoveRange(role.RolePermissions);

            if (norm.Length > 0)
            {
                var links = norm.Select(k => new RolePermission { RoleId = role.Id, PermissionId = permMap[k] });
                _ctx.RolePermissions.AddRange(links);
            }

            await _ctx.SaveChangesAsync();
            return await Roles_GetByIdAsync(role.Id);
        });
    }

    // =========================
    // PERMISOS (catálogo)
    // =========================

    public async Task<IEnumerable<PermissionDto>> Permissions_GetAllAsync() =>
        await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Key)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description))
            .ToListAsync();

    // =========================
    // MENÚS (catálogo / CRUD / filtrado)
    // =========================

    public async Task<IEnumerable<MenuItemDto>> Menus_GetTreeAsync()
    {
        var list = await _ctx.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.ParentId)
            .ThenBy(m => m.Order)
            .Select(m => new FlatMenu(m.Id, m.Label, m.Icon, m.Route, m.Order, m.ParentId))
            .ToListAsync();

        return BuildTree(list);
    }

    /// <summary>
    /// El menú efectivo del usuario dentro de una empresa.
    ///
    /// <para>
    /// <b>Lo resuelve la BD, en una sola llamada.</b> Antes eran cuatro viajes a Postgres (roles →
    /// keys de permiso → catálogo con su subquery de <c>menu_permissions</c> → menús asignados) más
    /// el armado del árbol en memoria; <c>fn_menu_usuario</c> hace la relación entre las siete tablas
    /// donde viven los índices y devuelve el árbol ya construido.
    /// </para>
    ///
    /// <para>
    /// <b>Y ahora respeta <c>company_menus</c>.</b> Hasta el 26-ago-2026 esta consulta no miraba esa
    /// tabla, así que quitarle un módulo a una empresa no ocultaba nada: a ItalcolPanamá se le
    /// colaban 7 menús que no tiene asignados (ItalJira entero, Guía Genética, Bandeja de gestión).
    /// </para>
    ///
    /// <para>
    /// La regla vive en la función SQL; su especificación ejecutable —y los tests que la fijan— en
    /// <see cref="MenuVisibilidadCalculos"/>. Plan:
    /// <c>fase_de_desarrollo/menu_efectivo_por_empresa_plan.md</c>.
    /// </para>
    /// </summary>
    /// <param name="companyId">
    /// Empresa efectiva. <c>null</c> = sin recorte por empresa (modo administración). Los endpoints
    /// caen a <c>ICurrentUser.CompanyId</c>, que es el que validó <c>ActiveCompanyMiddleware</c>.
    /// </param>
    public async Task<IEnumerable<MenuItemDto>> Menus_GetForUserAsync(Guid userId, int? companyId)
    {
        var filas = await _ctx.Database
            .SqlQueryRaw<string>(
                "SELECT fn_menu_usuario({0}::uuid, {1}::int)::text AS \"Value\"",
                userId,
                (object?)companyId ?? DBNull.Value)
            .ToListAsync();

        var json = filas.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<MenuItemDto>();

        // Las opciones viven en MenuVisibilidadCalculos para que el test que fija el contrato del
        // JSON use exactamente las mismas.
        return System.Text.Json.JsonSerializer.Deserialize<MenuItemDto[]>(
                   json, MenuVisibilidadCalculos.OpcionesJson)
               ?? Array.Empty<MenuItemDto>();
    }

    public async Task<MenuItemDto> Menus_CreateAsync(CreateMenuDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.Label)) throw new InvalidOperationException("Label es requerido.");

        return await ExecInTxAsync(async () =>
        {
            var m = new Menu
            {
                Label = dto.Label.Trim(),
                Icon = string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon.Trim(),
                Route = string.IsNullOrWhiteSpace(dto.Route) ? null : dto.Route.Trim(),
                ParentId = dto.ParentId,
                Order = dto.Order,
                IsActive = dto.IsActive
            };
            _ctx.Menus.Add(m);
            await _ctx.SaveChangesAsync();

            if (dto.PermissionIds?.Length > 0)
            {
                var links = dto.PermissionIds.Distinct()
                    .Select(pid => new MenuPermission { MenuId = m.Id, PermissionId = pid });
                _ctx.MenuPermissions.AddRange(links);
                await _ctx.SaveChangesAsync();
            }

            return new MenuItemDto(m.Id, m.Label, m.Icon, m.Route, m.Order, Array.Empty<MenuItemDto>());
        });
    }

    public async Task<MenuItemDto?> Menus_UpdateAsync(UpdateMenuDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        var m = await _ctx.Menus
            .Include(x => x.MenuPermissions)
            .SingleOrDefaultAsync(x => x.Id == dto.Id);
        if (m is null) return null;

        return await ExecInTxAsync(async () =>
        {
            m.Label = string.IsNullOrWhiteSpace(dto.Label) ? m.Label : dto.Label.Trim();
            m.Icon  = string.IsNullOrWhiteSpace(dto.Icon)  ? null : dto.Icon.Trim();
            m.Route = string.IsNullOrWhiteSpace(dto.Route) ? null : dto.Route.Trim();
            m.ParentId = dto.ParentId;
            m.Order = dto.Order;
            m.IsActive = dto.IsActive;

            _ctx.MenuPermissions.RemoveRange(m.MenuPermissions);
            if (dto.PermissionIds?.Length > 0)
            {
                var links = dto.PermissionIds.Distinct()
                    .Select(pid => new MenuPermission { MenuId = m.Id, PermissionId = pid });
                _ctx.MenuPermissions.AddRange(links);
            }

            await _ctx.SaveChangesAsync();

            return new MenuItemDto(m.Id, m.Label, m.Icon, m.Route, m.Order , Array.Empty<MenuItemDto>());
        });
    }

    public async Task<bool> Menus_DeleteAsync(int id)
    {
        if (id <= 0) return false;

        var m = await _ctx.Menus.FindAsync(id);
        if (m is null) return false;

        return await ExecInTxAsync(async () =>
        {
            var hasChildren = await _ctx.Menus.AnyAsync(x => x.ParentId == id);
            if (hasChildren)
                throw new InvalidOperationException("No se puede eliminar un menú con hijos.");

            _ctx.Menus.Remove(m);
            await _ctx.SaveChangesAsync();
            return true;
        });
    }

    // =========================
    // MENÚS ASIGNADOS A ROLES
    // =========================

    public async Task<int[]?> Roles_GetMenusAsync(int roleId)
    {
        var exists = await _ctx.Roles.AsNoTracking().AnyAsync(r => r.Id == roleId);
        if (!exists) return null;

        return await _ctx.RoleMenus
            .AsNoTracking()
            .Where(rm => rm.RoleId == roleId)
            .Select(rm => rm.MenuId)
            .OrderBy(id => id)
            .ToArrayAsync();
    }

    public async Task<RoleDto?> Roles_AddMenusAsync(int roleId, int[] menuIds)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RoleMenus)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var ids = (menuIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (ids.Length == 0) return await Roles_GetByIdAsync(roleId);

        return await ExecInTxAsync(async () =>
        {
            // Validar existencia de menús
            var existingMenuIds = await _ctx.Menus.AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();
            var missing = ids.Except(existingMenuIds).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Menús inexistentes: {string.Join(", ", missing)}");

            // Agregar los que no estén
            var current = role.RoleMenus.Select(rm => rm.MenuId).ToHashSet();
            var toAdd = ids.Where(id => !current.Contains(id))
                           .Select(id => new RoleMenu { RoleId = role.Id, MenuId = id });

            _ctx.RoleMenus.AddRange(toAdd);
            await _ctx.SaveChangesAsync();

            return await Roles_GetByIdAsync(role.Id);
        });
    }

    public async Task<RoleDto?> Roles_RemoveMenusAsync(int roleId, int[] menuIds)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RoleMenus)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var ids = (menuIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (ids.Length == 0) return await Roles_GetByIdAsync(roleId);

        return await ExecInTxAsync(async () =>
        {
            var toRemove = role.RoleMenus.Where(rm => ids.Contains(rm.MenuId)).ToList();
            if (toRemove.Count > 0)
            {
                _ctx.RoleMenus.RemoveRange(toRemove);
                await _ctx.SaveChangesAsync();
            }

            return await Roles_GetByIdAsync(role.Id);
        });
    }

    public async Task<RoleDto?> Roles_ReplaceMenusAsync(int roleId, int[] menuIds)
    {
        var role = await _ctx.Roles
            .AsSplitQuery()
            .Include(r => r.RoleMenus)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Include(r => r.RoleCompanies)
            .SingleOrDefaultAsync(r => r.Id == roleId);
        if (role is null) return null;

        var ids = (menuIds ?? Array.Empty<int>()).Distinct().ToArray();

        return await ExecInTxAsync(async () =>
        {
            // Validar existencia
            var existingMenuIds = await _ctx.Menus.AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync();
            var missing = ids.Except(existingMenuIds).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Menús inexistentes: {string.Join(", ", missing)}");

            // Reemplazar
            _ctx.RoleMenus.RemoveRange(role.RoleMenus);
            if (ids.Length > 0)
            {
                var newLinks = ids.Select(id => new RoleMenu { RoleId = role.Id, MenuId = id });
                _ctx.RoleMenus.AddRange(newLinks);
            }
            await _ctx.SaveChangesAsync();

            return await Roles_GetByIdAsync(role.Id);
        });
    }
}
