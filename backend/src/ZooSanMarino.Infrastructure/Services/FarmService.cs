// src/ZooSanMarino.Infrastructure/Services/FarmService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.Calculos;                      // RoleAdminCalculos (lógica pura)
using ZooSanMarino.Application.DTOs;                          // FarmDto, Create/Update
using AppInterfaces = ZooSanMarino.Application.Interfaces;   // IFarmService, ICurrentUser
using CommonDtos   = ZooSanMarino.Application.DTOs.Common;   // PagedResult<>
using FarmDtos     = ZooSanMarino.Application.DTOs.Farms;    // Farm* DTOs (Tree, Detail, Search)

using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services
{
    public class FarmService : IFarmService
    {
        private readonly ZooSanMarinoContext _ctx;
        private readonly ICurrentUser _current;
        private readonly ICompanyResolver _companyResolver;
        private readonly IUserPermissionService _userPermissionService;

        public FarmService(ZooSanMarinoContext ctx, ICurrentUser current, ICompanyResolver companyResolver, IUserPermissionService userPermissionService)
        {
            _ctx = ctx;
            _current = current;
            _companyResolver = companyResolver;
            _userPermissionService = userPermissionService;
        }

        // ======================================================
        // HELPER METHODS
        // ======================================================
        
        /// <summary>
        /// Obtiene el CompanyId efectivo (validación por país deshabilitada temporalmente)
        /// </summary>
        private async Task<int> GetEffectiveCompanyIdAsync()
        {
            // Si hay una empresa activa especificada en el header, usarla
            if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            {
                var companyId = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
                if (companyId.HasValue)
                {
                    // Validación por país deshabilitada temporalmente
                    return companyId.Value;
                }
            }

            // Fallback al CompanyId del token JWT
            // Validación por país deshabilitada temporalmente
            
            return _current.CompanyId;
        }

        /// <summary>
        /// Resuelve el id de una opción de lista maestra (master_list_options) al regional_id de la tabla Regional
        /// por coincidencia de nombre (Value de la opción = RegionalNombre), para la compañía dada.
        /// </summary>
        private async Task<int?> ResolveRegionalIdFromOptionIdAsync(int? regionalOptionId, int companyId)
        {
            if (!regionalOptionId.HasValue || regionalOptionId.Value <= 0) return null;
            var opt = await _ctx.MasterListOptions
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == regionalOptionId.Value);
            if (opt?.Value == null) return null;
            var value = opt.Value.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            var regional = await _ctx.Regionales
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RegionalCia == companyId &&
                    (r.RegionalNombre != null && r.RegionalNombre.Trim().ToLower() == value.ToLower()));
            return regional?.RegionalId;
        }

        // ======================================================
        // BÚSQUEDA / LISTADO AVANZADO
        // ======================================================
        public async Task<CommonDtos.PagedResult<FarmDetailDto>> SearchAsync(FarmSearchRequest req)
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            
            var q = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == effectiveCompanyId);

            if (req.SoloActivos) q = q.Where(f => f.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                var term = req.Search.Trim().ToLower();
                q = q.Where(f =>
                    f.Name.ToLower().Contains(term) ||
                    f.Id.ToString().Contains(req.Search!.Trim())
                );
            }

            if (req.RegionalId.HasValue)      q = q.Where(f => f.RegionalId     == req.RegionalId.Value);
            if (req.DepartamentoId.HasValue)  q = q.Where(f => f.DepartamentoId == req.DepartamentoId.Value);
            if (req.CiudadId.HasValue)        q = q.Where(f => f.MunicipioId    == req.CiudadId.Value);
            if (!string.IsNullOrWhiteSpace(req.Status))
            {
                var s = NormalizeStatus(req.Status);
                q = q.Where(f => f.Status == s);
            }

            // ⬅️ NUEVO: filtro por País (via Departamento.PaisId)
            if (req.PaisId.HasValue)
            {
                var paisId = req.PaisId.Value;
                q = q.Where(f =>
                    _ctx.Set<Departamento>().Any(d => d.DepartamentoId == f.DepartamentoId && d.PaisId == paisId)
                );
            }

            // ⬅️ NUEVO: filtros Panamá por Zona y ClienteId
            if (!string.IsNullOrWhiteSpace(req.Zona))
            {
                var zona = req.Zona.Trim();
                q = q.Where(f => f.Zona == zona);
            }
            if (req.ClienteId.HasValue)
            {
                q = q.Where(f => f.ClienteId == req.ClienteId.Value);
            }

            q = ApplyOrder(q, req.SortBy, req.SortDesc);

            var page     = req.Page     <= 0 ? 1  : req.Page;
            var pageSize = req.PageSize <= 0 ? 20 : Math.Min(req.PageSize, 200);

            var total = await q.LongCountAsync();
            var items = await ProjectToDetail(q)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CommonDtos.PagedResult<FarmDetailDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            };
        }

        // ======================================================
        // DETALLE
        // ======================================================
        public async Task<FarmDetailDto?> GetDetailByIdAsync(int id)
        {
            var q = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == _current.CompanyId
                         && f.Id == id
                         && f.DeletedAt == null);

            return await ProjectToDetail(q).SingleOrDefaultAsync();
        }

        // ======================================================
        // ÁRBOL para cascadas (Farm → Núcleos → Galpones)
        // ======================================================
    public async Task<FarmTreeDto?> GetTreeByIdAsync(int farmId, bool soloActivos = true)
    {
        var farm = await _ctx.Farms
            .AsNoTracking()
            .Where(f => f.CompanyId == _current.CompanyId
                    && f.Id == farmId
                    && (!soloActivos || f.DeletedAt == null))
            .Select(f => new FarmLiteDto(
                f.Id,
                f.Name,
                f.RegionalId,     // ⬅️ es int? y el DTO acepta int?, no forzar 0
                f.DepartamentoId,
                f.MunicipioId,
                f.ClienteId,
                f.Zona,
                f.CertificadoGab,
                f.Latitud,
                f.Longitud
            ))
            .SingleOrDefaultAsync();

        if (farm is null) return null;

        var nucleos = await _ctx.Nucleos
            .AsNoTracking()
            .Where(n => n.GranjaId == farmId
                    && (!soloActivos || n.DeletedAt == null))
            .Select(n => new NucleoNodeDto(
                n.NucleoId, // ⬅️ si es int, no uses int.Parse
                n.GranjaId,
                n.NucleoNombre,
                _ctx.Galpones.Count(g =>
                    g.NucleoId == n.NucleoId &&
                    g.GranjaId == n.GranjaId &&
                    (!soloActivos || g.DeletedAt == null)
                ),
                _ctx.Lotes.Count(l =>
                    l.NucleoId == n.NucleoId &&
                    l.GranjaId == n.GranjaId &&
                    (!soloActivos || l.DeletedAt == null)
                )
            ))
            .OrderBy(n => n.NucleoNombre)
            .ToListAsync();

        // ⬅️ Faltaba este return
        return new FarmTreeDto(farm, nucleos);
    }

        // ======================================================
        // CRUD BÁSICO (compat)
        // ======================================================

        /// <inheritdoc />
        public async Task<IReadOnlyList<int>> GetAssignedFarmIdsForUserAsync(Guid userId, CancellationToken ct = default)
        {
            return await _ctx.UserFarms
                .AsNoTracking()
                .Where(uf => uf.UserId == userId)
                .Select(uf => uf.FarmId)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FarmDto>> GetFarmDtosByIdsInCompanyAsync(IReadOnlyCollection<int> farmIds, int companyId, CancellationToken ct = default)
        {
            if (farmIds == null || farmIds.Count == 0)
                return Array.Empty<FarmDto>();

            IQueryable<Farm> query = _ctx.Farms.AsNoTracking()
                .Where(f => f.DeletedAt == null && f.CompanyId == companyId && farmIds.Contains(f.Id));

            return await ToFarmDtoListAsync(query).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<FarmDto>> GetAssignedFarmsForCompanyAsync(Guid userId, int companyId, int? paisId = null)
        {
            var userFarmIds = await _ctx.UserFarms
                .AsNoTracking()
                .Where(uf => uf.UserId == userId)
                .Select(uf => uf.FarmId)
                .Distinct()
                .ToListAsync();

            IQueryable<Farm> query = _ctx.Farms.AsNoTracking()
                .Where(f => f.DeletedAt == null && f.CompanyId == companyId);

            if (paisId.HasValue)
            {
                var pid = paisId.Value;
                query = query.Where(f =>
                    _ctx.Set<Departamento>().Any(d => d.DepartamentoId == f.DepartamentoId && d.PaisId == pid));
            }

            if (userFarmIds.Count == 0)
                query = query.Where(f => f.Id == -1);
            else
                query = query.Where(f => userFarmIds.Contains(f.Id));

            return await ToFarmDtoListAsync(query);
        }

        /// <summary>
        /// Proyección a <see cref="FarmDto"/> + resolución de nombre regional desde lista maestra si aplica.
        /// </summary>
        private async Task<List<FarmDto>> ToFarmDtoListAsync(IQueryable<Farm> query)
        {
            var result = await query
                .OrderBy(f => f.Name)
                .Select(f => new FarmDto(
                    f.Id,
                    f.CompanyId,
                    f.Name,
                    f.RegionalId,
                    f.Status,
                    f.DepartamentoId,
                    f.MunicipioId,
                    _ctx.Set<Departamento>().Where(d => d.DepartamentoId == f.DepartamentoId).Select(d => d.DepartamentoNombre).FirstOrDefault(),
                    _ctx.Set<Municipio>().Where(m => m.MunicipioId == f.MunicipioId).Select(m => m.MunicipioNombre).FirstOrDefault(),
                    f.RegionalId.HasValue ? _ctx.Regionales.Where(r => r.RegionalCia == f.CompanyId && r.RegionalId == f.RegionalId.Value).Select(r => r.RegionalNombre).FirstOrDefault() : null,
                    _ctx.Companies.Where(c => c.Id == f.CompanyId).Select(c => c.Name).FirstOrDefault(),
                    f.ClienteId,
                    f.Zona,
                    f.CertificadoGab,
                    f.Latitud,
                    f.Longitud,
                    f.ManejaAlimentoPorGalpon,
                    f.CodigoErpEngorde
                ))
                .ToListAsync();

            var idsSinNombre = result.Where(x => x.RegionalNombre == null && x.RegionalId.HasValue).Select(x => x.RegionalId!.Value).Distinct().ToList();
            if (idsSinNombre.Count > 0)
            {
                var nombresOpcion = await _ctx.MasterListOptions
                    .AsNoTracking()
                    .Where(o => idsSinNombre.Contains(o.Id))
                    .ToDictionaryAsync(o => o.Id, o => o.Value ?? "");
                result = result.Select(f =>
                {
                    if (f.RegionalNombre != null || !f.RegionalId.HasValue) return f;
                    if (nombresOpcion.TryGetValue(f.RegionalId!.Value, out var nombre) && !string.IsNullOrWhiteSpace(nombre))
                        return new FarmDto(f.Id, f.CompanyId, f.Name, f.RegionalId, f.Status, f.DepartamentoId, f.CiudadId, f.DepartamentoNombre, f.CiudadNombre, nombre, f.CompanyNombre, f.ClienteId, f.Zona, f.CertificadoGab, f.Latitud, f.Longitud, f.ManejaAlimentoPorGalpon, f.CodigoErpEngorde);
                    return f;
                }).ToList();
            }

            return result;
        }

        public async Task<IEnumerable<FarmDto>> GetAllAsync(Guid? userId = null, int? companyId = null)
        {
            IQueryable<Farm> query = _ctx.Farms.AsNoTracking().Where(f => f.DeletedAt == null);

            // Verificar si el usuario actual es admin/administrador usando los países asignados
            // Si tiene acceso a todos los países, es admin
            var assignedCountries = await _userPermissionService.GetAssignedCountriesAsync(_current.UserId);
            var allCountriesCount = await _ctx.Set<Pais>().CountAsync();
            var isAdmin = assignedCountries.Count() >= allCountriesCount || 
                         await IsUserAdminOrAdministratorAsync(_current.UserId) ||
                         await IsSuperAdminAsync(_current.UserId);

            if (isAdmin)
            {
                Console.WriteLine($"=== FarmService.GetAllAsync - Usuario es admin/administrador ===");
                
                // Si se proporciona un companyId, filtrar por esa empresa
                if (companyId.HasValue)
                {
                    Console.WriteLine($"=== FarmService.GetAllAsync - Admin filtrando por empresa: {companyId.Value} ===");
                    query = query.Where(f => f.CompanyId == companyId.Value);
                }
                else
                {
                    Console.WriteLine($"=== FarmService.GetAllAsync - Admin sin filtro de empresa, devolviendo TODAS las granjas ===");
                    // No filtrar por empresa - mostrar todas las granjas
                }
            }
            else
            {
                // Si NO es admin, filtrar solo por las granjas de su empresa
                var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
                Console.WriteLine($"=== FarmService.GetAllAsync - Usuario NO es admin, filtrando por empresa: {effectiveCompanyId} ===");
                query = query.Where(f => f.CompanyId == effectiveCompanyId);
            }

            // Si se proporciona un userId, filtrar por las granjas asignadas directamente al usuario
            if (userId.HasValue)
            {
                Console.WriteLine($"=== FarmService.GetAllAsync - Filtrando por userId: {userId} ===");
                
                // Obtener las granjas asignadas directamente al usuario (UserFarms)
                var userFarmIds = await _ctx.UserFarms
                    .AsNoTracking()
                    .Where(uf => uf.UserId == userId.Value)
                    .Select(uf => uf.FarmId)
                    .Distinct()
                    .ToListAsync();

                Console.WriteLine($"=== Granjas asignadas directamente al usuario: {string.Join(", ", userFarmIds)} ===");
                
                // Si el usuario tiene granjas asignadas, filtrar solo por esas granjas
                if (userFarmIds.Any())
                {
                    Console.WriteLine($"✅ Filtrando por {userFarmIds.Count} granjas asignadas: [{string.Join(", ", userFarmIds)}]");
                    query = query.Where(f => userFarmIds.Contains(f.Id));
                }
                else
                {
                    Console.WriteLine("⚠️ Usuario no tiene granjas asignadas - devolviendo lista vacía");
                    // Devolver lista vacía filtrando por un ID que no existe
                    query = query.Where(f => f.Id == -1);
                }
            }
            else
            {
                Console.WriteLine("=== FarmService.GetAllAsync - Sin filtro de usuario ===");
            }

            var result = await ToFarmDtoListAsync(query);

            Console.WriteLine($"=== FarmService.GetAllAsync - Devolviendo {result.Count} granjas ===");
            return result;
        }

        // ======================================================
        // ASIGNAR GRANJAS — granjas asignables al configurar usuarios
        // ======================================================
        // Admin de Empresa (flag is_company_admin) o Super Admin → TODAS las granjas activas de la
        // empresa activa. Resto → solo las granjas de la empresa activa asignadas al propio usuario.
        public async Task<IEnumerable<FarmDto>> GetAssignableFarmsAsync()
        {
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

            var esAdminEmpresa = await IsCurrentUserCompanyAdminAsync(effectiveCompanyId);
            var esSuperAdmin   = await IsSuperAdminAsync(_current.UserId);
            var verTodas       = RoleAdminCalculos.PuedeVerTodasLasGranjas(esAdminEmpresa, esSuperAdmin);

            // Base: empresa activa + no eliminadas + activas ('A' o NULL por compatibilidad histórica).
            var query = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == effectiveCompanyId
                         && f.DeletedAt == null
                         && (f.Status == null || f.Status == "A"));

            if (!verTodas)
            {
                // Comportamiento actual para no-admin: solo las granjas asignadas al usuario logueado.
                var guid = _current.UserGuid;
                if (!guid.HasValue) return Array.Empty<FarmDto>();

                var userFarmIds = await _ctx.UserFarms
                    .AsNoTracking()
                    .Where(uf => uf.UserId == guid.Value)
                    .Select(uf => uf.FarmId)
                    .Distinct()
                    .ToListAsync();

                query = userFarmIds.Any()
                    ? query.Where(f => userFarmIds.Contains(f.Id))
                    : query.Where(f => f.Id == -1); // sin asignaciones → lista vacía
            }

            Console.WriteLine($"=== FarmService.GetAssignableFarmsAsync - company={effectiveCompanyId}, verTodas={verTodas} ===");
            var result = await ToFarmDtoListAsync(query);
            Console.WriteLine($"=== FarmService.GetAssignableFarmsAsync - Devolviendo {result.Count} granjas ===");
            return result;
        }

        /// <summary>
        /// El usuario actual es Administrador de Empresa para la empresa indicada si tiene algún
        /// <c>user_roles</c> en esa empresa cuyo rol tenga <c>is_company_admin = true</c>.
        /// </summary>
        private async Task<bool> IsCurrentUserCompanyAdminAsync(int companyId)
        {
            var guid = _current.UserGuid;
            if (!guid.HasValue) return false;

            return await _ctx.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == guid.Value && ur.CompanyId == companyId)
                .AnyAsync(ur => ur.Role.IsCompanyAdmin);
        }

        // ======================================================
        // FEATURE 13 — granjas válidas para traslado seguimiento
        // ======================================================
        public async Task<IEnumerable<FarmDto>> GetForTrasladoSeguimientoAsync()
        {
            // 1) CompanyId resuelto desde el token + header x-active-company
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();

            // 2) PaisId desde el header x-active-pais (cae al país asignado al usuario si no llega)
            var paisId = _current.PaisId;

            // 3) Filtrar: misma empresa + activas (status=A o deleted=null)
            var query = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == effectiveCompanyId && f.DeletedAt == null);

            // Si el modelo expone status, filtrar a "A"
            // (no rompemos si la columna está vacía/NULL — fallback a no eliminadas)
            query = query.Where(f => f.Status == null || f.Status == "A");

            // 4) Filtrar por país via Departamento.PaisId si tenemos paisId
            if (paisId.HasValue)
            {
                var pid = paisId.Value;
                query = query.Where(f =>
                    _ctx.Set<Departamento>().Any(d => d.DepartamentoId == f.DepartamentoId && d.PaisId == pid)
                );
            }

            Console.WriteLine($"=== FarmService.GetForTrasladoSeguimientoAsync - company={effectiveCompanyId}, pais={paisId} ===");
            var result = await ToFarmDtoListAsync(query);
            Console.WriteLine($"=== FarmService.GetForTrasladoSeguimientoAsync - Devolviendo {result.Count} granjas ===");
            return result;
        }

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

        public async Task<FarmDto?> GetByIdAsync(int id)
        {
            var dto = await _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == _current.CompanyId && f.DeletedAt == null && f.Id == id)
                .Select(f => new FarmDto(
                    f.Id,
                    f.CompanyId,
                    f.Name,
                    f.RegionalId,
                    f.Status,
                    f.DepartamentoId,
                    f.MunicipioId,
                    _ctx.Set<Departamento>().Where(d => d.DepartamentoId == f.DepartamentoId).Select(d => d.DepartamentoNombre).FirstOrDefault(),
                    _ctx.Set<Municipio>().Where(m => m.MunicipioId == f.MunicipioId).Select(m => m.MunicipioNombre).FirstOrDefault(),
                    f.RegionalId.HasValue ? _ctx.Regionales.Where(r => r.RegionalCia == f.CompanyId && r.RegionalId == f.RegionalId.Value).Select(r => r.RegionalNombre).FirstOrDefault() : null,
                    _ctx.Companies.Where(c => c.Id == f.CompanyId).Select(c => c.Name).FirstOrDefault(),
                    f.ClienteId,
                    f.Zona,
                    f.CertificadoGab,
                    f.Latitud,
                    f.Longitud,
                    f.ManejaAlimentoPorGalpon,
                    f.CodigoErpEngorde
                ))
                .SingleOrDefaultAsync();

            if (dto == null) return null;
            if (dto.RegionalNombre == null && dto.RegionalId.HasValue)
            {
                var nombreOpcion = await _ctx.MasterListOptions.AsNoTracking().Where(o => o.Id == dto.RegionalId.Value).Select(o => o.Value).FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(nombreOpcion))
                    dto = new FarmDto(dto.Id, dto.CompanyId, dto.Name, dto.RegionalId, dto.Status, dto.DepartamentoId, dto.CiudadId, dto.DepartamentoNombre, dto.CiudadNombre, nombreOpcion, dto.CompanyNombre, dto.ClienteId, dto.Zona, dto.CertificadoGab, dto.Latitud, dto.Longitud, dto.ManejaAlimentoPorGalpon, dto.CodigoErpEngorde);
            }
            return dto;
        }

        public async Task<FarmDto> CreateAsync(CreateFarmDto dto)
        {
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre es obligatorio.", nameof(dto.Name));

            if (!dto.DepartamentoId.HasValue)
                throw new ArgumentException("DepartamentoId es obligatorio.", nameof(dto.DepartamentoId));

            if (!dto.CiudadId.HasValue)
                throw new ArgumentException("CiudadId es obligatorio.", nameof(dto.CiudadId));

            // NUEVA VALIDACIÓN: Verificar que el usuario puede crear granjas en este país
            var departamentoEntity = await _ctx.Set<Departamento>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DepartamentoId == dto.DepartamentoId.Value);
            
            if (departamentoEntity == null)
                throw new ArgumentException("El departamento especificado no existe.", nameof(dto.DepartamentoId));

            // Obtener la empresa activa del usuario
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            
            // Verificar si el usuario puede crear granjas en este país
            // Primero verificar si la empresa activa tiene el país asignado
            var companyHasCountry = await _ctx.CompanyPaises
                .AsNoTracking()
                .AnyAsync(cp => cp.CompanyId == effectiveCompanyId && cp.PaisId == departamentoEntity.PaisId);
            
            // Si la empresa activa tiene el país, permitir crear
            // Si no, verificar otros permisos (admin, otras empresas, etc.)
            var canCreateInCountry = companyHasCountry || 
                await _userPermissionService.CanCreateFarmInCountryAsync(_current.UserId, departamentoEntity.PaisId);
            
            if (!canCreateInCountry)
            {
                // Obtener información para el mensaje de error
                var userIdGuid = _current.UserGuid;
                var userCompanies = userIdGuid.HasValue 
                    ? await _ctx.UserCompanies
                        .AsNoTracking()
                        .Where(uc => uc.UserId == userIdGuid.Value)
                        .Select(uc => uc.CompanyId)
                        .Distinct()
                        .ToListAsync()
                    : new List<int>();
                
                var companyPaises = userCompanies.Any()
                    ? await _ctx.CompanyPaises
                        .AsNoTracking()
                        .Where(cp => userCompanies.Contains(cp.CompanyId))
                        .Select(cp => cp.PaisId)
                        .Distinct()
                        .ToListAsync()
                    : new List<int>();
                
                var paisNombre = await _ctx.Set<Pais>()
                    .AsNoTracking()
                    .Where(p => p.PaisId == departamentoEntity.PaisId)
                    .Select(p => p.PaisNombre)
                    .FirstOrDefaultAsync() ?? "desconocido";
                
                var mensaje = userCompanies.Any()
                    ? $"No tienes permisos para crear granjas en {paisNombre}. " +
                      $"Tus empresas están asignadas a los siguientes países: {string.Join(", ", companyPaises)}. " +
                      $"Asegúrate de que la empresa tenga asignado el país {paisNombre} en la configuración de empresa-país."
                    : $"No tienes permisos para crear granjas en {paisNombre}. " +
                      $"No tienes empresas asignadas o tus empresas no tienen países configurados.";
                
                throw new UnauthorizedAccessException(mensaje);
            }

            var normalizedStatus = NormalizeStatus(dto.Status);
            // effectiveCompanyId ya está definido arriba (línea 275)

            var dup = await _ctx.Farms
                .AsNoTracking()
                .AnyAsync(f => f.CompanyId == effectiveCompanyId &&
                               f.Name.ToLower() == name.ToLower() &&
                               f.DeletedAt == null);
            if (dup) throw new InvalidOperationException("Ya existe una granja con ese nombre en la compañía.");

            var regionalId = dto.RegionalId ?? await ResolveRegionalIdFromOptionIdAsync(dto.RegionalOptionId, effectiveCompanyId);
            var entity = new Farm
            {
                CompanyId       = effectiveCompanyId,
                Name            = name,
                RegionalId      = regionalId,                // null OK; puede venir de dto o resuelto desde RegionalOptionId
                Status          = normalizedStatus,          // 'A'/'I'
                DepartamentoId  = dto.DepartamentoId!.Value,
                MunicipioId     = dto.CiudadId!.Value,       // DTO ciudadId → entidad MunicipioId
                // Nuevos campos (Panamá)
                ClienteId       = dto.ClienteId,
                Zona            = string.IsNullOrWhiteSpace(dto.Zona) ? null : dto.Zona!.Trim(),
                CertificadoGab  = dto.CertificadoGab,         // default false si no viene (DTO ya lo trae como false)
                Latitud         = dto.Latitud,
                Longitud        = dto.Longitud,
                ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon,   // null = hereda empresa
                CodigoErpEngorde = NormalizeCodigoErpEngorde(dto.CodigoErpEngorde),
                CreatedByUserId = _current.UserId,
                CreatedAt       = DateTime.UtcNow
            };

            _ctx.Farms.Add(entity);
            await _ctx.SaveChangesAsync();

            // Asignar automáticamente la granja al usuario que la creó
            var creatorUserGuid = _current.UserGuid;
            if (creatorUserGuid.HasValue)
            {
                // Verificar si ya existe la relación (por si acaso)
                var existingUserFarm = await _ctx.UserFarms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(uf => uf.UserId == creatorUserGuid.Value && uf.FarmId == entity.Id);
                
                if (existingUserFarm == null)
                {
                    // Verificar si el usuario ya tiene granjas asignadas para determinar si esta debe ser la default
                    var hasOtherFarms = await _ctx.UserFarms
                        .AsNoTracking()
                        .AnyAsync(uf => uf.UserId == creatorUserGuid.Value);
                    
                    // Crear la relación usuario-granja
                    var userFarm = new UserFarm
                    {
                        UserId = creatorUserGuid.Value,
                        FarmId = entity.Id,
                        IsAdmin = false, // El creador no es admin de la granja por defecto
                        IsDefault = !hasOtherFarms, // Si es la primera granja, marcarla como default
                        CreatedAt = DateTime.UtcNow,
                        CreatedByUserId = creatorUserGuid.Value // El usuario se asigna a sí mismo
                    };
                    
                    _ctx.UserFarms.Add(userFarm);
                    await _ctx.SaveChangesAsync();
                    
                    Console.WriteLine($"FarmService.CreateAsync - Granja {entity.Id} asignada automáticamente al usuario {creatorUserGuid.Value}");
                }
            }
            else
            {
                    Console.WriteLine($"FarmService.CreateAsync - WARNING: No se pudo obtener UserGuid, no se asignó la granja automáticamente");
            }

            // Obtener nombres de departamento y municipio
            var departamentoNombre = await _ctx.Set<Departamento>()
                .AsNoTracking()
                .Where(d => d.DepartamentoId == entity.DepartamentoId)
                .Select(d => d.DepartamentoNombre)
                .FirstOrDefaultAsync();
            
            var ciudadNombre = await _ctx.Set<Municipio>()
                .AsNoTracking()
                .Where(m => m.MunicipioId == entity.MunicipioId)
                .Select(m => m.MunicipioNombre)
                .FirstOrDefaultAsync();
            
            string? regionalNombre = null;
            if (entity.RegionalId.HasValue)
            {
                regionalNombre = await _ctx.Regionales
                    .AsNoTracking()
                    .Where(r => r.RegionalCia == entity.CompanyId && r.RegionalId == entity.RegionalId.Value)
                    .Select(r => r.RegionalNombre)
                    .FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(regionalNombre))
                    regionalNombre = await _ctx.MasterListOptions.AsNoTracking().Where(o => o.Id == entity.RegionalId.Value).Select(o => o.Value).FirstOrDefaultAsync();
            }

            var companyNombre = await _ctx.Companies.AsNoTracking().Where(c => c.Id == entity.CompanyId).Select(c => c.Name).FirstOrDefaultAsync();

            return new FarmDto(
                entity.Id,
                entity.CompanyId,
                entity.Name,
                entity.RegionalId,
                entity.Status,
                entity.DepartamentoId,
                entity.MunicipioId,
                departamentoNombre,
                ciudadNombre,
                regionalNombre,
                companyNombre,
                entity.ClienteId,
                entity.Zona,
                entity.CertificadoGab,
                entity.Latitud,
                entity.Longitud,
                entity.ManejaAlimentoPorGalpon,
                entity.CodigoErpEngorde
            );
        }

        public async Task<FarmDto?> UpdateAsync(UpdateFarmDto dto)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == dto.Id && f.CompanyId == _current.CompanyId);

            if (entity is null || entity.DeletedAt != null) return null;

            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre es obligatorio.", nameof(dto.Name));

            if (!dto.DepartamentoId.HasValue)
                throw new ArgumentException("DepartamentoId es obligatorio.", nameof(dto.DepartamentoId));

            if (!dto.CiudadId.HasValue)
                throw new ArgumentException("CiudadId es obligatorio.", nameof(dto.CiudadId));

            if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var dup = await _ctx.Farms
                    .AsNoTracking()
                    .AnyAsync(f => f.CompanyId == _current.CompanyId &&
                                   f.Id != dto.Id &&
                                   f.Name.ToLower() == name.ToLower() &&
                                   f.DeletedAt == null);
                if (dup) throw new InvalidOperationException("Ya existe otra granja con ese nombre en la compañía.");
            }

            entity.Name           = name;
            entity.RegionalId     = dto.RegionalId
                ?? await ResolveRegionalIdFromOptionIdAsync(dto.RegionalOptionId, entity.CompanyId)
                ?? entity.RegionalId;  // null OK; puede venir de dto, resuelto desde RegionalOptionId, o conservar actual
            entity.Status         = NormalizeStatus(dto.Status);    // 'A'/'I'
            entity.DepartamentoId = dto.DepartamentoId!.Value;
            entity.MunicipioId    = dto.CiudadId!.Value;
            // Nuevos campos (Panamá)
            entity.ClienteId      = dto.ClienteId;
            entity.Zona           = string.IsNullOrWhiteSpace(dto.Zona) ? null : dto.Zona!.Trim();
            entity.CertificadoGab = dto.CertificadoGab;
            entity.Latitud        = dto.Latitud;
            entity.Longitud       = dto.Longitud;
            entity.ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon;   // null = hereda empresa
            entity.CodigoErpEngorde = NormalizeCodigoErpEngorde(dto.CodigoErpEngorde);
            entity.UpdatedByUserId= _current.UserId;
            entity.UpdatedAt      = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();

            // Obtener nombres de departamento y municipio
            var departamentoNombre = await _ctx.Set<Departamento>()
                .AsNoTracking()
                .Where(d => d.DepartamentoId == entity.DepartamentoId)
                .Select(d => d.DepartamentoNombre)
                .FirstOrDefaultAsync();
            
            var ciudadNombre = await _ctx.Set<Municipio>()
                .AsNoTracking()
                .Where(m => m.MunicipioId == entity.MunicipioId)
                .Select(m => m.MunicipioNombre)
                .FirstOrDefaultAsync();
            
            string? regionalNombre = null;
            if (entity.RegionalId.HasValue)
            {
                regionalNombre = await _ctx.Regionales
                    .AsNoTracking()
                    .Where(r => r.RegionalCia == entity.CompanyId && r.RegionalId == entity.RegionalId.Value)
                    .Select(r => r.RegionalNombre)
                    .FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(regionalNombre))
                    regionalNombre = await _ctx.MasterListOptions.AsNoTracking().Where(o => o.Id == entity.RegionalId.Value).Select(o => o.Value).FirstOrDefaultAsync();
            }

            var companyNombre = await _ctx.Companies.AsNoTracking().Where(c => c.Id == entity.CompanyId).Select(c => c.Name).FirstOrDefaultAsync();

            return new FarmDto(
                entity.Id,
                entity.CompanyId,
                entity.Name,
                entity.RegionalId,
                entity.Status,
                entity.DepartamentoId,
                entity.MunicipioId,
                departamentoNombre,
                ciudadNombre,
                regionalNombre,
                companyNombre,
                entity.ClienteId,
                entity.Zona,
                entity.CertificadoGab,
                entity.Latitud,
                entity.Longitud,
                entity.ManejaAlimentoPorGalpon,
                entity.CodigoErpEngorde
            );
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == id && f.CompanyId == _current.CompanyId);
            if (entity is null || entity.DeletedAt != null) return false;

            var now = DateTime.UtcNow;

            entity.DeletedAt       = now;
            entity.UpdatedByUserId = _current.UserId;
            entity.UpdatedAt       = now;

            // Cascada: deshabilitar (soft-delete) los núcleos y galpones de la granja que sigan
            // activos (GestionGranjasCalculos.RequiereInhabilitar → DeletedAt == null). Mismo
            // SaveChanges = operación atómica.
            var nucleos = await _ctx.Nucleos
                .Where(n => n.GranjaId == entity.Id && n.CompanyId == entity.CompanyId && n.DeletedAt == null)
                .ToListAsync();
            foreach (var n in nucleos)
            {
                n.DeletedAt       = now;
                n.UpdatedByUserId = _current.UserId;
                n.UpdatedAt       = now;
            }

            var galpones = await _ctx.Galpones
                .Where(g => g.GranjaId == entity.Id && g.CompanyId == entity.CompanyId && g.DeletedAt == null)
                .ToListAsync();
            foreach (var g in galpones)
            {
                g.DeletedAt       = now;
                g.UpdatedByUserId = _current.UserId;
                g.UpdatedAt       = now;
            }

            await _ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HardDeleteAsync(int id)
        {
            var entity = await _ctx.Farms
                .SingleOrDefaultAsync(f => f.Id == id && f.CompanyId == _current.CompanyId);
            if (entity is null) return false;

            // Cascada dura: eliminar físicamente núcleos y galpones de la granja (consistencia con
            // el borrado de la granja; evita huérfanos con FK a una granja inexistente).
            var galpones = await _ctx.Galpones
                .Where(g => g.GranjaId == entity.Id && g.CompanyId == entity.CompanyId)
                .ToListAsync();
            if (galpones.Count > 0) _ctx.Galpones.RemoveRange(galpones);

            var nucleos = await _ctx.Nucleos
                .Where(n => n.GranjaId == entity.Id && n.CompanyId == entity.CompanyId)
                .ToListAsync();
            if (nucleos.Count > 0) _ctx.Nucleos.RemoveRange(nucleos);

            _ctx.Farms.Remove(entity);
            await _ctx.SaveChangesAsync();
            return true;
        }

        // ======================================================
        // PANAMÁ — Filtro de granjas por zona del usuario logueado
        // ======================================================
        /// <summary>
        /// Devuelve granjas filtradas por zona del usuario logueado cuando el país activo
        /// es PANAMA. Para otros países, conserva el comportamiento existente (granjas
        /// asignadas vía UserFarm).
        /// </summary>
        public async Task<IEnumerable<FarmDto>> GetByZonaUsuarioAsync(string? paisActivo, CancellationToken ct = default)
        {
            var companyId = await GetEffectiveCompanyIdAsync();
            var userGuid  = _current.UserGuid;          // Guid del usuario (PK en tabla users)

            // Lee la zona del usuario actual (si la tiene)
            string? userZona = null;
            if (userGuid.HasValue)
            {
                userZona = await _ctx.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userGuid.Value)
                    .Select(u => u.Zona)
                    .FirstOrDefaultAsync(ct);
            }

            var query = _ctx.Farms
                .AsNoTracking()
                .Where(f => f.CompanyId == companyId && f.DeletedAt == null);

            var esPanama = !string.IsNullOrWhiteSpace(paisActivo) &&
                           string.Equals(paisActivo.Trim(), "PANAMA", StringComparison.OrdinalIgnoreCase);

            if (esPanama && !string.IsNullOrWhiteSpace(userZona))
            {
                // Filtrado por zona: el usuario solo ve granjas de su zona asignada
                query = query.Where(f => f.Zona == userZona);
            }
            else if (userGuid.HasValue)
            {
                // Comportamiento normal: granjas asignadas al usuario vía UserFarm (PK Guid)
                query = query.Where(f => _ctx.UserFarms.Any(uf =>
                    uf.UserId == userGuid.Value && uf.FarmId == f.Id));
            }
            else
            {
                // Sin Guid disponible — no devolver nada (evita exponer granjas sin contexto)
                query = query.Where(f => false);
            }

            return await query
                .OrderBy(f => f.Name)
                .Select(f => new FarmDto(
                    f.Id, f.CompanyId, f.Name, f.RegionalId, f.Status, f.DepartamentoId, f.MunicipioId,
                    null, null, null, null,
                    f.ClienteId, f.Zona, f.CertificadoGab, f.Latitud, f.Longitud, f.ManejaAlimentoPorGalpon,
                    f.CodigoErpEngorde))
                .ToListAsync(ct);
        }

        // ======================================================
        // Helpers
        // ======================================================
        private static string NormalizeStatus(string? status)
        {
            var s = (status ?? "A").Trim().ToUpperInvariant();
            return (s == "A" || s == "I") ? s : "A";
        }

        /// <summary>
        /// Código ERP de engorde de la granja (Panamá): trim, vacío → null; si viene con algo
        /// que no sean dígitos se rechaza (el avance +1 al cerrar el ciclo exige código numérico).
        /// </summary>
        private static string? NormalizeCodigoErpEngorde(string? codigo)
        {
            var c = (codigo ?? string.Empty).Trim();
            if (c.Length == 0) return null;
            if (!GestionLotesEngordeCalculos.EsCodigoErpGranjaValido(c))
                throw new ArgumentException("El código ERP de engorde de la granja debe contener solo dígitos (máx. 18).");
            return c;
        }

        private static IQueryable<FarmDetailDto> ProjectToDetail(IQueryable<Farm> q)
        {
            return q.Select(f => new FarmDetailDto(
                f.Id,
                f.CompanyId,
                f.Name,
                f.RegionalId,                    // ⬅️ int?
                f.Status,
                f.DepartamentoId,
                f.MunicipioId,                   // → CiudadId
                f.CreatedByUserId,
                f.CreatedAt,
                f.UpdatedByUserId,
                f.UpdatedAt,
                f.Nucleos.Count(),
                f.Nucleos.SelectMany(n => n.Galpones).Count(),
                f.Lotes.Count(),
                f.ClienteId,
                f.Zona,
                f.CertificadoGab,
                f.Latitud,
                f.Longitud,
                f.ManejaAlimentoPorGalpon,
                f.CodigoErpEngorde
            ));
        }

        private static IQueryable<Farm> ApplyOrder(IQueryable<Farm> q, string sortBy, bool desc)
        {
            Expression<Func<Farm, object>> key = (sortBy ?? "").ToLower() switch
            {
                "name"             => f => f.Name,
                "regional_id"      => f => (object?)f.RegionalId ?? 0,         // null-safe
                "departamento_id"  => f => f.DepartamentoId,
                "ciudad_id"        => f => f.MunicipioId,
                "created_at"       => f => (object?)f.CreatedAt ?? DateTime.MinValue,
                _                  => f => f.Name
            };
            return desc ? q.OrderByDescending(key) : q.OrderBy(key);
        }
    }
}
