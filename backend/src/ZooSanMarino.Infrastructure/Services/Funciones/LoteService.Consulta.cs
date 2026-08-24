// file: src/ZooSanMarino.Infrastructure/Services/Funciones/LoteService.Consulta.cs
// Lectura de lotes: listado simple, levante, busqueda paginada, detalle por id y resumen de mortalidad.
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.Calculos;       // GuiaGeneticaRequisitoCalculos (logica pura)
using ZooSanMarino.Application.DTOs;           // LoteDto, Create/Update
using ZooSanMarino.Application.DTOs.Lotes;     // LoteDetailDto, LoteSearchRequest, TrasladoLoteRequestDto, TrasladoLoteResponseDto, HistorialTrasladoLoteDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using AppInterfaces = ZooSanMarino.Application.Interfaces;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

using ZooSanMarino.Domain.Entities;
using HistorialTrasladoLote = ZooSanMarino.Domain.Entities.HistorialTrasladoLote;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class LoteService
{
    /// <summary>
    /// Listado simple con informacion completa de relaciones. Excluye siempre los lotes "hijo de
    /// produccion" (Fase == Produccion y LotePadreId != null) para no duplicar en pantalla el lote
    /// padre y el registro creado para seguimiento diario.
    /// </summary>
    public async Task<IEnumerable<LoteDetailDto>> GetAllAsync(string? fase = null, bool paraDestino = false)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var q = _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null);

        // Scoping por granjas asignadas al usuario (UserFarms) — alineado con Núcleos/Galpones
        // (mismo alcance que la tab Granjas, incluso super-admin). Fail-closed: sin
        // usuario/asignaciones → vacío. Cierra el gap histórico de este servicio (solo CompanyId).
        var assignedFarmIds = await GetAssignedFarmIdsForCurrentUserAsync();
        if (assignedFarmIds == null || assignedFarmIds.Count == 0)
            return Array.Empty<LoteDetailDto>();
        q = q.Where(l => assignedFarmIds.Contains(l.GranjaId));

        // Alcance granular núcleo/galpón/lote (omitido al elegir DESTINO de traslados)
        q = await AplicarScopeUbicacionAsync(q, paraDestino);

        // No mostrar lotes hijo de producción (el " - Prod" creado para registro diario)
        q = q.Where(l => !(l.Fase == "Produccion" && l.LotePadreId != null));

        var faseNorm = fase?.Trim().ToLowerInvariant();
        if (faseNorm == "levante")
            q = q.Where(l => l.Fase == "Levante");
        else if (faseNorm == "produccion")
            q = q.Where(l => l.Fase == "Produccion" && l.LotePadreId == null);

        q = q.OrderBy(l => l.LoteId);
        return await ProjectToDetail(_ctx, q).ToListAsync();
    }

    /// <summary>Lotes en fase Levante (Fase == "Levante") para el módulo Seguimiento Diario de Levante. No mezcla con Producción.</summary>
    public async Task<IEnumerable<LoteDetailDto>> GetLotesLevanteAsync()
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var q = _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.Fase == "Levante");

        // Mismo scoping que GetAllAsync (granjas asignadas + alcance granular)
        var assignedFarmIds = await GetAssignedFarmIdsForCurrentUserAsync();
        if (assignedFarmIds == null || assignedFarmIds.Count == 0)
            return Array.Empty<LoteDetailDto>();
        q = q.Where(l => assignedFarmIds.Contains(l.GranjaId));
        q = await AplicarScopeUbicacionAsync(q);

        q = q.OrderBy(l => l.LoteId);
        return await ProjectToDetail(_ctx, q).ToListAsync();
    }

    public async Task<CommonDtos.PagedResult<LoteDetailDto>> SearchAsync(LoteSearchRequest req)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        // saneo mínimo
        var page = req.Page <= 0 ? 1 : req.Page;
        var pageSize = req.PageSize <= 0 ? 50 : req.PageSize;

        var q = _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId);

        if (req.SoloActivos)
            q = q.Where(l => l.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            q = q.Where(l =>
                (l.LoteId.HasValue && l.LoteId.Value.ToString().Contains(term)) ||
                EF.Functions.Like(l.LoteNombre!.ToLower(), $"%{term}%"));
        }

        if (req.GranjaId.HasValue) q = q.Where(l => l.GranjaId == req.GranjaId.Value);
        if (!string.IsNullOrWhiteSpace(req.NucleoId)) q = q.Where(l => l.NucleoId == req.NucleoId);
        if (!string.IsNullOrWhiteSpace(req.GalponId)) q = q.Where(l => l.GalponId == req.GalponId);

        if (req.FechaDesde.HasValue) q = q.Where(l => l.FechaEncaset >= req.FechaDesde!.Value);
        if (req.FechaHasta.HasValue) q = q.Where(l => l.FechaEncaset <= req.FechaHasta!.Value);

        if (!string.IsNullOrWhiteSpace(req.TipoLinea)) q = q.Where(l => l.TipoLinea == req.TipoLinea);
        if (!string.IsNullOrWhiteSpace(req.Raza)) q = q.Where(l => l.Raza == req.Raza);
        if (!string.IsNullOrWhiteSpace(req.Tecnico)) q = q.Where(l => l.Tecnico == req.Tecnico);

        // Scoping por granjas asignadas + alcance granular (mismo criterio que GetAllAsync)
        var assignedFarmIds = await GetAssignedFarmIdsForCurrentUserAsync();
        if (assignedFarmIds != null)
        {
            if (assignedFarmIds.Count == 0)
                return new CommonDtos.PagedResult<LoteDetailDto>
                {
                    Page = page, PageSize = pageSize, Total = 0, Items = new List<LoteDetailDto>()
                };
            q = q.Where(l => assignedFarmIds.Contains(l.GranjaId));
        }
        q = await AplicarScopeUbicacionAsync(q);

        q = ApplyOrder(q, req.SortBy, req.SortDesc);

        var total = await q.LongCountAsync();
        var items = await ProjectToDetail(_ctx, q)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CommonDtos.PagedResult<LoteDetailDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        };
    }

    public async Task<LoteDetailDto?> GetByIdAsync(int loteId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var q = _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId &&
                        l.LoteId == loteId &&
                        l.DeletedAt == null);

        // Alcance granular: en granjas restringidas solo se puede leer un lote permitido
        // (fail-closed → 404). Sin filtro por granjas asignadas aquí: lo usan flujos internos.
        q = await AplicarScopeUbicacionAsync(q);

        return await ProjectToDetail(_ctx, q).SingleOrDefaultAsync();
    }

    /// <summary>
    /// Resumen de mortalidad + saldos (levante).
    /// Reglas solicitadas:
    ///  - Sumas acumuladas = Σ(mortalidad hembra) y Σ(mortalidad macho) de SeguimientoLoteLevante por LoteId.
    ///  - SaldoHembras = (HembrasL - MortCajaH) - MortalidadAcumHembras
    ///  - SaldoMachos  = (MachosL  - MortCajaM) - MortalidadAcumMachos
    ///  - Clampea a cero si queda negativo.
    ///  - Tenant-safe (CompanyId) y exige que el lote no esté eliminado.
    /// </summary>
    public async Task<LoteMortalidadResumenDto?> GetMortalidadResumenAsync(int loteId)
    {
        // Alcance granular (fix QA M2): acceso directo por loteId respeta el scope (fail-closed → 404)
        if (!await _scopeResolver.PermiteLoteAsync(loteId))
            return null;

        var companyId = await GetEffectiveCompanyIdAsync();
        // 1) Carga del lote (tenant-safe)
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .SingleOrDefaultAsync(l =>
                l.LoteId == loteId &&
                l.CompanyId == companyId &&
                l.DeletedAt == null);

        if (lote is null) return null;

        var loteIdStr = loteId.ToString();

        // 2) Sumas de mortalidad desde tabla unificada seguimiento_diario (tipo levante).
        //    Feature 13 (refinamiento): NO excluimos filas con es_traslado=true porque
        //    una fila puede ser MIXTA: tener tanto datos de traslado (en columnas
        //    dedicadas traslado_ingreso_*/traslado_salida_*) COMO datos manuales
        //    de mortalidad/selección/error de sexaje. Cada concepto va por su
        //    propia columna, así que sumamos las mortalidades de TODAS las filas.
        var mort = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteIdStr)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                H = (int?)g.Sum(x => x.MortalidadHembras ?? 0) ?? 0,
                M = (int?)g.Sum(x => x.MortalidadMachos ?? 0) ?? 0,
                SelH = (int?)g.Sum(x => x.SelH ?? 0) ?? 0,
                SelM = (int?)g.Sum(x => x.SelM ?? 0) ?? 0,
                ErrH = (int?)g.Sum(x => x.ErrorSexajeHembras ?? 0) ?? 0,
                ErrM = (int?)g.Sum(x => x.ErrorSexajeMachos ?? 0) ?? 0
            })
            .SingleOrDefaultAsync();

        int mortH = mort?.H ?? 0;
        int mortM = mort?.M ?? 0;
        int selH = mort?.SelH ?? 0;
        int selM = mort?.SelM ?? 0;
        int errH = mort?.ErrH ?? 0;
        int errM = mort?.ErrM ?? 0;

        // 3) Bases desde historial (lote_etapa_levante) si existe; si no, desde lote
        int baseH;
        int baseM;
        var etapaLevante = await _ctx.LoteEtapaLevante.AsNoTracking()
            .FirstOrDefaultAsync(el => el.LoteId == loteId);
        if (etapaLevante != null)
        {
            baseH = etapaLevante.AvesInicioHembras;
            baseM = etapaLevante.AvesInicioMachos;
        }
        else
        {
            baseH = lote.HembrasL ?? 0;
            baseM = lote.MachosL ?? 0;
        }
        int mortCajaH = lote.MortCajaH ?? 0;
        int mortCajaM = lote.MortCajaM ?? 0;

        // 3.5) Traslados acumulados por fase (Feature 14): Levante + Producción
        var lpl = await _ctx.LotePosturaLevante.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null && l.CompanyId == companyId)
            .Select(l => new
            {
                l.LevanteTrasladoIngresoHembras,
                l.LevanteTrasladoIngresoMachos,
                l.LevanteTrasladoSalidaHembras,
                l.LevanteTrasladoSalidaMachos
            })
            .FirstOrDefaultAsync();
        int levInH  = lpl?.LevanteTrasladoIngresoHembras ?? 0;
        int levInM  = lpl?.LevanteTrasladoIngresoMachos  ?? 0;
        int levOutH = lpl?.LevanteTrasladoSalidaHembras  ?? 0;
        int levOutM = lpl?.LevanteTrasladoSalidaMachos   ?? 0;

        var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null && l.CompanyId == companyId)
            .Select(l => new
            {
                l.ProduccionTrasladoIngresoHembras,
                l.ProduccionTrasladoIngresoMachos,
                l.ProduccionTrasladoSalidaHembras,
                l.ProduccionTrasladoSalidaMachos
            })
            .FirstOrDefaultAsync();
        int prodInH  = lpp?.ProduccionTrasladoIngresoHembras ?? 0;
        int prodInM  = lpp?.ProduccionTrasladoIngresoMachos  ?? 0;
        int prodOutH = lpp?.ProduccionTrasladoSalidaHembras  ?? 0;
        int prodOutM = lpp?.ProduccionTrasladoSalidaMachos   ?? 0;

        int totInH  = levInH  + prodInH;
        int totInM  = levInM  + prodInM;
        int totOutH = levOutH + prodOutH;
        int totOutM = levOutM + prodOutM;

        // 4) Saldos = base - bajas + ingresos_traslado (ambas fases) - salidas_traslado (ambas fases)
        int saldoH = Math.Max(0, baseH - mortCajaH - mortH - selH - errH + totInH - totOutH);
        int saldoM = Math.Max(0, baseM - mortCajaM - mortM - selM - errM + totInM - totOutM);

        return new LoteMortalidadResumenDto
        {
            LoteId = loteId.ToString(),
            HembrasIniciales = baseH,
            MachosIniciales = baseM,
            MortCajaHembras = mortCajaH,
            MortCajaMachos = mortCajaM,
            MortalidadAcumHembras = mortH,
            MortalidadAcumMachos = mortM,
            SelAcumHembras = selH,
            SelAcumMachos = selM,
            ErrorSexajeAcumHembras = errH,
            ErrorSexajeAcumMachos = errM,
            SaldoHembras = saldoH,
            SaldoMachos = saldoM,
            LevanteTrasladoIngresoHembras = levInH,
            LevanteTrasladoIngresoMachos  = levInM,
            LevanteTrasladoSalidaHembras  = levOutH,
            LevanteTrasladoSalidaMachos   = levOutM,
            ProduccionTrasladoIngresoHembras = prodInH,
            ProduccionTrasladoIngresoMachos  = prodInM,
            ProduccionTrasladoSalidaHembras  = prodOutH,
            ProduccionTrasladoSalidaMachos   = prodOutM
        };
    }
}
