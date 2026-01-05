// file: src/ZooSanMarino.Infrastructure/Services/GalponService.cs

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using ZooSanMarino.Application.DTOs;
using AppInterfaces = ZooSanMarino.Application.Interfaces;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using GalponDtos = ZooSanMarino.Application.DTOs.Galpones;
using SharedDtos = ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Application.DTOs.Farms;

namespace ZooSanMarino.Infrastructure.Services;

public class GalponService : AppInterfaces.IGalponService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;
    private readonly AppInterfaces.ICompanyResolver _companyResolver;
    private readonly AppInterfaces.IUserPermissionService _userPermissionService;

    public GalponService(
        ZooSanMarinoContext ctx, 
        AppInterfaces.ICurrentUser current,
        AppInterfaces.ICompanyResolver companyResolver,
        AppInterfaces.IUserPermissionService userPermissionService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _userPermissionService = userPermissionService;
    }

    /// <summary>
    /// Obtiene el CompanyId efectivo basado en el header o token JWT
    /// </summary>
    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        // Si hay una empresa activa especificada en el header, usarla
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var companyId = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
            if (companyId.HasValue)
            {
                return companyId.Value;
            }
        }

        // Fallback al CompanyId del token JWT
        return _current.CompanyId;
    }

    /// <summary>
    /// Verifica si el usuario es admin o administrador
    /// </summary>
    private async Task<bool> IsUserAdminOrAdministratorAsync(int userId)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue)
        {
            userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        }
        
        var userRoles = await _ctx.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userIdGuid.Value)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        return userRoles.Any(role => 
            !string.IsNullOrWhiteSpace(role) && 
            (role.Equals("admin", StringComparison.OrdinalIgnoreCase) || 
             role.Equals("administrador", StringComparison.OrdinalIgnoreCase))
        );
    }

    /// <summary>
    /// Verifica si el usuario es super admin
    /// </summary>
    private async Task<bool> IsSuperAdminAsync(int userId)
    {
        var userIdGuid = _current.UserGuid;
        if (!userIdGuid.HasValue)
        {
            userIdGuid = new Guid(userId.ToString("D32").PadLeft(32, '0'));
        }
        
        var userEmail = await _ctx.UserLogins
            .AsNoTracking()
            .Include(ul => ul.Login)
            .Where(ul => ul.UserId == userIdGuid.Value)
            .Select(ul => ul.Login.email)
            .FirstOrDefaultAsync();

        return userEmail?.ToLower() == "moiesbbuga@gmail.com";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BÚSQUEDA DETALLADA (PAGINADA)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<CommonDtos.PagedResult<GalponDtos.GalponDetailDto>> SearchAsync(GalponDtos.GalponSearchRequest req)
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking();

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (isAdmin)
        {
            Console.WriteLine($"=== GalponService.SearchAsync - Usuario es admin/administrador, mostrando TODOS los galpones ===");
            // No filtrar por empresa - mostrar todos los galpones
        }
        else
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            Console.WriteLine($"=== GalponService.SearchAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        if (req.SoloActivos) q = q.Where(g => g.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            q = q.Where(g =>
                g.GalponId.ToLower().Contains(term) ||
                g.GalponNombre.ToLower().Contains(term));
            // Para PostgreSQL puedes usar ILIKE:
            // q = q.Where(g => EF.Functions.ILike(g.GalponId, $"%{req.Search}%") ||
            //                  EF.Functions.ILike(g.GalponNombre, $"%{req.Search}%"));
        }

        if (req.GranjaId.HasValue)            q = q.Where(g => g.GranjaId == req.GranjaId);
        if (!string.IsNullOrWhiteSpace(req.NucleoId))  q = q.Where(g => g.NucleoId == req.NucleoId);
        if (!string.IsNullOrWhiteSpace(req.TipoGalpon)) q = q.Where(g => g.TipoGalpon == req.TipoGalpon);

        q = ApplyOrder(q, req.SortBy, req.SortDesc);

        var total = await q.LongCountAsync();
        var items = await ProjectToDetail(q)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToListAsync();

        return new CommonDtos.PagedResult<GalponDtos.GalponDetailDto>
        {
            Page     = req.Page,
            PageSize = req.PageSize,
            Total    = total,
            Items    = items
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // QUERIES DETALLADAS (NO PAGINADAS)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<GalponDtos.GalponDetailDto?> GetDetailByIdAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetAllDetailAsync()
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking().Where(g => g.DeletedAt == null);

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (isAdmin)
        {
            Console.WriteLine($"=== GalponService.GetAllDetailAsync - Usuario es admin/administrador, mostrando TODOS los galpones ===");
            // No filtrar por empresa - mostrar todos los galpones
        }
        else
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            Console.WriteLine($"=== GalponService.GetAllDetailAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<GalponDtos.GalponDetailDto?> GetDetailByIdSimpleAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetDetailByGranjaAndNucleoAsync(int granjaId, string nucleoId)
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.DeletedAt == null &&
                        g.GranjaId == granjaId &&
                        g.NucleoId == nucleoId);

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (!isAdmin)
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        return await ProjectToDetail(q).ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CRUD / LISTADOS QUE USA EL CONTROLLER (DETALLE CONSISTENTE)
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetAllAsync()
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking().Where(g => g.DeletedAt == null);

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (isAdmin)
        {
            Console.WriteLine($"=== GalponService.GetAllAsync - Usuario es admin/administrador, mostrando TODOS los galpones ===");
            // No filtrar por empresa - mostrar todos los galpones
        }
        else
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            Console.WriteLine($"=== GalponService.GetAllAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<GalponDtos.GalponDetailDto?> GetByIdAsync(string galponId)
    {
        var q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.CompanyId == _current.CompanyId &&
                        g.GalponId   == galponId &&
                        g.DeletedAt  == null);
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetByGranjaAsync(int granjaId)
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.DeletedAt == null && g.GranjaId == granjaId);

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (!isAdmin)
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<IEnumerable<GalponDtos.GalponDetailDto>> GetByGranjaAndNucleoAsync(int granjaId, string nucleoId)
    {
        IQueryable<Galpon> q = _ctx.Galpones.AsNoTracking()
            .Where(g => g.DeletedAt == null &&
                        g.GranjaId == granjaId &&
                        g.NucleoId == nucleoId);

        // Verificar si el usuario es admin/administrador
        var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
        var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
        var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                     await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                     await IsSuperAdminAsync(_current.UserId);

        if (!isAdmin)
        {
            // Si NO es admin, filtrar solo por los galpones de su empresa
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            q = q.Where(g => g.CompanyId == effectiveCompanyId);
        }

        return await ProjectToDetail(q).ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // CREATE / UPDATE / DELETE
    // ─────────────────────────────────────────────────────────────────────────────
    public async Task<GalponDtos.GalponDetailDto> CreateAsync(CreateGalponDto dto)
    {
        await EnsureFarmExists(dto.GranjaId);
        await EnsureNucleoExists(dto.NucleoId, dto.GranjaId);

        // Obtener la empresa efectiva
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

        // Si el GalponId está vacío o ya existe, generar uno nuevo automáticamente
        string galponId = dto.GalponId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(galponId))
        {
            galponId = await GenerateNextGalponIdAsync(effectiveCompanyId);
            Console.WriteLine($"=== GalponService.CreateAsync - GalponId generado automáticamente: {galponId} ===");
        }
        else
        {
            // Verificar si ya existe
            var exists = await _ctx.Galpones.AnyAsync(x =>
                x.CompanyId == effectiveCompanyId &&
                x.GalponId == galponId &&
                x.DeletedAt == null);

            if (exists)
            {
                // Si ya existe, generar uno nuevo automáticamente
                Console.WriteLine($"=== GalponService.CreateAsync - GalponId '{galponId}' ya existe, generando uno nuevo ===");
                galponId = await GenerateNextGalponIdAsync(effectiveCompanyId);
                Console.WriteLine($"=== GalponService.CreateAsync - Nuevo GalponId generado: {galponId} ===");
            }
        }

        // Verificación final antes de crear (por si hubo una condición de carrera)
        var finalCheck = await _ctx.Galpones
            .AsNoTracking()
            .AnyAsync(x => x.GalponId == galponId);

        if (finalCheck)
        {
            // Si el ID ya existe, generar uno nuevo
            Console.WriteLine($"=== GalponService.CreateAsync - Verificación final: ID '{galponId}' ya existe, generando uno nuevo ===");
            galponId = await GenerateNextGalponIdAsync(effectiveCompanyId);
            Console.WriteLine($"=== GalponService.CreateAsync - Nuevo ID generado después de verificación final: {galponId} ===");
        }

        var ent = new Galpon
        {
            GalponId        = galponId,
            GalponNombre    = dto.GalponNombre,
            NucleoId        = dto.NucleoId,
            GranjaId        = dto.GranjaId,
            Ancho           = dto.Ancho,
            Largo           = dto.Largo,
            TipoGalpon      = dto.TipoGalpon,
            CompanyId       = effectiveCompanyId,
            CreatedByUserId = _current.UserId,
            CreatedAt       = DateTime.UtcNow
        };

        _ctx.Galpones.Add(ent);
        
        try
        {
            await _ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Verificar si es un error de clave duplicada de PostgreSQL
            var innerException = ex.InnerException;
            bool isDuplicateKey = false;
            
            if (innerException != null)
            {
                var exceptionType = innerException.GetType();
                if (exceptionType.Name == "PostgresException")
                {
                    var sqlStateProperty = exceptionType.GetProperty("SqlState");
                    if (sqlStateProperty != null)
                    {
                        var sqlState = sqlStateProperty.GetValue(innerException)?.ToString();
                        isDuplicateKey = sqlState == "23505";
                    }
                }
            }

            if (isDuplicateKey)
            {
                // Si aún así hay un error de clave duplicada (condición de carrera extrema),
                // remover la entidad del contexto, generar un nuevo ID y reintentar
                Console.WriteLine($"=== GalponService.CreateAsync - Error de clave duplicada detectado, generando nuevo ID ===");
                _ctx.Entry(ent).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                galponId = await GenerateNextGalponIdAsync(effectiveCompanyId);
                ent.GalponId = galponId;
                _ctx.Galpones.Add(ent);
                await _ctx.SaveChangesAsync();
            }
            else
            {
                // Si no es un error de clave duplicada, relanzar la excepción
                throw;
            }
        }

        // Releer con proyección a detalle (para traer Farm/Nucleo/Company)
        return await GetDetailByIdAsync(ent.GalponId)
               ?? new GalponDtos.GalponDetailDto(
                    ent.GalponId, ent.GalponNombre, ent.NucleoId, ent.GranjaId,
                    ent.Ancho, ent.Largo, ent.TipoGalpon, ent.CompanyId,
                    ent.CreatedByUserId, ent.CreatedAt, ent.UpdatedByUserId, ent.UpdatedAt,
                    new FarmLiteDto(ent.Farm?.Id ?? dto.GranjaId, ent.Farm?.Name ?? "", ent.Farm?.RegionalId ?? 0, ent.Farm?.MunicipioId ?? 0,ent.Farm?.DepartamentoId ?? 0),
                    new SharedDtos.NucleoLiteDto(ent.NucleoId, "", ent.GranjaId),
                    new SharedDtos.CompanyLiteDto(
                        ent.CompanyId,
                        ent.Company?.Name ?? "",
                        ent.Company?.VisualPermissions ?? Array.Empty<string>(),
                        ent.Company?.MobileAccess ?? false,
                        ent.Company?.Identifier)
                    );
    }

    public async Task<GalponDtos.GalponDetailDto?> UpdateAsync(UpdateGalponDto dto)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == dto.GalponId);

        if (ent is null || ent.DeletedAt != null) return null;

        await EnsureFarmExists(dto.GranjaId);
        await EnsureNucleoExists(dto.NucleoId, dto.GranjaId);

        ent.GalponNombre   = dto.GalponNombre;
        ent.NucleoId       = dto.NucleoId;
        ent.GranjaId       = dto.GranjaId;
        ent.Ancho          = dto.Ancho;
        ent.Largo          = dto.Largo;
        ent.TipoGalpon     = dto.TipoGalpon;
        ent.UpdatedByUserId= _current.UserId;
        ent.UpdatedAt      = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();

        return await GetDetailByIdAsync(ent.GalponId);
    }

    public async Task<bool> DeleteAsync(string galponId)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == galponId);

        if (ent is null || ent.DeletedAt != null) return false;

        ent.DeletedAt       = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt       = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(string galponId)
    {
        var ent = await _ctx.Galpones.SingleOrDefaultAsync(x =>
            x.CompanyId == _current.CompanyId &&
            x.GalponId  == galponId);
        if (ent is null) return false;

        _ctx.Galpones.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Genera el siguiente ID de galpón basándose en el último creado
    /// Formato: G0001, G0002, G0003, etc.
    /// Verifica que el ID generado no exista antes de retornarlo.
    /// </summary>
    private async Task<string> GenerateNextGalponIdAsync(int companyId)
    {
        // Obtener todos los IDs de galpones que empiezan con "G" para esta empresa
        // Incluir eliminados porque la PK es única globalmente
        var allGalponIds = await _ctx.Galpones
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.GalponId.StartsWith("G"))
            .Select(g => g.GalponId)
            .ToListAsync();

        int maxNumber = 0;
        foreach (var id in allGalponIds)
        {
            var m = Regex.Match(id, @"^G(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int num))
            {
                if (num > maxNumber) maxNumber = num;
            }
        }

        // Generar el siguiente ID y verificar que no existe
        // Intentar hasta encontrar uno disponible (máximo 1000 intentos para evitar bucle infinito)
        for (int attempt = 1; attempt <= 1000; attempt++)
        {
            int nextNumber = maxNumber + attempt;
            string candidateId = $"G{nextNumber:D4}";

            // Verificar si el ID ya existe (incluyendo eliminados porque la PK es única)
            var exists = await _ctx.Galpones
                .AsNoTracking()
                .AnyAsync(g => g.GalponId == candidateId);

            if (!exists)
            {
                Console.WriteLine($"=== GenerateNextGalponIdAsync - ID generado: {candidateId} (intento {attempt}) ===");
                return candidateId;
            }

            Console.WriteLine($"=== GenerateNextGalponIdAsync - ID {candidateId} ya existe, intentando siguiente ===");
        }

        // Si llegamos aquí, algo está muy mal. Usar timestamp como fallback
        var timestamp = DateTime.UtcNow.Ticks % 100000;
        return $"G{timestamp:D4}";
    }

    private async Task EnsureFarmExists(int granjaId)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var exists = await _ctx.Farms.AsNoTracking()
            .AnyAsync(f => f.Id == granjaId && f.CompanyId == effectiveCompanyId);
        if (!exists) throw new InvalidOperationException("Granja no existe o no pertenece a la compañía.");
    }

    private async Task EnsureNucleoExists(string nucleoId, int granjaId)
    {
        var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
        var exists = await _ctx.Nucleos.AsNoTracking()
            .AnyAsync(n => n.NucleoId == nucleoId &&
                           n.GranjaId == granjaId &&
                           n.CompanyId == effectiveCompanyId);
        if (!exists) throw new InvalidOperationException("Núcleo no existe en la granja o no pertenece a la compañía.");
    }

    private static IQueryable<GalponDtos.GalponDetailDto> ProjectToDetail(IQueryable<Galpon> q)
    {
        return q.Include(g => g.Farm)
                .Include(g => g.Nucleo)
                .Include(g => g.Company)
                .Select(g => new GalponDtos.GalponDetailDto(
                    g.GalponId,
                    g.GalponNombre,
                    g.NucleoId,
                    g.GranjaId,
                    g.Ancho,
                    g.Largo,
                    g.TipoGalpon,
                    g.CompanyId,
                    g.CreatedByUserId,
                    g.CreatedAt,
                    g.UpdatedByUserId,
                    g.UpdatedAt,
                    new FarmLiteDto(g.Farm.Id, g.Farm.Name, g.Farm.RegionalId, g.Farm.DepartamentoId, g.Farm.MunicipioId),
                    new SharedDtos.NucleoLiteDto(g.Nucleo.NucleoId, g.Nucleo.NucleoNombre, g.Nucleo.GranjaId),
                    new SharedDtos.CompanyLiteDto(
                        g.CompanyId,
                        g.Company.Name,
                        g.Company.VisualPermissions ?? Array.Empty<string>(),
                        g.Company.MobileAccess,
                        g.Company.Identifier)

                ));
    }

    private static IQueryable<Galpon> ApplyOrder(IQueryable<Galpon> q, string sortBy, bool desc)
    {
        Expression<Func<Galpon, object>> key = sortBy?.ToLower() switch
        {
            "galpon_id"     => g => g.GalponId,
            "nucleo_id"     => g => g.NucleoId,
            "galpon_nombre" => g => g.GalponNombre,
            _               => g => g.GalponNombre
        };
        return desc ? q.OrderByDescending(key) : q.OrderBy(key);
    }
}
