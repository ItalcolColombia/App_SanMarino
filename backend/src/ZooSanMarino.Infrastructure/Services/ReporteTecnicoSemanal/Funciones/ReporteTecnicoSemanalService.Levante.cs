// Reporte Técnico Semanal — concern LEVANTE (semanas 1-25).
// Fuente real: fn_reporte_semanal_levante_extras (hermana de
// fn_indicadores_levante_postura: misma semana/guards/saldo por sexo).
// Guía: guia_genetica_sanmarino_colombia leída una vez por (raza, año).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoSemanalService
{
    public async Task<ReporteTecnicoSemanalLevanteResponse> GenerarLevanteAsync(
        ReporteTecnicoSemanalRequest request, CancellationToken ct = default)
    {
        var companyId = await ResolveCompanyIdAsync();
        var loteBase = await ResolverLoteBaseAsync(request.LotePosturaBaseId, companyId, ct);

        // Sublotes de levante del lote base (un lote por galpón).
        // El descarte va por FaseLoteCalculos: se excluye el lote HIJO de producción, no todo lo
        // que tenga fase "Produccion" — esa fase también la traen los lotes cargados con historia
        // (encaset de hace más de 26 semanas), cuya data es de levante.
        var sublotes = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePosturaBaseId == request.LotePosturaBaseId
                        && l.CompanyId == companyId
                        && l.DeletedAt == null
                        && l.LoteId != null)
            .Where(FaseLoteCalculos.LoteEsRegistroLevante)
            .OrderBy(l => l.LoteNombre)
            .ToListAsync(ct);

        // Alcance granular: recorte fail-closed de lotes no visibles.
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count > 0)
        {
            sublotes = sublotes
                .Where(l => UserLocationScopeCalculos.LotePermitido(restringidos, l.GranjaId, l.LoteId))
                .ToList();
        }

        // Guía por (raza, año) — normalmente una sola combinación por lote base.
        var guiaPorCombo = new Dictionary<(string Raza, int Anio), Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>>();
        foreach (var combo in sublotes
                     .Where(l => !string.IsNullOrWhiteSpace(l.Raza) && l.AnoTablaGenetica.HasValue)
                     .Select(l => (Raza: l.Raza!.Trim().ToLower(), Anio: l.AnoTablaGenetica!.Value))
                     .Distinct())
        {
            // Desempate de la semana 25: levante ⇒ la fila '25', no la '25P' de arranque de producción.
            var cruda = await CargarGuiaPorSemanaAsync(
                companyId, combo.Raza, combo.Anio, preferirVarianteProduccion: false, ct);
            guiaPorCombo[combo] = cruda
                .Where(kv => kv.Key is >= 1 and <= 25)
                .ToDictionary(kv => kv.Key, kv => new ReporteTecnicoSemanalCalculos.GuiaSemanaLevante(
                    MortSemHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.MortSemH),
                    MortSemMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.MortSemM),
                    RetiroAcHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.RetiroAcH),
                    RetiroAcMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.RetiroAcM),
                    GrAveDiaHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.GrAveDiaH),
                    GrAveDiaMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.GrAveDiaM),
                    ConsAcHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ConsAcH),
                    ConsAcMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ConsAcM),
                    PesoHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.PesoH),
                    PesoMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.PesoM),
                    Uniformidad: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.Uniformidad),
                    // ── Hoja «ALIMLev» ──
                    AlimentoHembras: string.IsNullOrWhiteSpace(kv.Value.AlimH) ? null : kv.Value.AlimH!.Trim(),
                    AlimentoMachos: string.IsNullOrWhiteSpace(kv.Value.AlimM) ? null : kv.Value.AlimM!.Trim(),
                    KcalSemHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.KcalSemH),
                    ProtSemHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ProtHSem),
                    KcalSemMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.KcalSemM),
                    ProtSemMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ProtSemM),
                    KcalAlimentoMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.KcalM),
                    ProtAlimentoMachos: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ProtM),
                    KcalAlimentoHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.KcalH),
                    ProtAlimentoHembras: ReporteTecnicoSemanalCalculos.ParseGuia(kv.Value.ProtH)));
        }

        var vacio = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>();
        var tabs = new List<ReporteSemanalLevanteTabDto>(sublotes.Count);

        foreach (var lote in sublotes)
        {
            var filas = await _ctx.Database
                .SqlQueryRaw<ReporteSemanalLevanteExtrasRow>(
                    "SELECT * FROM public.fn_reporte_semanal_levante_extras({0}::int)", lote.LoteId!.Value)
                .ToListAsync(ct);

            var guia = (!string.IsNullOrWhiteSpace(lote.Raza) && lote.AnoTablaGenetica.HasValue
                        && guiaPorCombo.TryGetValue((lote.Raza!.Trim().ToLower(), lote.AnoTablaGenetica.Value), out var g))
                ? g
                : vacio;

            var header = new ReporteSemanalTabHeaderDto
            {
                LoteId = lote.LoteId,
                LoteNombre = lote.LoteNombre,
                GranjaId = lote.GranjaId,
                NucleoId = lote.NucleoId,
                GalponId = lote.GalponId,
                Tecnico = lote.Tecnico,
                Raza = lote.Raza,
                AnioGuia = lote.AnoTablaGenetica,
                FechaEncaset = lote.FechaEncaset,
                BaseHembras = filas.Count > 0 ? filas[0].BaseHembras : (lote.HembrasL ?? 0),
                BaseMachos = filas.Count > 0 ? filas[0].BaseMachos : (lote.MachosL ?? 0),
                PesoInicialHembras = lote.PesoInicialH,
                MortCajasHembras = lote.MortCajaH,
                MortCajasMachos = lote.MortCajaM
            };

            var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(filas, guia);
            tabs.Add(new ReporteSemanalLevanteTabDto
            {
                Header = header,
                Semanas = semanas,
                AlimentoPorFase = AlimentoPorFaseCalculos.Construir(semanas)
            });
        }

        var nombres = await CargarNombresUbicacionAsync(
            tabs.Where(t => t.Header.GranjaId.HasValue).Select(t => t.Header.GranjaId!.Value).Distinct().ToList(),
            tabs.Where(t => !string.IsNullOrWhiteSpace(t.Header.NucleoId)).Select(t => t.Header.NucleoId!).Distinct().ToList(),
            tabs.Where(t => !string.IsNullOrWhiteSpace(t.Header.GalponId)).Select(t => t.Header.GalponId!).Distinct().ToList(),
            ct);
        foreach (var tab in tabs) CompletarNombres(tab.Header, nombres);

        var consolidado = tabs.Count > 1 ? ReporteTecnicoSemanalCalculos.ConsolidarLevante(tabs) : null;

        // Rango de semanas: se recorta al final (los acumulados ya están completos).
        if (request.SemanaDesde.HasValue || request.SemanaHasta.HasValue)
        {
            foreach (var tab in tabs)
                tab.Semanas = FiltrarRango(tab.Semanas, request.SemanaDesde, request.SemanaHasta, s => s.Semana);
            if (consolidado is not null)
                consolidado.Semanas = FiltrarRango(consolidado.Semanas, request.SemanaDesde, request.SemanaHasta, s => s.Semana);
        }

        return new ReporteTecnicoSemanalLevanteResponse(
            LotePosturaBaseId: loteBase.LotePosturaBaseId,
            LoteBaseNombre: loteBase.LoteNombre,
            Raza: loteBase.Raza ?? sublotes.FirstOrDefault()?.Raza,
            AnioGuia: sublotes.FirstOrDefault()?.AnoTablaGenetica,
            TieneGuia: guiaPorCombo.Values.Any(d => d.Count > 0),
            Tabs: tabs,
            Consolidado: consolidado);
    }
}
