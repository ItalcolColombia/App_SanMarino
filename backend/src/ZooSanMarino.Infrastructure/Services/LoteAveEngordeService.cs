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
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteAveEngordeService : AppInterfaces.ILoteAveEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;
    private readonly AppInterfaces.ICompanyResolver _companyResolver;
    private readonly AppInterfaces.IFarmService _farmService;
    private readonly AppInterfaces.ILocationScopeResolver _scopeResolver;

    public LoteAveEngordeService(
        ZooSanMarinoContext ctx,
        AppInterfaces.ICurrentUser current,
        AppInterfaces.ICompanyResolver companyResolver,
        AppInterfaces.IFarmService farmService,
        AppInterfaces.ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
        _scopeResolver = scopeResolver;
    }

    /// <summary>
    /// Filtro de alcance granular (user_farms.restrict_locations + user_farm_scopes), componible en
    /// SQL. Engorde no referencia la tabla lotes ⇒ el nivel LOTE del scope no aplica aquí: los lotes
    /// de engorde se gobiernan por galpón/núcleo visibles. Granjas no restringidas pasan intactas.
    /// <para>
    /// <paramref name="paraDestino"/> = true lo omite (selección de DESTINO en traslados): quien traslada
    /// necesita ver los lotes que RECIBEN aves aunque su alcance granular no incluya ese galpón. Mismo
    /// contrato que <c>LotePosturaLevanteService.AplicarScopeUbicacionAsync</c>; la restricción por granjas
    /// asignadas NO se relaja.
    /// </para>
    /// </summary>
    private async Task<IQueryable<LoteAveEngorde>> AplicarScopeUbicacionAsync(
        IQueryable<LoteAveEngorde> q, bool paraDestino = false)
    {
        if (paraDestino) return q;

        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count == 0) return q;

        var granjasRestringidas = restringidos.Keys.ToList();
        var galponesVisibles = restringidos.SelectMany(kv => kv.Value.GalponesVisibles).Distinct().ToList();
        var clavesNucleo = restringidos
            .SelectMany(kv => kv.Value.NucleosVisibles.Select(n => kv.Key + "|" + n))
            .ToList();

        return q.Where(l => !granjasRestringidas.Contains(l.GranjaId)
            || (l.GalponId != null && l.GalponId != "" && galponesVisibles.Contains(l.GalponId))
            || ((l.GalponId == null || l.GalponId == "") && l.NucleoId != null &&
                clavesNucleo.Contains(l.GranjaId.ToString() + "|" + l.NucleoId)));
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

    public async Task<IEnumerable<LoteAveEngordeDetailDto>> GetAllAsync(bool paraDestino = false)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0)
            return Array.Empty<LoteAveEngordeDetailDto>();
        IQueryable<LoteAveEngorde> q = _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && allowed.Contains(l.GranjaId));

        // Alcance granular núcleo/galpón (el nivel lote del scope no aplica a engorde).
        // Omitido al elegir DESTINO de un traslado.
        q = await AplicarScopeUbicacionAsync(q, paraDestino);

        q = q.OrderBy(l => l.LoteAveEngordeId);
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

        // Alcance granular núcleo/galpón (el nivel lote del scope no aplica a engorde)
        q = await AplicarScopeUbicacionAsync(q);

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
        IQueryable<LoteAveEngorde> q = _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId &&
                l.LoteAveEngordeId == loteAveEngordeId &&
                l.DeletedAt == null &&
                allowed.Contains(l.GranjaId));

        // Alcance granular: acceso directo también respeta el scope (fail-closed → null/404)
        q = await AplicarScopeUbicacionAsync(q);

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

        // Lote base (opcional) + numeración de corrida (solo Panamá, lo pide el front vía AutoNombrePorCorrida).
        // Cuando aplica, el backend es la fuente de verdad del número (MAX+1 por company+base+galpón, contando
        // también soft-deleted para no reusar números) y sobrescribe el nombre "{base} - {n}".
        var loteBaseId = await ResolverLoteBaseAsync(dto.LoteBaseEngordeId, companyId);
        string loteNombre = (dto.LoteNombre ?? string.Empty).Trim();
        int? numeroCorrida = null;
        if (dto.AutoNombrePorCorrida && loteBaseId.HasValue && !string.IsNullOrWhiteSpace(galponId))
        {
            var baseNombre = await _ctx.LoteBaseEngorde.AsNoTracking()
                .Where(b => b.Id == loteBaseId.Value)
                .Select(b => b.Nombre)
                .FirstAsync();
            var maxActual = await _ctx.LoteAveEngorde
                .Where(l => l.CompanyId == companyId && l.LoteBaseEngordeId == loteBaseId.Value && l.GalponId == galponId)
                .MaxAsync(l => (int?)l.NumeroCorrida);
            numeroCorrida = GestionLotesEngordeCalculos.SiguienteNumeroCorrida(maxActual);
            loteNombre = GestionLotesEngordeCalculos.ConstruirNombreCorrida(baseNombre, numeroCorrida.Value);
        }

        // Panamá (solo flujo interactivo, gateado por AutoNombrePorCorrida igual que la corrida):
        // si la granja tiene código ERP de engorde configurado, el lote nuevo lo captura (la granja
        // es la fuente; el front lo muestra readonly y el backend ignora lo que venga en el DTO).
        // Puente Panamá y migración masiva NO mandan el flag → conservan su LoteErp explícito
        // ("PA-{id}" = clave de idempotencia del puente / columna ERP del Excel). Granja sin
        // código = comportamiento actual (se respeta lo digitado, como en los demás países).
        var loteErp = dto.LoteErp;
        if (dto.AutoNombrePorCorrida)
        {
            var codigoErpGranja = await _ctx.Farms.AsNoTracking()
                .Where(f => f.Id == dto.GranjaId && f.CompanyId == companyId)
                .Select(f => f.CodigoErpEngorde)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(codigoErpGranja))
                loteErp = codigoErpGranja.Trim();
        }

        var ent = new LoteAveEngorde
        {
            LoteNombre = loteNombre,
            GranjaId = dto.GranjaId,
            NucleoId = nucleoId,
            GalponId = galponId,
            Regional = dto.Regional,
            FechaEncaset = FechasPuras.AnclarMediodiaUtc(dto.FechaEncaset),
            HoraEncasetamiento = dto.HoraEncasetamiento,
            FechaAlistamiento = FechasPuras.AnclarMediodiaUtc(dto.FechaAlistamiento),
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
            LoteErp = loteErp,
            LoteBaseEngordeId = loteBaseId,
            NumeroCorrida = numeroCorrida,
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow,
            PaisId = _current.PaisId,
            PaisNombre = paisNombre,
            EmpresaNombre = _current.ActiveCompanyName,
            EstadoOperativoLote = "Abierto"
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

        // Gate B1 — el más destructivo: cambiar AvesEncasetadas o FechaEncaset invalida la
        // liquidación congelada. Reabrir primero.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            ent.EstadoOperativoLote, OperacionLoteEngordeLiquidado.EditarLote);

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
        var nuevaFechaEncaset = FechasPuras.AnclarMediodiaUtc(dto.FechaEncaset);

        // La hora de encasetamiento decide el primer día con registro. En un lote que YA tiene
        // seguimientos (todos los de producción se crearon sin hora) informar una hora tardía dejaría
        // registros existentes fuera de la ventana válida. Se diagnostica ANTES de escribir: mejor un
        // 400 que explica qué registros estorban que un 200 que deja el lote inconsistente en silencio.
        if (dto.HoraEncasetamiento != ent.HoraEncasetamiento || nuevaFechaEncaset != ent.FechaEncaset)
        {
            var fechasSeguimiento = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => s.LoteAveEngordeId == ent.LoteAveEngordeId)
                .Select(s => s.Fecha)
                .ToListAsync();

            var horaRegla = EncasetamientoCalculos.HoraEfectiva(
                dto.HoraEncasetamiento, await PrimerRegistroPorHoraGate.ActivaAsync(_ctx, ent.CompanyId));
            var diag = EncasetamientoRetroactivoCalculos.Diagnosticar(
                nuevaFechaEncaset, horaRegla, fechasSeguimiento);
            if (!diag.Compatible)
                throw new InvalidOperationException(
                    EncasetamientoRetroactivoCalculos.MensajeIncompatible(diag, horaRegla));
        }

        ent.FechaEncaset = nuevaFechaEncaset;
        ent.HoraEncasetamiento = dto.HoraEncasetamiento;
        ent.FechaAlistamiento = FechasPuras.AnclarMediodiaUtc(dto.FechaAlistamiento);
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
        // Panamá: si la granja tiene código ERP configurado y el lote ya capturó uno, se conserva
        // el almacenado (histórico del ciclo; no se re-estampa ni se deja pisar desde el DTO).
        // Lotes viejos sin código pueden backfillearse a mano. Granja sin código = como hoy.
        var codigoErpGranjaUpd = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == dto.GranjaId && f.CompanyId == companyId)
            .Select(f => f.CodigoErpEngorde)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(codigoErpGranjaUpd) || string.IsNullOrWhiteSpace(ent.LoteErp))
            ent.LoteErp = dto.LoteErp;
        ent.LoteBaseEngordeId = await ResolverLoteBaseAsync(dto.LoteBaseEngordeId, companyId);
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

        // Gate B2 — un lote liquidado no se elimina (ni soft): reabrir primero.
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            ent.EstadoOperativoLote, OperacionLoteEngordeLiquidado.EliminarLote);

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

        // Gate B3 — el hard delete arrastra por FK todo el histórico del lote (la copia incluida).
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            ent.EstadoOperativoLote, OperacionLoteEngordeLiquidado.EliminarDefinitivoLote);

        _ctx.LoteAveEngorde.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<LoteAveEngordeDetailDto?> CerrarLoteAsync(int loteAveEngordeId, CerrarLoteAveEngordeRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ClosedByUserId))
            throw new ArgumentException("ClosedByUserId es requerido.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;

        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null &&
                allowed.Contains(x.GranjaId));
        if (ent is null) return null;

        if (string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote ya está cerrado.");

        ent.EstadoOperativoLote = "Cerrado";
        // Si el usuario eligió una fecha de liquidación, se ancla a medianoche UTC (misma
        // fecha "pura" que fecha_encaset/fecha_alistamiento); si no, comportamiento previo (ahora).
        ent.LiquidadoAt = request.FechaLiquidacion.HasValue
            ? DateTime.SpecifyKind(request.FechaLiquidacion.Value.Date, DateTimeKind.Utc)
            : DateTime.UtcNow;
        ent.LiquidadoPorUserId = request.ClosedByUserId.Trim();
        // Merma opcional digitada por Costos al liquidar (Parte B / R1).
        if (request.MermaUnidades.HasValue || request.MermaKilos.HasValue)
        {
            if (request.MermaUnidades is < 0 || request.MermaKilos is < 0)
                throw new ArgumentException("La merma (unidades/kilos) no puede ser negativa.");
            ent.MermaUnidades = request.MermaUnidades;
            ent.MermaKilos = request.MermaKilos;
            ent.MermaRegistradaAt = DateTime.UtcNow;
            ent.MermaRegistradaPorUserId = request.ClosedByUserId.Trim();
        }
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;

        // Liquidar ahora es transaccional: estado + copia congelada + resumen, todo o nada.
        // Si el congelado falla, la liquidación falla entera — sin copia no hay liquidación
        // (corrige el defecto del precedente de levante, cuyo snapshot lo dispara el front
        // en modo best-effort, fuera de la transacción del backend).
        await using var tx = await _ctx.Database.BeginTransactionAsync();

        // Panamá: si con este cierre no queda ningún lote abierto del lote base en la granja,
        // el código ERP de la granja avanza +1 (mismo SaveChanges ⇒ atómico con el cierre).
        await AvanzarCodigoErpGranjaSiCicloCerradoAsync(ent);
        await _ctx.SaveChangesAsync();

        // Congelar DESPUÉS de aplicar 'Cerrado': la fn fuerza el cierre en 0 con ese estado
        // (aves_iniciales = bajas + ventas). Congelar antes guardaría una foto distinta a la
        // que el usuario aprobó. La fn lee su detalle EN VIVO porque la cabecera y las filas
        // se insertan en un único statement (mismo snapshot).
        await LiquidacionCongeladaAplicador.CongelarAsync(
            _ctx, loteAveEngordeId, ent.LiquidadoPorUserId ?? request.ClosedByUserId.Trim(), "cierre");

        // El saldo persistido queda alineado con la copia desde el instante del cierre
        // (idempotente: IS DISTINCT FROM ⇒ 0 filas si ya coincidía).
        await SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync(_ctx, loteAveEngordeId);

        // Resumen aprobado (los campos tipados de la cabecera) — la misma fórmula que ven los
        // dos services de seguimiento (LiquidacionEngordeCalculos vía el aplicador).
        var resumen = await LiquidacionCongeladaAplicador.CalcularResumenVivoAsync(_ctx, loteAveEngordeId, companyId);
        if (resumen is not null)
            await LiquidacionCongeladaAplicador.ActualizarResumenCongeladoAsync(_ctx, loteAveEngordeId, resumen);

        await tx.CommitAsync();
        return await GetByIdAsync(loteAveEngordeId);
    }

    /// <summary>
    /// Re-congela la liquidación SIN reabrir el lote (endpoint admin): anula la copia vigente y
    /// crea una nueva con la fórmula de HOY (<c>origen='recongelado'</c>), refrescando el resumen.
    /// Escape hatch para «se descubrió un bug en la fórmula después de congelar». Auditado: queda
    /// la copia anterior anulada con quién y cuándo.
    /// </summary>
    public async Task<LoteAveEngordeDetailDto?> RecongelarLiquidacionAsync(int loteAveEngordeId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId es requerido.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;

        var ent = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null &&
                allowed.Contains(x.GranjaId));
        if (ent is null) return null;

        if (!string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote no está liquidado; no hay copia congelada que regenerar.");

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        var nuevaId = await LiquidacionCongeladaAplicador.RecongelarYRefrescarResumenAsync(
            _ctx, loteAveEngordeId, companyId, userId.Trim(), "recongelado");
        if (nuevaId is null)
            throw new InvalidOperationException("El lote no tiene copia congelada vigente (¿cerrado antes del backfill?). Reabra y liquide de nuevo.");
        await tx.CommitAsync();
        return await GetByIdAsync(loteAveEngordeId);
    }

    /// <summary>
    /// Panamá — avance automático del código ERP de la granja al cerrarse el ciclo del lote base.
    /// Con el lote recién marcado "Cerrado" (aún sin guardar), avanza el código SOLO si:
    /// (1) el lote tiene lote base; (2) la granja tiene <c>CodigoErpEngorde</c> configurado;
    /// (3) el <c>LoteErp</c> del lote coincide con el código vigente de la granja — guarda de ciclo:
    /// re-cerrar un lote reabierto de un ciclo viejo no vuelve a avanzar; y (4) no queda ningún
    /// otro lote abierto (no eliminado) de esa misma granja + lote base.
    /// Ej.: granja "4001017" y cierran todas las corridas del base 17 ⇒ pasa a "4001018"
    /// (… "4001099" → "4001100"). La reapertura NO decrementa: si hace falta, se corrige el
    /// código editando la granja.
    /// </summary>
    private async Task AvanzarCodigoErpGranjaSiCicloCerradoAsync(LoteAveEngorde ent)
    {
        if (!ent.LoteBaseEngordeId.HasValue) return;

        var farm = await _ctx.Farms
            .SingleOrDefaultAsync(f => f.Id == ent.GranjaId && f.CompanyId == ent.CompanyId && f.DeletedAt == null);
        if (farm is null || string.IsNullOrWhiteSpace(farm.CodigoErpEngorde)) return;

        var codigoVigente = farm.CodigoErpEngorde.Trim();
        if (!string.Equals((ent.LoteErp ?? string.Empty).Trim(), codigoVigente, StringComparison.Ordinal)) return;

        var quedanAbiertos = await _ctx.LoteAveEngorde.AnyAsync(l =>
            l.CompanyId == ent.CompanyId &&
            l.GranjaId == ent.GranjaId &&
            l.LoteBaseEngordeId == ent.LoteBaseEngordeId &&
            l.DeletedAt == null &&
            l.LoteAveEngordeId != ent.LoteAveEngordeId &&
            l.EstadoOperativoLote.ToLower() != "cerrado");
        if (quedanAbiertos) return;

        var siguiente = GestionLotesEngordeCalculos.SiguienteCodigoErpGranja(codigoVigente);
        if (siguiente is null) return;

        farm.CodigoErpEngorde = siguiente;
        farm.UpdatedByUserId = _current.UserId;
        farm.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Digita/edita la merma del lote (Costos) sin requerir cierre/reapertura. Parte B / R1.</summary>
    public async Task<LoteAveEngordeDetailDto?> ActualizarMermaAsync(int loteAveEngordeId, ActualizarMermaLoteEngordeRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RegistradoPorUserId))
            throw new ArgumentException("RegistradoPorUserId es requerido.");
        if (request.MermaUnidades is < 0) throw new ArgumentException("La merma en unidades no puede ser negativa.");
        if (request.MermaKilos is < 0) throw new ArgumentException("La merma en kilos no puede ser negativa.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;

        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null &&
                allowed.Contains(x.GranjaId));
        if (ent is null) return null;

        ent.MermaUnidades = request.MermaUnidades;
        ent.MermaKilos = request.MermaKilos;
        ent.MermaRegistradaAt = DateTime.UtcNow;
        ent.MermaRegistradaPorUserId = request.RegistradoPorUserId.Trim();
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;

        // La merma se digita después de liquidar POR DISEÑO («NO afectan el registro diario»),
        // así que este camino queda abierto con el lote cerrado — pero la copia congelada guarda
        // el resumen aprobado, y la merma es parte de él: se actualizan los 2 campos de la
        // cabecera vigente en el mismo SaveChanges (no toca el detalle).
        var copiaVigente = await _ctx.LiquidacionLoteEngordeCongelada
            .FirstOrDefaultAsync(c => c.LoteAveEngordeId == loteAveEngordeId && c.AnuladaAt == null);
        if (copiaVigente is not null)
        {
            copiaVigente.MermaUnidades = request.MermaUnidades;
            copiaVigente.MermaKilos = request.MermaKilos;
        }

        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(loteAveEngordeId);
    }

    public async Task<LoteAveEngordeDetailDto?> AbrirLoteAsync(int loteAveEngordeId, AbrirLoteAveEngordeRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.OpenedByUserId))
            throw new ArgumentException("OpenedByUserId es requerido.");
        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new ArgumentException("Motivo es requerido.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var allowed = await GetAllowedGranjaIdsForCurrentUserAsync(companyId);
        if (allowed.Count == 0) return null;

        var ent = await _ctx.LoteAveEngorde
            .SingleOrDefaultAsync(x =>
                x.LoteAveEngordeId == loteAveEngordeId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null &&
                allowed.Contains(x.GranjaId));
        if (ent is null) return null;

        if (!string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote no está cerrado; no aplica reapertura.");

        // Reapertura transaccional: la copia congelada se ANULA (no se borra — queda el rastro de
        // qué se había liquidado) y el lote vuelve al cálculo en vivo. Se anula PRIMERO, con el
        // usuario y el motivo reales; el trigger trg_lote_ave_engorde_anula_congelada queda como
        // red para cualquier UPDATE crudo que no pase por acá (encontrará la copia ya anulada).
        await using var tx = await _ctx.Database.BeginTransactionAsync();
        await LiquidacionCongeladaAplicador.AnularAsync(
            _ctx, loteAveEngordeId, request.OpenedByUserId.Trim(),
            $"Reapertura: {request.Motivo.Trim()}");

        ent.EstadoOperativoLote = "Abierto";
        ent.ReabiertoAt = DateTime.UtcNow;
        ent.ReabiertoPorUserId = request.OpenedByUserId.Trim();
        ent.MotivoReapertura = request.Motivo.Trim();
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetByIdAsync(loteAveEngordeId);
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

    /// <summary>
    /// Valida el lote base (opcional): debe existir, estar vivo y pertenecer a la empresa efectiva.
    /// Devuelve el id normalizado (null si no se envió).
    /// </summary>
    private async Task<int?> ResolverLoteBaseAsync(int? loteBaseEngordeId, int companyId)
    {
        if (!loteBaseEngordeId.HasValue) return null;
        var existe = await _ctx.LoteBaseEngorde
            .AsNoTracking()
            .AnyAsync(b => b.Id == loteBaseEngordeId.Value && b.CompanyId == companyId && b.DeletedAt == null);
        if (!existe)
            throw new InvalidOperationException("El lote base indicado no existe o no pertenece a la compañía.");
        return loteBaseEngordeId;
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
                l.FechaAlistamiento,
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
                l.EstadoOperativoLote,
                l.LiquidadoAt,
                l.LiquidadoPorUserId,
                l.ReabiertoAt,
                l.ReabiertoPorUserId,
                l.MotivoReapertura,
                l.MermaUnidades,
                l.MermaKilos,
                l.MermaRegistradaAt,
                l.MermaRegistradaPorUserId,
                l.AvesSobrante,
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
                    l.Farm.MunicipioId,
                    l.Farm.ClienteId,
                    l.Farm.Zona,
                    l.Farm.CertificadoGab,
                    l.Farm.Latitud,
                    l.Farm.Longitud
                ),
                l.Nucleo == null ? null : new NucleoLiteDto(l.Nucleo.NucleoId, l.Nucleo.NucleoNombre, l.Nucleo.GranjaId),
                l.Galpon == null ? null : new GalponLiteDto(l.Galpon.GalponId, l.Galpon.GalponNombre, l.Galpon.NucleoId, l.Galpon.GranjaId),
                l.LoteBaseEngordeId,
                l.LoteBaseEngorde == null ? null : l.LoteBaseEngorde.Nombre,
                l.NumeroCorrida,
                l.HoraEncasetamiento
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
