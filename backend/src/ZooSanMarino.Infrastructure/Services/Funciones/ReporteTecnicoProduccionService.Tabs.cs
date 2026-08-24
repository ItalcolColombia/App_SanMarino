// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Tabs.cs
// Reporte de PRODUCCION con pestanas por galpon (Fase 4), leyendo desde produccion_diaria.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
    public async Task<ReporteTecnicoProduccionCompletoDto> ObtenerReporteProduccionAsync(
        ObtenerReporteProduccionRequestDto request,
        CancellationToken ct = default)
    {
        var lppQuery = _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Where(l => l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);

        List<LotePosturaProduccion> lotesProduccion;

        if (request.LotePosturaProduccionId.HasValue)
        {
            var lpp = await lppQuery
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == request.LotePosturaProduccionId.Value, ct)
                ?? throw new InvalidOperationException(
                    $"Lote producción {request.LotePosturaProduccionId} no encontrado");
            lotesProduccion = [lpp];
        }
        else
        {
            // Cadena: lote_postura_base → lote (LotePosturaBaseId) → lote_postura_levante (LoteId) → lote_postura_produccion (LotePosturaLevanteId)
            var lplIds = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(lpl => lpl.LoteId.HasValue &&
                              _ctx.Lotes.Any(l => l.LoteId == lpl.LoteId &&
                                                  l.LotePosturaBaseId == request.LotePosturaBaseId))
                .Select(lpl => lpl.LotePosturaLevanteId)
                .ToListAsync(ct);

            if (!lplIds.Any())
                throw new InvalidOperationException(
                    $"No se encontraron lotes de levante para la base {request.LotePosturaBaseId}");

            lotesProduccion = await lppQuery
                .Where(l => l.LotePosturaLevanteId.HasValue && lplIds.Contains(l.LotePosturaLevanteId.Value))
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            if (!lotesProduccion.Any())
                throw new InvalidOperationException(
                    $"No hay lotes de producción para la base {request.LotePosturaBaseId}");
        }

        var fechaInicioGlobal = lotesProduccion
            .Select(l => l.FechaInicioProduccion ?? l.FechaEncaset ?? DateTime.Today)
            .Min();

        var todosDiarios = new List<ReporteTecnicoProduccionDiarioDto>();
        foreach (var lpp in lotesProduccion)
        {
            var lppId        = lpp.LotePosturaProduccionId ?? 0;
            var fechaInicioL = lpp.FechaInicioProduccion ?? lpp.FechaEncaset ?? DateTime.Today;
            var hembras      = lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? lpp.HembrasL ?? 0;
            var machos       = lpp.AvesMInicial ?? lpp.MachosInicialesProd ?? lpp.MachosL ?? 0;

            var diarios = await ObtenerDatosDiariosPorLPPAsync(
                lppId, lpp.LoteId, fechaInicioL,
                request.FechaInicio, request.FechaFin,
                hembras, machos, ct,
                usarProduccionDiaria: true);

            todosDiarios.AddRange(diarios);
        }

        var datosDiarios = lotesProduccion.Count > 1
            ? ConsolidarDatosDiarios(todosDiarios)
            : todosDiarios;

        var datosSemanales = request.FiltroPeriodicidad == "Semanal"
            ? ConsolidarSemanales(datosDiarios, fechaInicioGlobal)
            : new List<ReporteTecnicoProduccionSemanalDto>();

        return new ReporteTecnicoProduccionCompletoDto(
            MapearInformacionLoteFromLPP(lotesProduccion.First()),
            datosDiarios,
            datosSemanales);
    }

    private sealed class SegProdTab
    {
        public DateTime Fecha        { get; set; }
        public int  MortH            { get; set; }
        public int  MortM            { get; set; }
        public int  SelH             { get; set; }
        public int  SelM             { get; set; }
        public double ConsKgH        { get; set; }
        public double ConsKgM        { get; set; }
        public int  HuevoTot         { get; set; }
        public int  HuevoInc         { get; set; }
        public double PesoHuevo      { get; set; }
        public double? PesoH         { get; set; }
        public double? PesoM         { get; set; }
        public double? Uniformidad   { get; set; }
        public string? Observaciones { get; set; }
    }

    private async Task<List<SegProdTab>> ObtenerSegsProdTabsAsync(
        int lppId, DateTime? fechaInicio, DateTime? fechaFin, CancellationToken ct)
    {
        var query = _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lppId);

        if (fechaInicio.HasValue) query = query.Where(s => s.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)   query = query.Where(s => s.Fecha <= fechaFin.Value);

        return await query
            .OrderBy(s => s.Fecha)
            .Select(s => new SegProdTab
            {
                Fecha        = s.Fecha,
                MortH        = s.MortalidadH,
                MortM        = s.MortalidadM,
                SelH         = s.SelH,
                SelM         = s.SelM,
                ConsKgH      = (double)s.ConsKgH,
                ConsKgM      = (double)s.ConsKgM,
                HuevoTot     = s.HuevoTot,
                HuevoInc     = s.HuevoInc,
                PesoHuevo    = (double)(s.PesoHuevo ?? 0),
                PesoH        = s.PesoH     != null ? (double?)((double)s.PesoH.Value)        : null,
                PesoM        = s.PesoM     != null ? (double?)((double)s.PesoM.Value)        : null,
                Uniformidad  = s.Uniformidad != null ? (double?)((double)s.Uniformidad.Value) : null,
                Observaciones = s.Observaciones
            })
            .ToListAsync(ct);
    }

    private static (double? ProdPorc, double? PesoHuevo, double? HtotalAa, double? Uniformidad)?
        ObtenerGuiaParaSemana(List<Domain.Entities.ProduccionAvicolaRaw> guias, int semana)
    {
        if (guias.Count == 0) return null;

        static double? TryParse(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var clean = val.Trim().Replace(",", ".");
            return double.TryParse(clean,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var d) ? d : null;
        }

        static int? TryParseEdad(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var s = val.Trim().Replace(",", ".");
            if (int.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var n)) return n;
            var m = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out var n2) ? n2 : null;
        }

        var guia = guias.FirstOrDefault(g => TryParseEdad(g.Edad) == semana);
        if (guia == null) return null;

        return (TryParse(guia.ProdPorcentaje), TryParse(guia.PesoHuevo),
                TryParse(guia.HTotalAa),       TryParse(guia.Uniformidad));
    }

    public async Task<ReporteTecnicoProduccionTabsDto> ObtenerReporteProduccionTabsAsync(
        ObtenerReporteProduccionRequestDto request,
        CancellationToken ct = default)
    {
        // ── 1. Resolver LPPs (idéntico a ObtenerReporteProduccionAsync) ────────
        var lppQuery = _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l => l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);

        List<LotePosturaProduccion> lotesProduccion;

        if (request.LotePosturaProduccionId.HasValue)
        {
            var lpp = await lppQuery
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == request.LotePosturaProduccionId.Value, ct)
                ?? throw new InvalidOperationException(
                    $"Lote producción {request.LotePosturaProduccionId} no encontrado");
            lotesProduccion = [lpp];
        }
        else
        {
            var lplIds = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(lpl => lpl.LoteId.HasValue &&
                              _ctx.Lotes.Any(l => l.LoteId == lpl.LoteId &&
                                                  l.LotePosturaBaseId == request.LotePosturaBaseId))
                .Select(lpl => lpl.LotePosturaLevanteId)
                .ToListAsync(ct);

            if (!lplIds.Any())
                throw new InvalidOperationException(
                    $"No se encontraron lotes de levante para la base {request.LotePosturaBaseId}");

            lotesProduccion = await lppQuery
                .Where(l => l.LotePosturaLevanteId.HasValue && lplIds.Contains(l.LotePosturaLevanteId.Value))
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            if (!lotesProduccion.Any())
                throw new InvalidOperationException(
                    $"No hay lotes de producción para la base {request.LotePosturaBaseId}");
        }

        // ── 2. Cargar GUIA genética (primer LPP con Raza + AnoTablaGenetica) ──
        var guiasCompletas = new List<Domain.Entities.ProduccionAvicolaRaw>();
        var lppConRaza = lotesProduccion.FirstOrDefault(l =>
            !string.IsNullOrWhiteSpace(l.Raza) && l.AnoTablaGenetica.HasValue);

        if (lppConRaza != null)
        {
            var razaNorm = lppConRaza.Raza!.Trim().ToLower();
            var ano      = lppConRaza.AnoTablaGenetica!.Value.ToString();
            // Santa Reyes tiene su guia en tabla propia (F2.2). Se pregunta primero; si la empresa
            // no tiene guia propia la lista vuelve vacia y corre la consulta de siempre.
            guiasCompletas = await GuiaGeneticaLookup.ObtenerFilasPropiasAsync(
                _ctx, _currentUser.CompanyId, razaNorm, ano, ct);

            if (guiasCompletas.Count == 0)
            {
                guiasCompletas = await _ctx.ProduccionAvicolaRaw
                    .AsNoTracking()
                    .Where(p => p.CompanyId == _currentUser.CompanyId &&
                                p.Raza != null && p.AnioGuia != null &&
                                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                                p.AnioGuia.Trim() == ano)
                    .ToListAsync(ct);
            }
        }

        // ── 3. Construir datos por galpón ──────────────────────────────────────
        var diariosGalpon   = new List<ReporteDiarioGalponDto>();
        var semanalesGalpon = new List<ReporteSemanalGalponDto>();

        foreach (var lpp in lotesProduccion)
        {
            var lppId        = lpp.LotePosturaProduccionId ?? 0;
            var galponId     = lpp.GalponId ?? "";
            var galponNombre = lpp.Galpon?.GalponNombre ?? lpp.GalponId ?? "Galpón";
            var hembrasIni   = (int)(lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? lpp.HembrasL ?? 0);
            var machosIni    = (int)(lpp.AvesMInicial ?? lpp.MachosInicialesProd  ?? lpp.MachosL  ?? 0);
            var fechaInicioProd = lpp.FechaInicioProduccion ?? lpp.FechaEncaset ?? DateTime.Today;

            var segs = await ObtenerSegsProdTabsAsync(lppId, request.FechaInicio, request.FechaFin, ct);
            if (segs.Count == 0) continue;

            // Acumuladores para saldo y HTAA
            int cumMortH = 0, cumMortM = 0, cumSelH = 0, cumSelM = 0, cumHuevos = 0;

            var diasLpp = new List<ReporteDiarioGalponDto>(segs.Count);

            foreach (var s in segs)
            {
                cumMortH  += s.MortH;
                cumMortM  += s.MortM;
                cumSelH   += s.SelH;
                cumSelM   += s.SelM;
                cumHuevos += s.HuevoTot;

                var saldoH   = Math.Max(0, hembrasIni - cumMortH - cumSelH);
                var saldoM   = Math.Max(0, machosIni  - cumMortM - cumSelM);
                var edadDias = (int)(s.Fecha - fechaInicioProd).TotalDays;
                var semana   = (int)Math.Ceiling((edadDias + 1.0) / 7);
                var htaa     = saldoH > 0 ? (double?)((double)cumHuevos / saldoH) : null;

                var porcPost = saldoH > 0 ? (double)s.HuevoTot / saldoH * 100d : 0d;
                var porcInc  = s.HuevoTot > 0 ? (double)s.HuevoInc / s.HuevoTot * 100d : 0d;
                var porcMort = hembrasIni > 0 ? (double)s.MortH / hembrasIni * 100d : 0d;

                var guia = ObtenerGuiaParaSemana(guiasCompletas, semana);

                diasLpp.Add(new ReporteDiarioGalponDto(
                    LotePosturaProduccionId: lppId,
                    GalponId:               galponId,
                    GalponNombre:           galponNombre,
                    LoteNombre:             lpp.LoteNombre,
                    Fecha:                  s.Fecha,
                    SemanaRelativa:         semana,
                    EdadDias:               edadDias,
                    SaldoHembras:           saldoH,
                    SaldoMachos:            saldoM,
                    MortalidadHembras:      s.MortH,
                    MortalidadMachos:       s.MortM,
                    PorcMortalidad:         porcMort,
                    ConsKgH:                s.ConsKgH,
                    ConsKgM:                s.ConsKgM,
                    HuevoTot:               s.HuevoTot,
                    HuevoInc:               s.HuevoInc,
                    PorcentajePostura:      porcPost,
                    PorcentajeIncubables:   porcInc,
                    PesoHuevo:              s.PesoHuevo,
                    PesoH:                  s.PesoH,
                    PesoM:                  s.PesoM,
                    Uniformidad:            s.Uniformidad,
                    Htaa:                   htaa,
                    PorcentajePosturaGuia:  guia?.ProdPorc,
                    PesoHuevoGuia:          guia?.PesoHuevo,
                    HtaaGuia:               guia?.HtotalAa,
                    UniformidadGuia:        guia?.Uniformidad,
                    DifPostura:  guia?.ProdPorc  != null ? porcPost   - guia.Value.ProdPorc.Value  : null,
                    DifPesoHuevo: guia?.PesoHuevo != null ? s.PesoHuevo - guia.Value.PesoHuevo.Value : null,
                    Observaciones: s.Observaciones
                ));
            }

            diariosGalpon.AddRange(diasLpp);

            // ── Agregar semanales por galpón ──────────────────────────────────
            foreach (var sg in diasLpp.GroupBy(d => d.SemanaRelativa).OrderBy(g => g.Key))
            {
                var rows  = sg.ToList();
                var guia  = ObtenerGuiaParaSemana(guiasCompletas, sg.Key);
                var porcPos = rows.Count(r => r.SaldoHembras > 0) > 0
                    ? rows.Where(r => r.SaldoHembras > 0).Average(r => r.PorcentajePostura)
                    : 0d;
                var htotSum   = rows.Sum(r => r.HuevoTot);
                var porcIncSem = htotSum > 0
                    ? (double)rows.Sum(r => r.HuevoInc) / htotSum * 100d
                    : 0d;
                var pesoHuevoSem = rows.Where(r => r.PesoHuevo > 0)
                                       .Select(r => r.PesoHuevo)
                                       .DefaultIfEmpty(0d).Average();

                semanalesGalpon.Add(new ReporteSemanalGalponDto(
                    LotePosturaProduccionId: lppId,
                    GalponId:               galponId,
                    GalponNombre:           galponNombre,
                    LoteNombre:             lpp.LoteNombre,
                    Semana:                 sg.Key,
                    FechaInicioSemana:      rows.First().Fecha,
                    FechaFinSemana:         rows.Last().Fecha,
                    EdadSemanas:            sg.Key,
                    SaldoInicioHembras:     rows.First().SaldoHembras,
                    SaldoInicioMachos:      rows.First().SaldoMachos,
                    SaldoFinHembras:        rows.Last().SaldoHembras,
                    SaldoFinMachos:         rows.Last().SaldoMachos,
                    MortalidadHembrasSemanal: rows.Sum(r => r.MortalidadHembras),
                    MortalidadMachosSemanal:  rows.Sum(r => r.MortalidadMachos),
                    PorcMortalidadSemanal:  hembrasIni > 0
                        ? (double)rows.Sum(r => r.MortalidadHembras) / hembrasIni * 100d : 0d,
                    ConsKgHSemanal:         rows.Sum(r => r.ConsKgH),
                    ConsKgMSemanal:         rows.Sum(r => r.ConsKgM),
                    HuevoTotSemanal:        htotSum,
                    HuevoIncSemanal:        rows.Sum(r => r.HuevoInc),
                    PorcentajePosturaPromedio:   porcPos,
                    PorcentajeIncubablesPromedio: porcIncSem,
                    PesoHuevoPromedio:      pesoHuevoSem,
                    PesoHPromedio:          rows.Any(r => r.PesoH.HasValue)
                        ? rows.Where(r => r.PesoH.HasValue).Average(r => r.PesoH!.Value) : null,
                    PesoMPromedio:          rows.Any(r => r.PesoM.HasValue)
                        ? rows.Where(r => r.PesoM.HasValue).Average(r => r.PesoM!.Value) : null,
                    UniformidadPromedio:    rows.Any(r => r.Uniformidad.HasValue)
                        ? rows.Where(r => r.Uniformidad.HasValue).Average(r => r.Uniformidad!.Value) : null,
                    HtaaSemanal:            rows.Last().Htaa,
                    PorcentajePosturaGuia:  guia?.ProdPorc,
                    PesoHuevoGuia:          guia?.PesoHuevo,
                    HtaaGuia:               guia?.HtotalAa,
                    UniformidadGuia:        guia?.Uniformidad,
                    DifPostura:   guia?.ProdPorc  != null ? porcPos      - guia.Value.ProdPorc.Value   : null,
                    DifPesoHuevo: guia?.PesoHuevo != null ? pesoHuevoSem - guia.Value.PesoHuevo.Value  : null
                ));
            }
        }

        // ── 4. DiariosGeneral — consolidar por fecha ───────────────────────────
        var diariosGeneral = diariosGalpon
            .GroupBy(d => d.Fecha.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var rows    = g.ToList();
                var semR    = rows[0].SemanaRelativa;
                var guia    = ObtenerGuiaParaSemana(guiasCompletas, semR);
                var saldoH  = rows.Sum(r => r.SaldoHembras);
                var htotSum = rows.Sum(r => r.HuevoTot);
                var porcPos = saldoH > 0 ? (double)htotSum / saldoH * 100d : 0d;

                return new ReporteGeneralDiarioDto(
                    Fecha:                    g.Key,
                    SemanaRelativa:           semR,
                    EdadDias:                 rows[0].EdadDias,
                    SaldoTotalHembras:        saldoH,
                    SaldoTotalMachos:         rows.Sum(r => r.SaldoMachos),
                    MortalidadTotalHembras:   rows.Sum(r => r.MortalidadHembras),
                    MortalidadTotalMachos:    rows.Sum(r => r.MortalidadMachos),
                    PorcMortalidadPromedio:   rows.Count > 0 ? rows.Average(r => r.PorcMortalidad) : 0d,
                    ConsKgHTotalKg:           rows.Sum(r => r.ConsKgH),
                    ConsKgMTotalKg:           rows.Sum(r => r.ConsKgM),
                    HuevosTotTotal:           htotSum,
                    HuevosIncTotal:           rows.Sum(r => r.HuevoInc),
                    PorcentajePosturaPromedio: porcPos,
                    PesoHuevoPromedio:        rows.Where(r => r.PesoHuevo > 0)
                                                  .Select(r => r.PesoHuevo)
                                                  .DefaultIfEmpty(0d).Average(),
                    UniformidadPromedio:      rows.Any(r => r.Uniformidad.HasValue)
                                                  ? rows.Where(r => r.Uniformidad.HasValue)
                                                        .Average(r => r.Uniformidad!.Value)
                                                  : null,
                    PorcentajePosturaGuia:    guia?.ProdPorc,
                    PesoHuevoGuia:            guia?.PesoHuevo,
                    HtaaGuia:                 guia?.HtotalAa,
                    DifPostura:  guia?.ProdPorc != null ? porcPos - guia.Value.ProdPorc.Value : null
                );
            })
            .ToList();

        // ── 5. SemanalesGeneral — consolidar semanalesGalpon por semana ────────
        var semanalesGeneral = semanalesGalpon
            .GroupBy(s => s.Semana)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var rows     = g.ToList();
                var guia     = ObtenerGuiaParaSemana(guiasCompletas, g.Key);
                var htotSum  = rows.Sum(r => r.HuevoTotSemanal);
                var saldoFin = rows.Sum(r => r.SaldoFinHembras);
                var porcPos  = saldoFin > 0
                    ? (double)htotSum / saldoFin * 100d
                    : rows.Count > 0 ? rows.Average(r => r.PorcentajePosturaPromedio) : 0d;
                var pesoHuevo = rows.Where(r => r.PesoHuevoPromedio > 0)
                                    .Select(r => r.PesoHuevoPromedio)
                                    .DefaultIfEmpty(0d).Average();

                return new ReporteGeneralSemanalDto(
                    Semana:                g.Key,
                    FechaInicioSemana:     rows.Min(r => r.FechaInicioSemana),
                    FechaFinSemana:        rows.Max(r => r.FechaFinSemana),
                    EdadSemanas:           g.Key,
                    SaldoInicioHembras:    rows.Sum(r => r.SaldoInicioHembras),
                    SaldoInicioMachos:     rows.Sum(r => r.SaldoInicioMachos),
                    SaldoFinHembras:       saldoFin,
                    SaldoFinMachos:        rows.Sum(r => r.SaldoFinMachos),
                    MortalidadTotalHembras: rows.Sum(r => r.MortalidadHembrasSemanal),
                    MortalidadTotalMachos:  rows.Sum(r => r.MortalidadMachosSemanal),
                    PorcMortalidadSemanal:  rows.Count > 0 ? rows.Average(r => r.PorcMortalidadSemanal) : 0d,
                    ConsKgHTotal:          rows.Sum(r => r.ConsKgHSemanal),
                    ConsKgMTotal:          rows.Sum(r => r.ConsKgMSemanal),
                    HuevosTotTotal:        htotSum,
                    HuevosIncTotal:        rows.Sum(r => r.HuevoIncSemanal),
                    PorcentajePosturaPromedio: porcPos,
                    PesoHuevoPromedio:     pesoHuevo,
                    PesoHPromedio:         rows.Any(r => r.PesoHPromedio.HasValue)
                        ? rows.Where(r => r.PesoHPromedio.HasValue).Average(r => r.PesoHPromedio!.Value) : null,
                    PesoMPromedio:         rows.Any(r => r.PesoMPromedio.HasValue)
                        ? rows.Where(r => r.PesoMPromedio.HasValue).Average(r => r.PesoMPromedio!.Value) : null,
                    UniformidadPromedio:   rows.Any(r => r.UniformidadPromedio.HasValue)
                        ? rows.Where(r => r.UniformidadPromedio.HasValue).Average(r => r.UniformidadPromedio!.Value) : null,
                    HtaaSemanal:           rows.Any(r => r.HtaaSemanal.HasValue)
                        ? rows.Where(r => r.HtaaSemanal.HasValue).Average(r => r.HtaaSemanal!.Value) : null,
                    PorcentajePosturaGuia:  guia?.ProdPorc,
                    PesoHuevoGuia:          guia?.PesoHuevo,
                    HtaaGuia:               guia?.HtotalAa,
                    UniformidadGuia:        guia?.Uniformidad,
                    DifPostura:   guia?.ProdPorc  != null ? porcPos   - guia.Value.ProdPorc.Value   : null,
                    DifPesoHuevo: guia?.PesoHuevo != null ? pesoHuevo - guia.Value.PesoHuevo.Value  : null
                );
            })
            .ToList();

        return new ReporteTecnicoProduccionTabsDto
        {
            LoteInfo        = MapearInformacionLoteFromLPP(lotesProduccion.First()),
            DiariosGalpon   = diariosGalpon,
            SemanalesGalpon = semanalesGalpon,
            DiariosGeneral  = diariosGeneral,
            SemanalesGeneral = semanalesGeneral
        };
    }
}
