// Reporte Técnico Semanal — concern PRODUCCIÓN (semana 25+).
// Fuente real: fn_indicadores_produccion_postura (canónica; misma aritmética
// que la pantalla de indicadores). Las columnas que la fn no emite (aprov,
// masa huevo, conversión gr/HI, apareo, nacimientos) salen de la guía cruda
// y de ReporteTecnicoSemanalCalculos (puro).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoSemanalService
{
    public async Task<ReporteTecnicoSemanalProduccionResponse> GenerarProduccionAsync(
        ReporteTecnicoSemanalRequest request, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync();
        var loteBase = await ResolverLoteBaseAsync(request.LotePosturaBaseId, companyId, ct);

        // lote_ids del base (incluye hijos legacy fase=Produccion por lote_padre_id).
        var lotesDelBase = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePosturaBaseId == request.LotePosturaBaseId
                        && l.CompanyId == companyId
                        && l.DeletedAt == null
                        && l.LoteId != null)
            .Select(l => l.LoteId!.Value)
            .ToListAsync(ct);

        var hijosProduccion = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId != null
                        && lotesDelBase.Contains(l.LotePadreId.Value)
                        && l.CompanyId == companyId
                        && l.DeletedAt == null
                        && l.LoteId != null)
            .Select(l => l.LoteId!.Value)
            .ToListAsync(ct);

        var loteIds = lotesDelBase.Concat(hijosProduccion).ToHashSet();

        var lpps = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId
                        && p.DeletedAt == null
                        && p.LoteId != null
                        && loteIds.Contains(p.LoteId.Value))
            .OrderBy(p => p.LoteNombre)
            .ToListAsync(ct);

        // Alcance granular: recorte fail-closed de lotes no visibles.
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count > 0)
        {
            lpps = lpps
                .Where(p => UserLocationScopeCalculos.LotePermitido(restringidos, p.GranjaId, p.LoteId))
                .ToList();
        }

        // Guía por (raza, año) — columnas que la fn base no expone.
        var guiaPorCombo = new Dictionary<(string Raza, int Anio), Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>>();
        foreach (var combo in lpps
                     .Where(p => !string.IsNullOrWhiteSpace(p.Raza) && p.AnoTablaGenetica.HasValue)
                     .Select(p => (Raza: p.Raza!.Trim().ToLower(), Anio: p.AnoTablaGenetica!.Value))
                     .Distinct())
        {
            var cruda = await CargarGuiaPorSemanaAsync(companyId, combo.Raza, combo.Anio, ct);
            guiaPorCombo[combo] = cruda
                .Where(kv => kv.Key >= 25)
                .ToDictionary(kv => kv.Key, kv => new ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion(
                    AprovSem: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.AprovSem),
                    AprovAc: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.AprovAc),
                    MasaHuevo: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.MasaHuevo),
                    GrHuevoInc: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.GrHuevoInc),
                    Apareo: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.Apareo),
                    NacimPorcentaje: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.NacimPorcentaje),
                    PollitoAa: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.PollitoAa),
                    MortSemHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.MortSemH),
                    MortSemMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.MortSemM)));
        }

        var vacio = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>();
        var tabs = new List<ReporteSemanalProduccionTabDto>(lpps.Count);

        // Fecha de referencia de cada lote (misma prioridad que la fn: encaset del levante
        // ligado → encaset del LPP → inicio de producción) para ubicar los traslados por semana.
        var levanteIds = lpps.Where(p => p.LotePosturaLevanteId.HasValue)
                             .Select(p => (int?)p.LotePosturaLevanteId!.Value).Distinct().ToList();
        var encasetLevante = (await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Where(l => levanteIds.Contains(l.LotePosturaLevanteId) && l.DeletedAt == null && l.FechaEncaset != null)
            .Select(l => new { l.LotePosturaLevanteId, l.FechaEncaset })
            .ToListAsync(ct))
            .Where(l => l.LotePosturaLevanteId.HasValue)
            .ToDictionary(l => l.LotePosturaLevanteId!.Value, l => l.FechaEncaset!.Value);

        foreach (var lpp in lpps)
        {
            var filas = await _ctx.Database
                .SqlQueryRaw<IndicadorProduccionSemanalBdRow>(
                    "SELECT * FROM public.fn_indicadores_produccion_postura(" +
                    "{0}::int, {1}::int, NULL::int, NULL::int, NULL::int, NULL::date, NULL::date)",
                    companyId, lpp.LotePosturaProduccionId!.Value)
                .ToListAsync(ct);

            var cargadosPorSemana = await CargarHuevosEnviadosPorSemanaAsync(lpp, encasetLevante, ct);
            var ventasPorSemana = await CargarVentasAvesPorSemanaAsync(lpp, encasetLevante, ct);

            var guia = (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue
                        && guiaPorCombo.TryGetValue((lpp.Raza!.Trim().ToLower(), lpp.AnoTablaGenetica.Value), out var g))
                ? g
                : vacio;

            var header = new ReporteSemanalTabHeaderDto
            {
                LoteId = lpp.LoteId,
                LotePosturaProduccionId = lpp.LotePosturaProduccionId,
                LoteNombre = lpp.LoteNombre,
                GranjaId = lpp.GranjaId,
                NucleoId = lpp.NucleoId,
                GalponId = lpp.GalponId,
                Tecnico = lpp.Tecnico,
                Raza = lpp.Raza,
                AnioGuia = lpp.AnoTablaGenetica,
                FechaEncaset = lpp.FechaEncaset,
                FechaInicioProduccion = lpp.FechaInicioProduccion,
                BaseHembras = lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? 0,
                BaseMachos = lpp.AvesMInicial ?? lpp.MachosInicialesProd ?? 0,
                PesoInicialHembras = lpp.PesoInicialH,
                MortCajasHembras = lpp.MortCajaH,
                MortCajasMachos = lpp.MortCajaM
            };

            tabs.Add(new ReporteSemanalProduccionTabDto
            {
                Header = header,
                Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                    filas, guia, cargadosPorSemana, ventasPorSemana)
            });
        }

        var nombres = await CargarNombresUbicacionAsync(
            tabs.Where(t => t.Header.GranjaId.HasValue).Select(t => t.Header.GranjaId!.Value).Distinct().ToList(),
            tabs.Where(t => !string.IsNullOrWhiteSpace(t.Header.NucleoId)).Select(t => t.Header.NucleoId!).Distinct().ToList(),
            tabs.Where(t => !string.IsNullOrWhiteSpace(t.Header.GalponId)).Select(t => t.Header.GalponId!).Distinct().ToList(),
            ct);
        foreach (var tab in tabs) CompletarNombres(tab.Header, nombres);

        var consolidado = tabs.Count > 1 ? ReporteTecnicoSemanalCalculos.ConsolidarProduccion(tabs) : null;

        if (request.SemanaDesde.HasValue || request.SemanaHasta.HasValue)
        {
            foreach (var tab in tabs)
                tab.Semanas = FiltrarRango(tab.Semanas, request.SemanaDesde, request.SemanaHasta, s => s.Semana);
            if (consolidado is not null)
                consolidado.Semanas = FiltrarRango(consolidado.Semanas, request.SemanaDesde, request.SemanaHasta, s => s.Semana);
        }

        return new ReporteTecnicoSemanalProduccionResponse(
            LotePosturaBaseId: loteBase.LotePosturaBaseId,
            LoteBaseNombre: loteBase.LoteNombre,
            Raza: loteBase.Raza ?? lpps.FirstOrDefault()?.Raza,
            AnioGuia: lpps.FirstOrDefault()?.AnoTablaGenetica,
            TieneGuia: guiaPorCombo.Values.Any(d => d.Count > 0),
            Tabs: tabs,
            Consolidado: consolidado);
    }

    /// <summary>
    /// "HI Cargado" del Excel: huevos incubables ENVIADOS a planta/incubadora por semana de vida.
    /// Fuente real = traslado_huevos (Completado + destino Planta), incubables = limpio + tratado
    /// (misma convención que TrasladoHuevosService y el reporte técnico existente).
    /// Devuelve vacío si el lote no tiene fecha de referencia (sin ella no hay semana que asignar).
    /// </summary>
    private async Task<Dictionary<int, int>> CargarHuevosEnviadosPorSemanaAsync(
        Domain.Entities.LotePosturaProduccion lpp,
        IReadOnlyDictionary<int, DateTime> encasetLevante,
        CancellationToken ct)
    {
        DateTime? fechaRef = null;
        if (lpp.LotePosturaLevanteId.HasValue
            && encasetLevante.TryGetValue(lpp.LotePosturaLevanteId.Value, out var encLev))
            fechaRef = encLev;
        fechaRef ??= lpp.FechaEncaset ?? lpp.FechaInicioProduccion;

        if (!fechaRef.HasValue) return new Dictionary<int, int>();

        var traslados = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LotePosturaProduccionId == lpp.LotePosturaProduccionId
                        && t.DeletedAt == null
                        && t.Estado == "Completado"
                        && t.TipoDestino == "Planta")
            .Select(t => new { t.FechaTraslado, Incubables = t.CantidadLimpio + t.CantidadTratado })
            .ToListAsync(ct);

        return ReporteTecnicoSemanalCalculos.AgruparCargadosPorSemana(
            traslados.Select(t => (t.FechaTraslado, t.Incubables)), fechaRef.Value);
    }

    /// <summary>
    /// Columnas «VentaH / VentaM» del archivo: salidas de aves registradas en el
    /// módulo de Movimientos de Aves con tipo «Venta».
    ///
    /// El tipo NO es una constante del código: sale de la lista maestra
    /// `movimiento_de_aves_tipo_movimiento` (hoy «Traslado» y «Venta»), así que
    /// se compara por CONTENIDO en minúsculas — el mismo criterio que usa el
    /// front (`esTipoVenta`) — y no por igualdad exacta, que se rompería con
    /// «Venta de aves» o un cambio de mayúsculas en la lista.
    ///
    /// Solo cuentan las COMPLETADAS: una venta pendiente o cancelada no sacó
    /// aves. Se toma el lote de ORIGEN, porque una venta es una salida.
    /// </summary>
    private async Task<Dictionary<int, (int Hembras, int Machos)>> CargarVentasAvesPorSemanaAsync(
        Domain.Entities.LotePosturaProduccion lpp,
        IReadOnlyDictionary<int, DateTime> encasetLevante,
        CancellationToken ct)
    {
        var vacio = new Dictionary<int, (int Hembras, int Machos)>();
        if (!lpp.LoteId.HasValue) return vacio;

        DateTime? fechaRef = null;
        if (lpp.LotePosturaLevanteId.HasValue
            && encasetLevante.TryGetValue(lpp.LotePosturaLevanteId.Value, out var encLev))
            fechaRef = encLev;
        fechaRef ??= lpp.FechaEncaset ?? lpp.FechaInicioProduccion;

        if (!fechaRef.HasValue) return vacio;

        var ventas = await _ctx.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == lpp.LoteId
                        && m.DeletedAt == null
                        && m.Estado == "Completado"
                        && m.TipoMovimiento != null
                        && m.TipoMovimiento.ToLower().Contains("venta"))
            .Select(m => new { m.FechaMovimiento, m.CantidadHembras, m.CantidadMachos })
            .ToListAsync(ct);

        return ReporteTecnicoSemanalCalculos.AgruparVentasPorSemana(
            ventas.Select(v => (v.FechaMovimiento, v.CantidadHembras, v.CantidadMachos)), fechaRef.Value);
    }
}
