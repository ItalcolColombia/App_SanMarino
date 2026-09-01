// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Tabs.cs
// Reporte de PRODUCCION con pestanas por galpon (Fase 4), leyendo desde produccion_diaria.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
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
        /// <summary>Desglose por ítems del día (`metadata.huevoItems`), ya resumido en Primera/Pnc/Otros.</summary>
        public ResumenHuevoPorTipo Huevo { get; set; }
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
                // OJO: `Huevo` NO se asigna acá. Ponerlo en la proyección -aunque sea `default`-
                // hace que EF lo lea como una constante del cliente dentro del Select y REVIENTE la
                // consulta entera con "The client projection contains a reference to a constant
                // expression"; el endpoint devolvía 404 con ese mensaje para TODAS las empresas.
                // Lo detectó el smoke HTTP, no los tests: el cálculo puro no toca EF.
                // Al ser una clase, el campo ya nace en su valor por defecto y lo llena
                // `ObtenerSegsProdTabsConItemsAsync` después, en memoria.
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Igual que <see cref="ObtenerSegsProdTabsAsync"/> pero además resuelve el desglose por ítems.
    /// Se usa SÓLO cuando la empresa clasifica por ítems: para las demás el `metadata` no se pide,
    /// así que su consulta queda exactamente como estaba.
    /// </summary>
    private async Task<List<SegProdTab>> ObtenerSegsProdTabsConItemsAsync(
        int lppId, DateTime? fechaInicio, DateTime? fechaFin, bool clasificacionPorItems,
        CancellationToken ct)
    {
        var segs = await ObtenerSegsProdTabsAsync(lppId, fechaInicio, fechaFin, ct);
        if (!clasificacionPorItems || segs.Count == 0) return segs;

        var query = _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LotePosturaProduccionId == lppId);

        if (fechaInicio.HasValue) query = query.Where(s => s.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)   query = query.Where(s => s.Fecha <= fechaFin.Value);

        var metadatos = await query
            .Select(s => new { s.Fecha, s.Metadata })
            .ToListAsync(ct);

        var porFecha = new Dictionary<DateTime, ResumenHuevoPorTipo>();
        foreach (var m in metadatos)
        {
            if (m.Metadata is null) continue;
            var items = HuevoItemsCalculos.LeerDeMetadata(m.Metadata.RootElement);
            porFecha[m.Fecha] = HuevoItemsResumenCalculos.Resumir(items);
        }

        foreach (var seg in segs)
            if (porFecha.TryGetValue(seg.Fecha, out var resumen))
                seg.Huevo = resumen;

        return segs;
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

        // Desempate DETERMINISTA cuando dos filas caen en la misma semana. Medido: la única
        // colisión real es `25P` (prepostura) contra `25`, una fila por raza/año en la guía de
        // esquema completo. Antes no importaba —con el eje viejo la semana 25 no se alcanzaba
        // nunca—; ahora sí, porque es justo el arranque de la postura. Se prefiere la grafía
        // NUMÉRICA PURA: `25P` es un caso aparte del calendario, no la semana 25 estándar.
        var candidatas = guias.Where(g => TryParseEdad(g.Edad) == semana).ToList();
        var guia = candidatas.FirstOrDefault(g => EsEdadNumericaPura(g.Edad))
                   ?? candidatas.FirstOrDefault();
        if (guia == null) return null;

        return (TryParse(guia.ProdPorcentaje), TryParse(guia.PesoHuevo),
                TryParse(guia.HTotalAa),       TryParse(guia.Uniformidad));
    }

    /// <summary>
    /// ¿La edad de la guía es un número sin sufijos? Distingue <c>"25"</c> de <c>"25P"</c>
    /// (prepostura), que el parseo tolerante colapsa en el mismo 25.
    /// </summary>
    private static bool EsEdadNumericaPura(string? edad) =>
        !string.IsNullOrWhiteSpace(edad) && edad.Trim().All(char.IsDigit);

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
        var guiaEsPropia   = false;
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
            else
            {
                // Vino de la tabla dedicada: es guía PROPIA. Se marca acá, donde se sabe, en vez de
                // volver a consultar.
                guiaEsPropia = true;
            }
        }

        // Cada empresa carga SU guía; lo que cambia es la tabla en que vive. La dedicada es un
        // modelo simple de 3 métricas, así que el reporte pinta sólo las columnas que esa guía
        // puede llenar; con la de esquema completo informa todas y no cambia nada.
        var guiaDisponibles = GuiaMetricasDisponiblesCalculos.Resolver(
            guiaEsPropia, AFilasGuiaMetricas(guiasCompletas));
        var semanaGuiaDesde = SemanaMinimaConGuia(guiasCompletas);

        var clasificacionPorItems = await ResolverClasificacionHuevoPorItemsAsync(ct);
        var cicloPorRaza          = await ResolverSemanasCicloPorRazaAsync(ct);

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

            var segs = await ObtenerSegsProdTabsConItemsAsync(
                lppId, request.FechaInicio, request.FechaFin, clasificacionPorItems, ct);
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

                // `semana` (relativa a producción) es lo que se PINTA y no cambia. La guía —la de
                // CUALQUIERA de las dos tablas: cada empresa carga la suya— está indexada por
                // semana de VIDA, así que el cruce usa ese eje. Ver SemanaGuiaProduccionCalculos.
                var semanaGuia = SemanaGuiaProduccionCalculos.Resolver(
                    s.Fecha, fechaInicioProd, lpp.FechaEncaset);

                // La etapa del ciclo también se mide en semanas de vida, así que reusa el mismo eje.
                var etapaCiclo = cicloPorRaza
                    ? SemanasCicloPosturaCalculos.ObtenerEtapa(lpp.Raza, semanaGuia)
                    : null;

                var porcPost = saldoH > 0 ? (double)s.HuevoTot / saldoH * 100d : 0d;
                var porcInc  = s.HuevoTot > 0 ? (double)s.HuevoInc / s.HuevoTot * 100d : 0d;
                var porcMort = hembrasIni > 0 ? (double)s.MortH / hembrasIni * 100d : 0d;

                var guia = ObtenerGuiaParaSemana(guiasCompletas, semanaGuia);

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
                    Observaciones: s.Observaciones,
                    HuevoPrimera: s.Huevo.Primera,
                    HuevoPnc:     s.Huevo.Pnc,
                    HuevoOtros:   s.Huevo.Otros,
                    SemanaGuia:   semanaGuia,
                    EtapaCiclo:   etapaCiclo
                ));
            }

            diariosGalpon.AddRange(diasLpp);

            // ── Agregar semanales por galpón ──────────────────────────────────
            foreach (var sg in diasLpp.GroupBy(d => d.SemanaRelativa).OrderBy(g => g.Key))
            {
                var rows  = sg.ToList();
                // El grupo sigue siendo por semana relativa (lo que se pinta); el cruce con la guía
                // usa el eje de la guía, que con la propia es la semana de vida.
                var guia  = ObtenerGuiaParaSemana(guiasCompletas, rows[0].SemanaGuia);
                var huevoSem = HuevoItemsResumenCalculos.Sumar(
                    rows.Select(r => new ResumenHuevoPorTipo(r.HuevoPrimera, r.HuevoPnc, r.HuevoOtros)));
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
                    DifPesoHuevo: guia?.PesoHuevo != null ? pesoHuevoSem - guia.Value.PesoHuevo.Value  : null,
                    HuevoPrimera: huevoSem.Primera,
                    HuevoPnc:     huevoSem.Pnc,
                    HuevoOtros:   huevoSem.Otros,
                    SemanaGuia:   rows[0].SemanaGuia,
                    EtapaCiclo:   rows[0].EtapaCiclo
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
                var guia    = ObtenerGuiaParaSemana(guiasCompletas, rows[0].SemanaGuia);
                var huevoDia = HuevoItemsResumenCalculos.Sumar(
                    rows.Select(r => new ResumenHuevoPorTipo(r.HuevoPrimera, r.HuevoPnc, r.HuevoOtros)));
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
                    DifPostura:  guia?.ProdPorc != null ? porcPos - guia.Value.ProdPorc.Value : null,
                    HuevoPrimera: huevoDia.Primera,
                    HuevoPnc:     huevoDia.Pnc,
                    HuevoOtros:   huevoDia.Otros,
                    SemanaGuia:   rows[0].SemanaGuia,
                    EtapaCiclo:   rows[0].EtapaCiclo
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
                var guia     = ObtenerGuiaParaSemana(guiasCompletas, rows[0].SemanaGuia);
                var huevoSemG = HuevoItemsResumenCalculos.Sumar(
                    rows.Select(r => new ResumenHuevoPorTipo(r.HuevoPrimera, r.HuevoPnc, r.HuevoOtros)));
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
                    DifPesoHuevo: guia?.PesoHuevo != null ? pesoHuevo - guia.Value.PesoHuevo.Value  : null,
                    HuevoPrimera: huevoSemG.Primera,
                    HuevoPnc:     huevoSemG.Pnc,
                    HuevoOtros:   huevoSemG.Otros,
                    SemanaGuia:   rows[0].SemanaGuia,
                    EtapaCiclo:   rows[0].EtapaCiclo
                );
            })
            .ToList();

        var loteInfo = MapearInformacionLoteFromLPP(lotesProduccion.First()) with
        {
            ClasificacionHuevoPorItems = clasificacionPorItems,
            GuiaMetricasDisponibles    = guiaDisponibles,
            SemanaGuiaDesde            = semanaGuiaDesde
        };

        return new ReporteTecnicoProduccionTabsDto
        {
            LoteInfo        = loteInfo,
            DiariosGalpon   = diariosGalpon,
            SemanalesGalpon = semanalesGalpon,
            DiariosGeneral  = diariosGeneral,
            SemanalesGeneral = semanalesGeneral
        };
    }
}
