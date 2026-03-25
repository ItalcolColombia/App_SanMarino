// file: src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.LoteAveEngorde;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using AppInterfaces = ZooSanMarino.Application.Interfaces;
using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteAveEngordeService : AppInterfaces.ILoteAveEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;
    private readonly AppInterfaces.ICompanyResolver _companyResolver;
    private readonly AppInterfaces.IFarmService _farmService;

    public LoteAveEngordeService(
        ZooSanMarinoContext ctx,
        AppInterfaces.ICurrentUser current,
        AppInterfaces.ICompanyResolver companyResolver,
        AppInterfaces.IFarmService farmService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    /// <summary>
    /// Granjas donde el usuario puede operar: mismas reglas que <see cref="FarmService.GetAllAsync"/> con UserGuid
    /// (solo <c>UserFarms</c> asignadas + empresa activa). Sin asignaciones → conjunto vacío.
    /// </summary>
    private async Task<HashSet<int>> GetAllowedGranjaIdsForCurrentUserAsync(int companyId)
    {
        if (!_current.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");
        var farms = await _farmService.GetAllAsync(_current.UserGuid, companyId);
        return farms.Select(f => f.Id).ToHashSet();
    }

    public async Task<IEnumerable<LoteAveEngordeDetailDto>> GetAllAsync()
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0)
            return Array.Empty<LoteAveEngordeDetailDto>();
        var q = _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && allowed.Contains(l.GranjaId))
            .OrderBy(l => l.LoteAveEngordeId);
        return await ProjectToDetail(q).ToListAsync();
    }

    public async Task<CommonDtos.PagedResult<LoteAveEngordeDetailDto>> SearchAsync(LoteAveEngordeSearchRequest req)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var page = req.Page <= 0 ? 1 : req.Page;
        var pageSize = req.PageSize <= 0 ? 50 : req.PageSize;

        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0)
        {
            return new CommonDtos.PagedResult<LoteAveEngordeDetailDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = 0,
                Items = Array.Empty<LoteAveEngordeDetailDto>()
            };
        }

        if (req.GranjaId.HasValue && !allowed.Contains(req.GranjaId.Value))
        {
            return new CommonDtos.PagedResult<LoteAveEngordeDetailDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = 0,
                Items = Array.Empty<LoteAveEngordeDetailDto>()
            };
        }

        var q = _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && allowed.Contains(l.GranjaId));

        if (req.SoloActivos)
            q = q.Where(l => l.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            q = q.Where(l =>
                (l.LoteAveEngordeId.HasValue && l.LoteAveEngordeId.Value.ToString().Contains(term)) ||
                EF.Functions.Like((l.LoteNombre ?? "").ToLower(), $"%{term}%"));
        }

        if (req.GranjaId.HasValue) q = q.Where(l => l.GranjaId == req.GranjaId.Value);
        if (!string.IsNullOrWhiteSpace(req.NucleoId)) q = q.Where(l => l.NucleoId == req.NucleoId);
        if (!string.IsNullOrWhiteSpace(req.GalponId)) q = q.Where(l => l.GalponId == req.GalponId);
        if (req.FechaDesde.HasValue) q = q.Where(l => l.FechaEncaset >= req.FechaDesde!.Value);
        if (req.FechaHasta.HasValue) q = q.Where(l => l.FechaEncaset <= req.FechaHasta!.Value);
        if (!string.IsNullOrWhiteSpace(req.TipoLinea)) q = q.Where(l => l.TipoLinea == req.TipoLinea);
        if (!string.IsNullOrWhiteSpace(req.Raza)) q = q.Where(l => l.Raza == req.Raza);
        if (!string.IsNullOrWhiteSpace(req.Tecnico)) q = q.Where(l => l.Tecnico == req.Tecnico);

        q = ApplyOrder(q, req.SortBy, req.SortDesc);

        var total = await q.LongCountAsync();
        var items = await ProjectToDetail(q)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonDtos.PagedResult<LoteAveEngordeDetailDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    public async Task<LoteAveEngordeDetailDto?> GetByIdAsync(int loteAveEngordeId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;
        var q = _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId &&
                l.LoteAveEngordeId == loteAveEngordeId &&
                l.DeletedAt == null &&
                allowed.Contains(l.GranjaId));
        return await ProjectToDetail(q).SingleOrDefaultAsync();
    }

    public async Task<LoteAveEngordeDetailDto> CreateAsync(CreateLoteAveEngordeDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        await EnsureFarmExists(dto.GranjaId, companyId);
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (!allowed.Contains(dto.GranjaId))
            throw new InvalidOperationException("No tiene permiso para registrar lotes en esta granja (no está asignada a su usuario).");

        // Guía clásica (produccion_avicola_raw) o guía Ecuador (guia_genetica_ecuador_header), misma compañía
        if (string.IsNullOrWhiteSpace(dto.Raza) || !dto.AnoTablaGenetica.HasValue || dto.AnoTablaGenetica.Value <= 0)
            throw new InvalidOperationException("Raza y Año de tabla genética son requeridos y deben existir en la guía genética cargada.");

        if (!await ExisteGuiaGeneticaRazaAnioAsync(companyId, dto.Raza!, dto.AnoTablaGenetica.Value))
            throw new InvalidOperationException(
                $"No existe guía genética (clásica ni Ecuador) para la raza '{dto.Raza}' y el año '{dto.AnoTablaGenetica}' en la compañía actual. " +
                "Cargue la tabla en Guía genética o en Guía genética Ecuador.");

        string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
        string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

        if (!string.IsNullOrWhiteSpace(galponId))
        {
            var g = await _ctx.Galpones
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.GalponId == galponId && x.CompanyId == companyId);

            if (g is null)
                throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");
            if (g.GranjaId != dto.GranjaId)
                throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");
            if (!string.IsNullOrWhiteSpace(nucleoId) && !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");
            nucleoId ??= g.NucleoId;
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
        {
            var n = await _ctx.Nucleos
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.NucleoId == nucleoId && x.GranjaId == dto.GranjaId);
            if (n is null)
                throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
        }

        string? paisNombre = null;
        if (_current.PaisId.HasValue)
        {
            var pais = await _ctx.Paises.AsNoTracking()
                .Where(p => p.PaisId == _current.PaisId.Value)
                .Select(p => new { p.PaisNombre })
                .FirstOrDefaultAsync();
            paisNombre = pais?.PaisNombre;
        }

        var ent = new LoteAveEngorde
        {
            LoteNombre = (dto.LoteNombre ?? string.Empty).Trim(),
            GranjaId = dto.GranjaId,
            NucleoId = nucleoId,
            GalponId = galponId,
            Regional = dto.Regional,
            FechaEncaset = dto.FechaEncaset?.ToUniversalTime(),
            HembrasL = dto.HembrasL,
            MachosL = dto.MachosL,
            PesoInicialH = dto.PesoInicialH,
            PesoInicialM = dto.PesoInicialM,
            UnifH = dto.UnifH,
            UnifM = dto.UnifM,
            MortCajaH = dto.MortCajaH,
            MortCajaM = dto.MortCajaM,
            Raza = dto.Raza,
            AnoTablaGenetica = dto.AnoTablaGenetica,
            Linea = dto.Linea,
            TipoLinea = dto.TipoLinea,
            CodigoGuiaGenetica = dto.CodigoGuiaGenetica,
            LineaGeneticaId = dto.LineaGeneticaId,
            Tecnico = dto.Tecnico,
            Mixtas = dto.Mixtas,
            PesoMixto = dto.PesoMixto,
            AvesEncasetadas = dto.AvesEncasetadas,
            EdadInicial = dto.EdadInicial,
            LoteErp = dto.LoteErp,
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow,
            PaisId = _current.PaisId,
            PaisNombre = paisNombre,
            EmpresaNombre = _current.ActiveCompanyName
        };

        _ctx.LoteAveEngorde.Add(ent);
        await _ctx.SaveChangesAsync();

        var id = ent.LoteAveEngordeId ?? 0;
        var avesH = ent.HembrasL ?? 0;
        var avesM = ent.MachosL ?? 0;
        var avesX = ent.Mixtas ?? 0;
        if (avesH + avesM + avesX == 0 && (ent.AvesEncasetadas ?? 0) > 0)
            avesX = ent.AvesEncasetadas ?? 0;
        _ctx.HistorialLotePolloEngorde.Add(new HistorialLotePolloEngorde
        {
            CompanyId = companyId,
            TipoLote = "LoteAveEngorde",
            LoteAveEngordeId = id,
            LoteReproductoraAveEngordeId = null,
            TipoRegistro = "Inicio",
            AvesHembras = avesH,
            AvesMachos = avesM,
            AvesMixtas = avesX,
            FechaRegistro = DateTime.UtcNow,
            MovimientoId = null,
            CreatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync();

        var result = await GetByIdAsync(id);
        return result ?? throw new InvalidOperationException("No fue posible leer el lote de engorde recién creado.");
    }

    public async Task<LoteAveEngordeDetailDto?> UpdateAsync(UpdateLoteAveEngordeDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;
        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == dto.LoteAveEngordeId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null &&
                allowed.Contains(x.GranjaId));

        if (ent is null) return null;

        await EnsureFarmExists(dto.GranjaId, companyId);
        if (!allowed.Contains(dto.GranjaId))
            throw new InvalidOperationException("No tiene permiso para usar esta granja (no está asignada a su usuario).");

        if (string.IsNullOrWhiteSpace(dto.Raza) || !dto.AnoTablaGenetica.HasValue || dto.AnoTablaGenetica.Value <= 0)
            throw new InvalidOperationException("Raza y Año de tabla genética son requeridos y deben existir en la guía genética cargada.");

        if (!await ExisteGuiaGeneticaRazaAnioAsync(companyId, dto.Raza!, dto.AnoTablaGenetica.Value))
            throw new InvalidOperationException(
                $"No existe guía genética (clásica ni Ecuador) para la raza '{dto.Raza}' y el año '{dto.AnoTablaGenetica}' en la compañía actual. " +
                "Cargue la tabla en Guía genética o en Guía genética Ecuador.");

        string? nucleoId = string.IsNullOrWhiteSpace(dto.NucleoId) ? null : dto.NucleoId.Trim();
        string? galponId = string.IsNullOrWhiteSpace(dto.GalponId) ? null : dto.GalponId.Trim();

        if (!string.IsNullOrWhiteSpace(galponId))
        {
            var g = await _ctx.Galpones
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.GalponId == galponId && x.CompanyId == companyId);
            if (g is null)
                throw new InvalidOperationException("Galpón no existe o no pertenece a la compañía.");
            if (g.GranjaId != dto.GranjaId)
                throw new InvalidOperationException("Galpón no pertenece a la granja indicada.");
            if (!string.IsNullOrWhiteSpace(nucleoId) && !string.Equals(g.NucleoId, nucleoId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Galpón no pertenece al núcleo indicado.");
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
        {
            var n = await _ctx.Nucleos
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.NucleoId == nucleoId && x.GranjaId == dto.GranjaId);
            if (n is null)
                throw new InvalidOperationException("Núcleo no existe en la granja (o no pertenece a la compañía).");
        }

        // Actualizar datos de sesión (empresa, país) como en Lote
        ent.PaisId = _current.PaisId;
        ent.EmpresaNombre = _current.ActiveCompanyName;
        if (_current.PaisId.HasValue)
        {
            var pais = await _ctx.Paises.AsNoTracking()
                .Where(p => p.PaisId == _current.PaisId.Value)
                .Select(p => new { p.PaisNombre })
                .FirstOrDefaultAsync();
            ent.PaisNombre = pais?.PaisNombre;
        }
        else
            ent.PaisNombre = null;

        ent.LoteNombre = (dto.LoteNombre ?? string.Empty).Trim();
        ent.GranjaId = dto.GranjaId;
        ent.NucleoId = nucleoId ?? ent.NucleoId;
        ent.GalponId = galponId ?? ent.GalponId;
        ent.Regional = dto.Regional;
        ent.FechaEncaset = dto.FechaEncaset?.ToUniversalTime();
        ent.HembrasL = dto.HembrasL;
        ent.MachosL = dto.MachosL;
        ent.PesoInicialH = dto.PesoInicialH;
        ent.PesoInicialM = dto.PesoInicialM;
        ent.UnifH = dto.UnifH;
        ent.UnifM = dto.UnifM;
        ent.MortCajaH = dto.MortCajaH;
        ent.MortCajaM = dto.MortCajaM;
        ent.Raza = dto.Raza;
        ent.AnoTablaGenetica = dto.AnoTablaGenetica;
        ent.Linea = dto.Linea;
        ent.TipoLinea = dto.TipoLinea;
        ent.CodigoGuiaGenetica = dto.CodigoGuiaGenetica;
        ent.LineaGeneticaId = dto.LineaGeneticaId;
        ent.Tecnico = dto.Tecnico;
        ent.Mixtas = dto.Mixtas;
        ent.PesoMixto = dto.PesoMixto;
        ent.AvesEncasetadas = dto.AvesEncasetadas;
        ent.EdadInicial = dto.EdadInicial;
        ent.LoteErp = dto.LoteErp;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.LoteAveEngordeId ?? 0);
    }

    public async Task<bool> DeleteAsync(int loteAveEngordeId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return false;
        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                allowed.Contains(x.GranjaId));
        if (ent is null || ent.DeletedAt != null) return false;

        ent.DeletedAt = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> HardDeleteAsync(int loteAveEngordeId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return false;
        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                allowed.Contains(x.GranjaId));
        if (ent is null) return false;

        _ctx.LoteAveEngorde.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Acepta combinación raza+año en guía clásica (<see cref="ProduccionAvicolaRaw"/>) o en guía Ecuador activa
    /// (<see cref="GuiaGeneticaEcuadorHeader"/>), misma compañía.
    /// </summary>
    private async Task<bool> ExisteGuiaGeneticaRazaAnioAsync(int companyId, string raza, int anioTabla)
    {
        var razaNorm = raza.Trim().ToLower();
        var anioStr = anioTabla.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var existeClasica = await _ctx.ProduccionAvicolaRaw
            .AsNoTracking()
            .AnyAsync(p =>
                p.CompanyId == companyId &&
                p.DeletedAt == null &&
                p.Raza != null &&
                p.AnioGuia != null &&
                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                p.AnioGuia.Trim() == anioStr);

        if (existeClasica)
            return true;

        var razaTrim = raza.Trim();
        var existeEcuador = await _ctx.GuiaGeneticaEcuadorHeader
            .AsNoTracking()
            .AnyAsync(h =>
                h.CompanyId == companyId &&
                h.DeletedAt == null &&
                h.Estado == "active" &&
                h.AnioGuia == anioTabla &&
                EF.Functions.ILike(h.Raza, razaTrim));

        return existeEcuador;
    }

    private async Task EnsureFarmExists(int granjaId, int companyId)
    {
        var exists = await _ctx.Farms
            .AsNoTracking()
            .AnyAsync(f => f.Id == granjaId && f.CompanyId == companyId);
        if (!exists)
            throw new InvalidOperationException("Granja no existe o no pertenece a la compañía.");
    }

    private static IQueryable<LoteAveEngordeDetailDto> ProjectToDetail(IQueryable<LoteAveEngorde> q)
    {
        return q
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Select(l => new LoteAveEngordeDetailDto(
                l.LoteAveEngordeId ?? 0,
                l.LoteNombre,
                l.GranjaId,
                l.NucleoId,
                l.GalponId,
                l.Regional,
                l.FechaEncaset,
                l.HembrasL,
                l.MachosL,
                l.PesoInicialH,
                l.PesoInicialM,
                l.UnifH,
                l.UnifM,
                l.MortCajaH,
                l.MortCajaM,
                l.Raza,
                l.AnoTablaGenetica,
                l.Linea,
                l.TipoLinea,
                l.CodigoGuiaGenetica,
                l.LineaGeneticaId,
                l.Tecnico,
                l.Mixtas,
                l.PesoMixto,
                l.AvesEncasetadas,
                l.EdadInicial,
                l.LoteErp,
                l.EstadoTraslado,
                l.PaisId,
                l.PaisNombre,
                l.EmpresaNombre,
                l.CompanyId,
                l.CreatedByUserId,
                l.CreatedAt,
                l.UpdatedByUserId,
                l.UpdatedAt,
                new FarmLiteDto(
                    l.Farm.Id,
                    l.Farm.Name,
                    l.Farm.RegionalId,
                    l.Farm.DepartamentoId,
                    l.Farm.MunicipioId
                ),
                l.Nucleo == null ? null : new NucleoLiteDto(l.Nucleo.NucleoId, l.Nucleo.NucleoNombre, l.Nucleo.GranjaId),
                l.Galpon == null ? null : new GalponLiteDto(l.Galpon.GalponId, l.Galpon.GalponNombre, l.Galpon.NucleoId, l.Galpon.GranjaId)
            ));
    }

    private static IQueryable<LoteAveEngorde> ApplyOrder(IQueryable<LoteAveEngorde> q, string? sortBy, bool desc)
    {
        Expression<Func<LoteAveEngorde, object>> key = (sortBy ?? string.Empty).ToLower() switch
        {
            "lote_nombre" => l => l.LoteNombre ?? string.Empty,
            "lote_id" => l => l.LoteAveEngordeId ?? 0,
            "fecha_encaset" => l => l.FechaEncaset ?? DateTime.MinValue,
            _ => l => l.FechaEncaset ?? DateTime.MinValue
        };
        return desc ? q.OrderByDescending(key) : q.OrderBy(key);
    }
}
