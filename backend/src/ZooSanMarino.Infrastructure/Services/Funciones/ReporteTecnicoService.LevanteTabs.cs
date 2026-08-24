// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.LevanteTabs.cs
// Reporte de LEVANTE con pestanas (machos/hembras) consolidando varios LPL del mismo lote base.
// ObtenerReporteLevanteAsync navega: lote_postura_base -> lotes -> lote_postura_levante -> seguimiento_diario
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    /// <summary>
    /// Consolida datos diarios de machos por fecha (suma valores de múltiples sublotes)
    /// </summary>
    private List<ReporteTecnicoDiarioMachosDto> ConsolidarDatosDiariosMachos(
        List<ReporteTecnicoDiarioMachosDto> datos)
    {
        if (!datos.Any())
            return datos;

        var datosConsolidados = datos
            .GroupBy(d => d.Fecha.Date)
            .Select(g => new ReporteTecnicoDiarioMachosDto
            {
                Fecha = g.Key,
                EdadDias = g.First().EdadDias,
                EdadSemanas = g.First().EdadSemanas,
                SaldoMachos = g.Sum(d => d.SaldoMachos),
                MortalidadMachos = g.Sum(d => d.MortalidadMachos),
                MortalidadMachosAcumulada = g.Sum(d => d.MortalidadMachosAcumulada),
                MortalidadMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.MortalidadMachosPorcentajeDiario) / g.Count() : 0,
                MortalidadMachosPorcentajeAcumulado = g.First().MortalidadMachosPorcentajeAcumulado,
                SeleccionMachos = g.Sum(d => d.SeleccionMachos),
                SeleccionMachosAcumulada = g.Sum(d => d.SeleccionMachosAcumulada),
                SeleccionMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.SeleccionMachosPorcentajeDiario) / g.Count() : 0,
                SeleccionMachosPorcentajeAcumulado = g.First().SeleccionMachosPorcentajeAcumulado,
                TrasladosMachos = g.Sum(d => d.TrasladosMachos),
                TrasladosMachosAcumulados = g.Sum(d => d.TrasladosMachosAcumulados),
                ErrorSexajeMachos = g.Sum(d => d.ErrorSexajeMachos),
                ErrorSexajeMachosAcumulado = g.Sum(d => d.ErrorSexajeMachosAcumulado),
                ErrorSexajeMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.ErrorSexajeMachosPorcentajeDiario) / g.Count() : 0,
                ErrorSexajeMachosPorcentajeAcumulado = g.First().ErrorSexajeMachosPorcentajeAcumulado,
                DescarteMachos = g.Sum(d => d.DescarteMachos),
                DescarteMachosAcumulado = g.Sum(d => d.DescarteMachosAcumulado),
                DescarteMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.DescarteMachosPorcentajeDiario) / g.Count() : 0,
                DescarteMachosPorcentajeAcumulado = g.First().DescarteMachosPorcentajeAcumulado,
                ConsumoKgMachos = g.Sum(d => d.ConsumoKgMachos),
                ConsumoKgMachosAcumulado = g.Sum(d => d.ConsumoKgMachosAcumulado),
                ConsumoGramosPorAveMachos = g.Count() > 0 ? g.Sum(d => d.ConsumoGramosPorAveMachos) / g.Count() : 0,
                PesoPromedioMachos = g.Average(d => d.PesoPromedioMachos ?? 0),
                UniformidadMachos = g.Average(d => d.UniformidadMachos ?? 0),
                CoeficienteVariacionMachos = g.Average(d => d.CoeficienteVariacionMachos ?? 0),
                GananciaPesoMachos = g.Average(d => d.GananciaPesoMachos ?? 0),
                KcalAlMachos = g.Average(d => d.KcalAlMachos ?? 0),
                ProtAlMachos = g.Average(d => d.ProtAlMachos ?? 0),
                KcalAveMachos = g.Average(d => d.KcalAveMachos ?? 0),
                ProtAveMachos = g.Average(d => d.ProtAveMachos ?? 0),
                IngresosAlimentoKilos = g.Sum(d => d.IngresosAlimentoKilos),
                TrasladosAlimentoKilos = g.Sum(d => d.TrasladosAlimentoKilos),
                Observaciones = g.First().Observaciones
            })
            .ToList();

        return datosConsolidados;
    }

    /// <summary>
    /// Consolida datos diarios de hembras por fecha (suma valores de múltiples sublotes)
    /// </summary>
    private List<ReporteTecnicoDiarioHembrasDto> ConsolidarDatosDiariosHembras(
        List<ReporteTecnicoDiarioHembrasDto> datos)
    {
        if (!datos.Any())
            return datos;

        var datosConsolidados = datos
            .GroupBy(d => d.Fecha.Date)
            .Select(g => new ReporteTecnicoDiarioHembrasDto
            {
                Fecha = g.Key,
                EdadDias = g.First().EdadDias,
                EdadSemanas = g.First().EdadSemanas,
                SaldoHembras = g.Sum(d => d.SaldoHembras),
                MortalidadHembras = g.Sum(d => d.MortalidadHembras),
                MortalidadHembrasAcumulada = g.Sum(d => d.MortalidadHembrasAcumulada),
                MortalidadHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.MortalidadHembrasPorcentajeDiario) / g.Count() : 0,
                MortalidadHembrasPorcentajeAcumulado = g.First().MortalidadHembrasPorcentajeAcumulado,
                SeleccionHembras = g.Sum(d => d.SeleccionHembras),
                SeleccionHembrasAcumulada = g.Sum(d => d.SeleccionHembrasAcumulada),
                SeleccionHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.SeleccionHembrasPorcentajeDiario) / g.Count() : 0,
                SeleccionHembrasPorcentajeAcumulado = g.First().SeleccionHembrasPorcentajeAcumulado,
                TrasladosHembras = g.Sum(d => d.TrasladosHembras),
                TrasladosHembrasAcumulados = g.Sum(d => d.TrasladosHembrasAcumulados),
                ErrorSexajeHembras = g.Sum(d => d.ErrorSexajeHembras),
                ErrorSexajeHembrasAcumulado = g.Sum(d => d.ErrorSexajeHembrasAcumulado),
                ErrorSexajeHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.ErrorSexajeHembrasPorcentajeDiario) / g.Count() : 0,
                ErrorSexajeHembrasPorcentajeAcumulado = g.First().ErrorSexajeHembrasPorcentajeAcumulado,
                DescarteHembras = g.Sum(d => d.DescarteHembras),
                DescarteHembrasAcumulado = g.Sum(d => d.DescarteHembrasAcumulado),
                DescarteHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.DescarteHembrasPorcentajeDiario) / g.Count() : 0,
                DescarteHembrasPorcentajeAcumulado = g.First().DescarteHembrasPorcentajeAcumulado,
                ConsumoKgHembras = g.Sum(d => d.ConsumoKgHembras),
                ConsumoKgHembrasAcumulado = g.Sum(d => d.ConsumoKgHembrasAcumulado),
                ConsumoGramosPorAveHembras = g.Count() > 0 ? g.Sum(d => d.ConsumoGramosPorAveHembras) / g.Count() : 0,
                PesoPromedioHembras = g.Average(d => d.PesoPromedioHembras ?? 0),
                UniformidadHembras = g.Average(d => d.UniformidadHembras ?? 0),
                CoeficienteVariacionHembras = g.Average(d => d.CoeficienteVariacionHembras ?? 0),
                GananciaPesoHembras = g.Average(d => d.GananciaPesoHembras ?? 0),
                KcalAlHembras = g.Average(d => d.KcalAlHembras ?? 0),
                ProtAlHembras = g.Average(d => d.ProtAlHembras ?? 0),
                KcalAveHembras = g.Average(d => d.KcalAveHembras ?? 0),
                ProtAveHembras = g.Average(d => d.ProtAveHembras ?? 0),
                IngresosAlimentoKilos = g.Sum(d => d.IngresosAlimentoKilos),
                TrasladosAlimentoKilos = g.Sum(d => d.TrasladosAlimentoKilos),
                Observaciones = g.First().Observaciones
            })
            .ToList();

        return datosConsolidados;
    }

    /// <summary>
    /// Genera reporte técnico de Levante con estructura de tabs
    /// Incluye datos diarios separados (machos y hembras) y datos semanales completos
    /// </summary>
    public async Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Nucleo)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);

            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");

            // Determinar lista de lotes a procesar (consolidado o solo el actual)
            List<LotePosturaLevante> lotesAProcesar;
            if (consolidarSublotes)
            {
                lotesAProcesar = await ObtenerSublotesLevantePorLoteBaseAsync(lotePosturaLevanteId, ct);
                if (!lotesAProcesar.Any())
                {
                    lotesAProcesar = new List<LotePosturaLevante> { lpl };
                }
            }
            else
            {
                lotesAProcesar = new List<LotePosturaLevante> { lpl };
            }

            // Generar datos diarios consolidados (machos y hembras)
            var todosDatosDiariosMachos = new List<ReporteTecnicoDiarioMachosDto>();
            var todosDatosDiariosHembras = new List<ReporteTecnicoDiarioHembrasDto>();

            foreach (var lote in lotesAProcesar)
            {
                var datosMachos = await GenerarReporteDiarioMachosAsync(lote.LotePosturaLevanteId ?? 0, fechaInicio, fechaFin, ct);
                var datosHembras = await GenerarReporteDiarioHembrasAsync(lote.LotePosturaLevanteId ?? 0, fechaInicio, fechaFin, ct);

                todosDatosDiariosMachos.AddRange(datosMachos);
                todosDatosDiariosHembras.AddRange(datosHembras);
            }

            // Consolidar datos diarios por fecha si es necesario (sumando valores)
            var datosDiariosMachosFinales = consolidarSublotes
                ? ConsolidarDatosDiariosMachos(todosDatosDiariosMachos)
                : todosDatosDiariosMachos;

            var datosDiariosHembrasFinales = consolidarSublotes
                ? ConsolidarDatosDiariosHembras(todosDatosDiariosHembras)
                : todosDatosDiariosHembras;

            // Generar reporte semanal consolidado
            var reporteCompleto = await GenerarReporteLevanteCompletoAsync(lotePosturaLevanteId, consolidarSublotes, ct);

            var infoLote = MapearInformacionLoteFromLPL(lpl);
            var sublote = ExtraerSublote(lpl.LoteNombre);
            infoLote.Sublote = consolidarSublotes ? null : sublote;
            infoLote.Etapa = "LEVANTE";

            return new ReporteTecnicoLevanteConTabsDto
            {
                InformacionLote = infoLote,
                DatosDiariosMachos = datosDiariosMachosFinales.OrderBy(d => d.Fecha).ToList(),
                DatosDiariosHembras = datosDiariosHembrasFinales.OrderBy(d => d.Fecha).ToList(),
                DatosSemanales = reporteCompleto.DatosSemanales,
                EsConsolidado = consolidarSublotes,
                SublotesIncluidos = reporteCompleto.SublotesIncluidos
            };
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte con tabs para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }

    public async Task<ReporteTecnicoLevanteCompletoDto> ObtenerReporteLevanteAsync(
        ObtenerReporteLevanteRequestDto request,
        CancellationToken ct = default)
    {
        // --- 1. Validar existencia del LotePosturaBase ---
        _ = await _ctx.LotePosturaBases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                lpb => lpb.LotePosturaBaseId == request.LotePosturaBaseId
                    && lpb.CompanyId == _currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException(
                $"LotePosturaBase con ID {request.LotePosturaBaseId} no encontrado.");

        // --- 2. Navegar a lotes intermedios ---
        var lotesIds = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePosturaBaseId == request.LotePosturaBaseId
                     && l.CompanyId == _currentUser.CompanyId
                     && l.DeletedAt == null)
            .Select(l => (int?)l.LoteId)
            .ToListAsync(ct);

        if (!lotesIds.Any())
            throw new InvalidOperationException(
                $"No hay lotes asociados al LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 3. Obtener lotes levante ---
        // Toda fila viva de lote_postura_levante ES el registro de levante del lote: la columna
        // `etapa` sólo queda en "Produccion" por la derivación por edad al crear el lote (un
        // encaset de hace más de 26 semanas ⇒ carga de histórico) y el paso real a producción
        // nunca la actualiza. Filtrar por etapa == "Levante" escondía justamente los lotes
        // cargados con historia — ver FaseLoteCalculos.EsRegistroLevante.
        var lotesLevanteQuery = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Include(lpl => lpl.Farm)
            .Include(lpl => lpl.Nucleo)
            .Where(lpl => lotesIds.Contains(lpl.LoteId)
                       && lpl.CompanyId == _currentUser.CompanyId
                       && lpl.DeletedAt == null);

        if (request.LoteLevanteId.HasValue)
            lotesLevanteQuery = lotesLevanteQuery
                .Where(lpl => lpl.LotePosturaLevanteId == request.LoteLevanteId.Value);

        var lotesLevante = await lotesLevanteQuery
            .OrderBy(lpl => lpl.FechaEncaset)
            .ThenBy(lpl => lpl.LoteNombre)
            .ToListAsync(ct);

        if (!lotesLevante.Any())
            throw new InvalidOperationException(
                $"No se encontraron lotes levante para LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 4. Recopilar seguimientos + acumular aves iniciales (Opción A) ---
        // Clave: lotePosturaLevanteId → (seguimientos, fechaEncaset)
        var seguimientosPorLpl = new List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)>();
        var sublotesIncluidos = new List<string>();
        var avesHInicialesTotal = 0;
        var avesMInicialesTotal = 0;

        foreach (var lpl in lotesLevante)
        {
            if (!lpl.LotePosturaLevanteId.HasValue || !lpl.FechaEncaset.HasValue)
                continue;

            var segs = await ObtenerSeguimientosLevantePorLPLAsync(lpl.LotePosturaLevanteId.Value, ct);

            // Restricción: solo semanas 1-25
            segs = segs.Where(s =>
            {
                var dias = CalcularEdadDias(lpl.FechaEncaset.Value, s.FechaRegistro);
                return CalcularEdadSemanas(dias) <= 25;
            }).ToList();

            // Filtro de rango de fechas (opcional)
            if (request.FechaInicio.HasValue)
                segs = segs.Where(s => s.FechaRegistro >= request.FechaInicio.Value).ToList();
            if (request.FechaFin.HasValue)
                segs = segs.Where(s => s.FechaRegistro <= request.FechaFin.Value).ToList();

            seguimientosPorLpl.Add((lpl, segs));
            sublotesIncluidos.Add(lpl.LoteNombre);

            // Denominador para porcentajes: aves vivas al inicio de levante (después de mortalidad en caja)
            avesHInicialesTotal += lpl.AvesHInicial ?? lpl.HembrasL ?? 0;
            avesMInicialesTotal += lpl.AvesMInicial ?? lpl.MachosL ?? 0;
        }

        if (!seguimientosPorLpl.Any())
            throw new InvalidOperationException(
                $"No hay seguimientos de levante para LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 5. Cargar guía genética desde ProduccionAvicolaRaw (mismo patrón que GenerarReporteLevanteCompletoAsync) ---
        var primerLpl = lotesLevante.First();
        var guiasRaw = new Dictionary<int, Domain.Entities.ProduccionAvicolaRaw>();
        var guiasGenetica = new Dictionary<int, GuiaGeneticaDto>();

        if (!string.IsNullOrWhiteSpace(primerLpl.Raza) && primerLpl.AnoTablaGenetica.HasValue)
        {
            try
            {
                var razaNorm = primerLpl.Raza.Trim().ToLower();
                var ano = primerLpl.AnoTablaGenetica.Value.ToString();

                // Santa Reyes tiene su guia en tabla propia (F2.2). Se pregunta primero; si la
                // empresa no tiene guia propia la lista vuelve vacia y corre la de siempre.
                var guiasRawList = await GuiaGeneticaLookup.ObtenerFilasPropiasAsync(
                    _ctx, _currentUser.CompanyId, razaNorm, ano, ct);

                if (guiasRawList.Count == 0)
                {
                    guiasRawList = await _ctx.ProduccionAvicolaRaw
                        .AsNoTracking()
                        .Where(p =>
                            p.Raza != null && p.AnioGuia != null &&
                            EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                            p.AnioGuia.Trim() == ano &&
                            p.CompanyId == _currentUser.CompanyId &&
                            p.DeletedAt == null)
                        .ToListAsync(ct);
                }

                foreach (var guia in guiasRawList)
                {
                    if (int.TryParse(guia.Edad?.Trim().Replace(",", ".").Split('.')[0], out var edad)
                        && edad >= 1 && edad <= 25)
                        guiasRaw[edad] = guia;
                }

                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(
                    primerLpl.Raza, primerLpl.AnoTablaGenetica.Value, edadDesde: 1, edadHasta: 25);
                guiasGenetica = guias.ToDictionary(g => g.Edad, g => g);
            }
            catch { /* Si no hay guía genética, los campos GUIA quedan null */ }
        }

        var infoLote = MapearInformacionLoteFromLPL(lotesLevante.First());
        infoLote.Etapa = "LEVANTE";
        var esConsolidado = !request.LoteLevanteId.HasValue && lotesLevante.Count > 1;

        // --- 6. Ramificar según periodicidad ---
        if (request.FiltroPeriodicidad.Equals("Semanal", StringComparison.OrdinalIgnoreCase))
        {
            var datosSemanales = GenerarSemanalesConsolidados(
                seguimientosPorLpl, avesHInicialesTotal, avesMInicialesTotal,
                guiasGenetica, guiasRaw);

            return new ReporteTecnicoLevanteCompletoDto
            {
                InformacionLote = infoLote,
                DatosSemanales = datosSemanales,
                DatosDiarios = new List<ReporteTecnicoDiarioLevanteDto>(),
                EsConsolidado = esConsolidado,
                SublotesIncluidos = sublotesIncluidos
            };
        }
        else // Diario
        {
            var datosDiarios = GenerarDiariosConsolidados(
                seguimientosPorLpl, avesHInicialesTotal, avesMInicialesTotal);

            return new ReporteTecnicoLevanteCompletoDto
            {
                InformacionLote = infoLote,
                DatosSemanales = new List<ReporteTecnicoLevanteSemanalDto>(),
                DatosDiarios = datosDiarios,
                EsConsolidado = esConsolidado,
                SublotesIncluidos = sublotesIncluidos
            };
        }
    }

    /// <summary>
    /// Construye la lista de datos DIARIOS consolidados desde múltiples lotes levante.
    /// Agrupa por fecha de calendario. Porcentajes recalculados sobre avesH/M iniciales totales.
    /// </summary>
    private List<ReporteTecnicoDiarioLevanteDto> GenerarDiariosConsolidados(
        List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)> fuentes,
        int avesHInicialesTotal,
        int avesMInicialesTotal)
    {
        // Aplanar todos los seguimientos con su lpl de origen para calcular edad
        var filasBruto = fuentes
            .SelectMany(f => f.Segs.Select(s => (f.Lpl, Seg: s)))
            .OrderBy(x => x.Seg.FechaRegistro)
            .ToList();

        if (!filasBruto.Any())
            return new List<ReporteTecnicoDiarioLevanteDto>();

        // Agrupar por fecha de calendario (consolidado multi-lote)
        var porFecha = filasBruto
            .GroupBy(x => x.Seg.FechaRegistro.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var resultado = new List<ReporteTecnicoDiarioLevanteDto>();
        int acMortH = 0, acMortM = 0;
        double acConsH = 0, acConsM = 0;
        int saldoH = avesHInicialesTotal;
        int saldoM = avesMInicialesTotal;

        foreach (var grupo in porFecha)
        {
            var items = grupo.ToList();

            // Edad calculada desde el primer lpl del grupo (referencia temporal)
            var lplRef = items.First().Lpl;
            var edadDias = lplRef.FechaEncaset.HasValue
                ? CalcularEdadDias(lplRef.FechaEncaset.Value, grupo.Key)
                : 0;
            var edadSemanas = CalcularEdadSemanas(edadDias);

            // Sumas del día
            var mortH = items.Sum(x => x.Seg.MortalidadHembras);
            var mortM = items.Sum(x => x.Seg.MortalidadMachos);
            var selH = items.Sum(x => Math.Max(0, x.Seg.SelH));
            var selM = items.Sum(x => Math.Max(0, x.Seg.SelM));
            var errH = items.Sum(x => x.Seg.ErrorSexajeHembras);
            var errM = items.Sum(x => x.Seg.ErrorSexajeMachos);
            var trasSalH = items.Sum(x => x.Seg.TrasladoSalidaHembras);
            var trasSalM = items.Sum(x => x.Seg.TrasladoSalidaMachos);
            var trasIngH = items.Sum(x => x.Seg.TrasladoIngresoHembras);
            var trasIngM = items.Sum(x => x.Seg.TrasladoIngresoMachos);
            var ventaH = items.Sum(x => x.Seg.VentaAvesHembras);
            var ventaM = items.Sum(x => x.Seg.VentaAvesMachos);
            var consH = items.Sum(x => x.Seg.ConsumoKgHembras);
            var consM = items.Sum(x => x.Seg.ConsumoKgMachos ?? 0);

            // Promedios ponderados (promedio simple; lotes con datos null se excluyen)
            var pesoH = items.Where(x => x.Seg.PesoPromH.HasValue).Select(x => x.Seg.PesoPromH!.Value).DefaultIfEmpty().Average();
            var pesoM = items.Where(x => x.Seg.PesoPromM.HasValue).Select(x => x.Seg.PesoPromM!.Value).DefaultIfEmpty().Average();
            var unifH = items.Where(x => x.Seg.UniformidadH.HasValue).Select(x => x.Seg.UniformidadH!.Value).DefaultIfEmpty().Average();
            var unifM = items.Where(x => x.Seg.UniformidadM.HasValue).Select(x => x.Seg.UniformidadM!.Value).DefaultIfEmpty().Average();
            var cvH = items.Where(x => x.Seg.CvH.HasValue).Select(x => x.Seg.CvH!.Value).DefaultIfEmpty().Average();
            var cvM = items.Where(x => x.Seg.CvM.HasValue).Select(x => x.Seg.CvM!.Value).DefaultIfEmpty().Average();
            var kcalAlH = items.Where(x => x.Seg.KcalAlH.HasValue).Select(x => x.Seg.KcalAlH!.Value).DefaultIfEmpty().Average();
            var protAlH = items.Where(x => x.Seg.ProtAlH.HasValue).Select(x => x.Seg.ProtAlH!.Value).DefaultIfEmpty().Average();
            var kcalAveH = items.Where(x => x.Seg.KcalAveH.HasValue).Select(x => x.Seg.KcalAveH!.Value).DefaultIfEmpty().Average();
            var protAveH = items.Where(x => x.Seg.ProtAveH.HasValue).Select(x => x.Seg.ProtAveH!.Value).DefaultIfEmpty().Average();

            // Actualizar saldos (antes de mortalidad del día = denominador porcentaje diario).
            // El saldo lo resuelve SaldoAvesLevanteCalculos, que es la especificación ejecutable de
            // fn_reporte_semanal_levante_extras: además de mortalidad y selección descuenta el
            // ERROR DE SEXAJE y los traslados. Antes solo restaba mort+sel, así que el reporte
            // cerraba por encima del maestro (lote_postura_levante.aves_h_actual) y del informe
            // técnico, e infravaloraba el gr/ave/día al dividir por un saldo inflado.
            //
            // ⭐ 2026-08-17: y la VENTA. Este service quedó fuera del arreglo de las fns SQL y era el
            // último lector que no la descontaba, así que reproducía el mismo defecto con el saldo
            // inflado por las aves vendidas — y por el mismo mecanismo subestimaba el gr/ave/día.
            var saldoHAntesMort = saldoH;
            var saldoMAntesMort = saldoM;
            saldoH = SaldoAvesLevanteCalculos.Siguiente(saldoH,
                new SaldoAvesLevanteCalculos.MovimientoDia(mortH, selH, errH, trasSalH, trasIngH, ventaH));
            saldoM = SaldoAvesLevanteCalculos.Siguiente(saldoM,
                new SaldoAvesLevanteCalculos.MovimientoDia(mortM, selM, errM, trasSalM, trasIngM, ventaM));

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acConsH += consH;
            acConsM += consM;

            // Recalcular porcentajes sobre total unificado (Opción A)
            var porcMortH = saldoHAntesMort > 0 ? (double)mortH / saldoHAntesMort * 100 : 0;
            var porcMortM = saldoMAntesMort > 0 ? (double)mortM / saldoMAntesMort * 100 : 0;
            var porcMortHAc = avesHInicialesTotal > 0 ? (double)acMortH / avesHInicialesTotal * 100 : 0;
            var porcMortMAc = avesMInicialesTotal > 0 ? (double)acMortM / avesMInicialesTotal * 100 : 0;

            resultado.Add(new ReporteTecnicoDiarioLevanteDto
            {
                Fecha = grupo.Key,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoHembras = Math.Max(0, saldoH),
                MortalidadHembras = mortH,
                MortalidadHembrasAcumulada = acMortH,
                PorcMortH = Math.Round(porcMortH, 4),
                PorcMortHAcumulado = Math.Round(porcMortHAc, 4),
                SelH = selH,
                ErrorSexajeH = errH,
                ConsumoKgH = Math.Round(consH, 3),
                ConsumoKgHAcumulado = Math.Round(acConsH, 3),
                PesoPromH = pesoH > 0 ? pesoH : null,
                UniformidadH = unifH > 0 ? unifH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,
                KcalAveH = kcalAveH > 0 ? kcalAveH : null,
                ProtAveH = protAveH > 0 ? protAveH : null,
                SaldoMachos = Math.Max(0, saldoM),
                MortalidadMachos = mortM,
                MortalidadMachosAcumulada = acMortM,
                PorcMortM = Math.Round(porcMortM, 4),
                PorcMortMAcumulado = Math.Round(porcMortMAc, 4),
                SelM = selM,
                ErrorSexajeM = errM,
                ConsumoKgM = Math.Round(consM, 3),
                ConsumoKgMAcumulado = Math.Round(acConsM, 3),
                PesoPromM = pesoM > 0 ? pesoM : null,
                UniformidadM = unifM > 0 ? unifM : null,
                CvM = cvM > 0 ? cvM : null,
                Observaciones = items.FirstOrDefault(x => x.Seg.Observaciones != null).Seg?.Observaciones
            });
        }

        return resultado;
    }

    /// <summary>
    /// Construye la lista de datos SEMANALES consolidados (semanas de levante 1-25).
    /// Semanas calculadas relativas a cada lote. Porcentajes recalculados sobre avesH/M iniciales totales.
    /// </summary>
    private List<ReporteTecnicoLevanteSemanalDto> GenerarSemanalesConsolidados(
        List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)> fuentes,
        int avesHInicialesTotal,
        int avesMInicialesTotal,
        Dictionary<int, GuiaGeneticaDto>? guiasGenetica = null,
        Dictionary<int, Domain.Entities.ProduccionAvicolaRaw>? guiasRaw = null)
    {
        static double ParseGuiaV(string? value) =>
            double.TryParse(value?.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;

        // Variables acumuladas a lo largo de las semanas (persisten entre iteraciones)
        int acMortH = 0, acSelH = 0, acErrH = 0;
        int acMortM = 0, acSelM = 0, acErrM = 0;
        int acTrasSalH = 0, acTrasSalM = 0, acTrasIngH = 0, acTrasIngM = 0;
        int acVentaH = 0, acVentaM = 0;
        double acConsH = 0, acConsM = 0;
        double acKcalSemH = 0;
        double acProtSemH = 0;
        double? consAcGrHAnterior = null;
        double? consAcGrMAnterior = null;
        double? consAcGrHGUIAAnterior = null;
        double? consAcGrMGUIAAnterior = null;

        var resultado = new List<ReporteTecnicoLevanteSemanalDto>();

        for (int semana = 1; semana <= 25; semana++)
        {
            // Recopilar todos los registros de esta semana de todos los lotes
            var registrosSemana = fuentes
                .SelectMany(f =>
                    f.Segs
                     .Where(s =>
                     {
                         if (!f.Lpl.FechaEncaset.HasValue) return false;
                         var dias = CalcularEdadDias(f.Lpl.FechaEncaset.Value, s.FechaRegistro);
                         return CalcularEdadSemanas(dias) == semana;
                     })
                     .Select(s => (f.Lpl, Seg: s))
                )
                .ToList();

            if (!registrosSemana.Any())
                continue;

            // Cruce genético con ProduccionAvicolaRaw (Tarea 1.6)
            var guiaGenetica = guiasGenetica != null && guiasGenetica.TryGetValue(semana, out var gg) ? gg : null;
            var guiaRaw = guiasRaw != null && guiasRaw.TryGetValue(semana, out var gr) ? gr : null;

            // Calcular valores de la semana (sumas)
            var mortH = registrosSemana.Sum(x => x.Seg.MortalidadHembras);
            var mortM = registrosSemana.Sum(x => x.Seg.MortalidadMachos);
            var selH = registrosSemana.Sum(x => Math.Max(0, x.Seg.SelH));
            var selM = registrosSemana.Sum(x => Math.Max(0, x.Seg.SelM));
            var errH = registrosSemana.Sum(x => x.Seg.ErrorSexajeHembras);
            var errM = registrosSemana.Sum(x => x.Seg.ErrorSexajeMachos);
            var trasSalH = registrosSemana.Sum(x => x.Seg.TrasladoSalidaHembras);
            var trasSalM = registrosSemana.Sum(x => x.Seg.TrasladoSalidaMachos);
            var trasIngH = registrosSemana.Sum(x => x.Seg.TrasladoIngresoHembras);
            var trasIngM = registrosSemana.Sum(x => x.Seg.TrasladoIngresoMachos);
            var ventaH = registrosSemana.Sum(x => x.Seg.VentaAvesHembras);
            var ventaM = registrosSemana.Sum(x => x.Seg.VentaAvesMachos);
            var consKgH = registrosSemana.Sum(x => x.Seg.ConsumoKgHembras);
            var consKgM = registrosSemana.Sum(x => x.Seg.ConsumoKgMachos ?? 0);

            // Promedios ponderados para pesos y uniformidades
            var pesoH = registrosSemana.Where(x => x.Seg.PesoPromH.HasValue)
                                       .Select(x => x.Seg.PesoPromH!.Value)
                                       .DefaultIfEmpty().Average();
            var pesoM = registrosSemana.Where(x => x.Seg.PesoPromM.HasValue)
                                       .Select(x => x.Seg.PesoPromM!.Value)
                                       .DefaultIfEmpty().Average();
            var unifH = registrosSemana.Where(x => x.Seg.UniformidadH.HasValue)
                                       .Select(x => x.Seg.UniformidadH!.Value)
                                       .DefaultIfEmpty().Average();
            var unifM = registrosSemana.Where(x => x.Seg.UniformidadM.HasValue)
                                       .Select(x => x.Seg.UniformidadM!.Value)
                                       .DefaultIfEmpty().Average();
            var cvH = registrosSemana.Where(x => x.Seg.CvH.HasValue)
                                     .Select(x => x.Seg.CvH!.Value)
                                     .DefaultIfEmpty().Average();
            var cvM = registrosSemana.Where(x => x.Seg.CvM.HasValue)
                                     .Select(x => x.Seg.CvM!.Value)
                                     .DefaultIfEmpty().Average();
            var kcalAlH = registrosSemana.Where(x => x.Seg.KcalAlH.HasValue)
                                         .Select(x => x.Seg.KcalAlH!.Value)
                                         .DefaultIfEmpty().Average();
            var protAlH = registrosSemana.Where(x => x.Seg.ProtAlH.HasValue)
                                         .Select(x => x.Seg.ProtAlH!.Value)
                                         .DefaultIfEmpty().Average();
            var kcalAveH = registrosSemana.Where(x => x.Seg.KcalAveH.HasValue)
                                          .Select(x => x.Seg.KcalAveH!.Value)
                                          .DefaultIfEmpty().Average();
            var protAveH = registrosSemana.Where(x => x.Seg.ProtAveH.HasValue)
                                          .Select(x => x.Seg.ProtAveH!.Value)
                                          .DefaultIfEmpty().Average();

            // Fecha de referencia: último día registrado en esta semana
            var fechaSemana = registrosSemana.Max(x => x.Seg.FechaRegistro);
            var lplRef = registrosSemana.First().Lpl;
            var edadDias = lplRef.FechaEncaset.HasValue
                ? CalcularEdadDias(lplRef.FechaEncaset.Value, fechaSemana)
                : semana * 7;

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acSelH += selH;
            acSelM += selM;
            acErrH += errH;
            acErrM += errM;
            acTrasSalH += trasSalH;
            acTrasSalM += trasSalM;
            acTrasIngH += trasIngH;
            acTrasIngM += trasIngM;
            acVentaH += ventaH;
            acVentaM += ventaM;
            acConsH += consKgH;
            acConsM += consKgM;

            // Saldos actuales (aves vivas al cierre de la semana). Misma fórmula que el diario y
            // que fn_reporte_semanal_levante_extras: descuenta también el ERROR DE SEXAJE (que ya
            // veníamos acumulando en acErrH/acErrM para el % de retiro), los traslados de aves y
            // —desde 2026-08-17— la VENTA.
            var hembraActual = SaldoAvesLevanteCalculos.SaldoFinal(avesHInicialesTotal,
                new[] { new SaldoAvesLevanteCalculos.MovimientoDia(acMortH, acSelH, acErrH, acTrasSalH, acTrasIngH, acVentaH) });
            var machoActual = SaldoAvesLevanteCalculos.SaldoFinal(avesMInicialesTotal,
                new[] { new SaldoAvesLevanteCalculos.MovimientoDia(acMortM, acSelM, acErrM, acTrasSalM, acTrasIngM, acVentaM) });

            // ---- Recálculo de porcentajes sobre total unificado (Opción A) ----
            var porcMortH = avesHInicialesTotal > 0
                ? (double)mortH / avesHInicialesTotal * 100 : 0;
            var porcMortM = avesMInicialesTotal > 0
                ? (double)mortM / avesMInicialesTotal * 100 : 0;
            var porcSelH = avesHInicialesTotal > 0
                ? (double)selH / avesHInicialesTotal * 100 : 0;
            var porcSelM = avesMInicialesTotal > 0
                ? (double)selM / avesMInicialesTotal * 100 : 0;
            var porcErrH = avesHInicialesTotal > 0
                ? (double)errH / avesHInicialesTotal * 100 : 0;
            var porcErrM = avesMInicialesTotal > 0
                ? (double)errM / avesMInicialesTotal * 100 : 0;

            // Relación machos/hembras
            var relMH = hembraActual > 0
                ? (double)machoActual / hembraActual * 100 : 0;

            // Consumo acumulado en g/ave
            var consAcGrH = avesHInicialesTotal > 0
                ? acConsH * 1000 / avesHInicialesTotal : 0;
            var consAcGrM = avesMInicialesTotal > 0
                ? acConsM * 1000 / avesMInicialesTotal : 0;

            // g/ave/día semana
            var grAveDiaH = hembraActual > 0 ? consKgH * 1000 / hembraActual / 7 : 0;
            var grAveDiaM = machoActual > 0 ? consKgM * 1000 / machoActual / 7 : 0;

            // Incremento de consumo acumulado vs semana anterior
            var incrConsH = consAcGrHAnterior.HasValue
                ? consAcGrH - consAcGrHAnterior.Value : 0;
            var incrConsM = consAcGrMAnterior.HasValue
                ? consAcGrM - consAcGrMAnterior.Value : 0;

            consAcGrHAnterior = consAcGrH;
            consAcGrMAnterior = consAcGrM;

            // Campos GUIA desde ProduccionAvicolaRaw (consumo acumulado en g/ave)
            var consAcGrHGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.ConsAcH) : null;
            var consAcGrMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.ConsAcM) : null;
            var incrConsHGUIA = consAcGrHGUIAAnterior.HasValue && consAcGrHGUIA.HasValue
                ? consAcGrHGUIA.Value - consAcGrHGUIAAnterior.Value
                : (semana == 1 ? consAcGrHGUIA : null);
            var incrConsMGUIA = consAcGrMGUIAAnterior.HasValue && consAcGrMGUIA.HasValue
                ? consAcGrMGUIA.Value - consAcGrMGUIAAnterior.Value
                : (semana == 1 ? consAcGrMGUIA : null);
            if (consAcGrHGUIA.HasValue) consAcGrHGUIAAnterior = consAcGrHGUIA.Value;
            if (consAcGrMGUIA.HasValue) consAcGrMGUIAAnterior = consAcGrMGUIA.Value;

            // Métricas nutricionales acumuladas (machos no tienen KcalAl en el esquema actual)
            var kcalSemH = kcalAlH * consKgH;
            var protSemH = protAlH > 0 ? (protAlH / 100) * consKgH : 0;
            acKcalSemH += kcalSemH;
            acProtSemH += protSemH;

            // Retiros acumulados (M+S+E)
            var msEH = mortH + selH + errH;
            var msEM = mortM + selM + errM;
            var retAcH = acMortH + acSelH + acErrH;
            var retAcM = acMortM + acSelM + acErrM;
            var porcRetH = avesHInicialesTotal > 0
                ? (double)retAcH / avesHInicialesTotal * 100 : 0;
            var porcRetM = avesMInicialesTotal > 0
                ? (double)retAcM / avesMInicialesTotal * 100 : 0;

            // Semana del año (ISO)
            var semAno = System.Globalization.ISOWeek.GetWeekOfYear(fechaSemana);

            resultado.Add(new ReporteTecnicoLevanteSemanalDto
            {
                Semana = semana,
                Edad = edadDias,
                SemAno = semAno,
                Fecha = fechaSemana,

                // Datos de encasetamiento
                HembraIni = avesHInicialesTotal,
                MachoIni = avesMInicialesTotal,

                // Raza y línea del primer lote de referencia
                Raza = lplRef.Raza,
                AnoG = lplRef.AnoTablaGenetica,
                Granja = lplRef.Farm?.Name,
                Regional = lplRef.Regional,
                CodGuia = lplRef.CodigoGuiaGenetica,
                NucleoL = lplRef.NucleoId,

                // Hembras
                Hembra = Math.Max(0, hembraActual),
                MortH = mortH,
                SelH = selH,
                ErrorH = errH,
                ConsKgH = Math.Round(consKgH, 3),
                // Gramos del seguimiento → kg, la unidad de la columna y de la guía de al lado.
                PesoH = PesoLevanteCalculos.AKilos(pesoH),
                UniformH = unifH > 0 ? unifH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,
                KcalAveH = kcalAveH > 0 ? kcalAveH : null,
                ProtAveH = protAveH > 0 ? protAveH : null,

                // Machos
                SaldoMacho = Math.Max(0, machoActual),
                MortM = mortM,
                SelM = selM,
                ErrorM = errM,
                ConsKgM = Math.Round(consKgM, 3),
                PesoM = PesoLevanteCalculos.AKilos(pesoM),
                UniformM = unifM > 0 ? unifM : null,
                CvM = cvM > 0 ? cvM : null,

                // ---- Porcentajes recalculados sobre total unificado ----
                PorcMortH = Math.Round(porcMortH, 4),
                ACMortH = acMortH,
                PorcSelH = Math.Round(porcSelH, 4),
                ACSelH = acSelH,
                PorcErrH = Math.Round(porcErrH, 4),
                ACErrH = acErrH,
                MSEH = msEH,
                RetAcH = retAcH,
                PorcRetiroH = Math.Round(porcRetH, 4),

                PorcMortM = Math.Round(porcMortM, 4),
                ACMortM = acMortM,
                PorcSelM = Math.Round(porcSelM, 4),
                ACSelM = acSelM,
                PorcErrM = Math.Round(porcErrM, 4),
                ACErrM = acErrM,
                MSEM = msEM,
                RetAcM = retAcM,
                PorcRetAcM = Math.Round(porcRetM, 4),

                RelMH = Math.Round(relMH, 4),

                // Consumos acumulados
                AcConsH = Math.Round(acConsH, 3),
                ConsAcGrH = Math.Round(consAcGrH, 2),
                GrAveDiaH = Math.Round(grAveDiaH, 2),
                IncrConsH = Math.Round(incrConsH, 2),

                AcConsM = Math.Round(acConsM, 3),
                ConsAcGrM = Math.Round(consAcGrM, 2),
                GrAveDiaM = Math.Round(grAveDiaM, 2),
                IncrConsM = Math.Round(incrConsM, 2),

                // Nutricional
                KcalSemH = Math.Round(kcalSemH, 2),
                KcalSemAcH = Math.Round(acKcalSemH, 2),
                ProtSemH = Math.Round(protSemH, 4),
                ProtSemAcH = Math.Round(acProtSemH, 4),

                // ---- Cruce Genético: campos GUIA desde ProduccionAvicolaRaw (guiaRaw), fuente confiable.
                // Antes peso/unif/grAveDia/mort/retiro salían de guiaGenetica (a veces vacío) → null pese a haber guía.
                PorcMortHGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.MortSemH) : null,
                DifMortH = guiaRaw != null ? Math.Round(porcMortH - ParseGuiaV(guiaRaw.MortSemH), 4) : null,
                RetiroHGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.RetiroAcH) : null,
                ConsAcGrHGUIA = consAcGrHGUIA.HasValue ? Math.Round(consAcGrHGUIA.Value, 2) : null,
                GrAveDiaGUIAH = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.GrAveDiaH) : null,
                IncrConsHGUIA = incrConsHGUIA.HasValue ? Math.Round(incrConsHGUIA.Value, 2) : null,
                PorcDifConsH = consAcGrHGUIA.HasValue && consAcGrHGUIA.Value > 0
                    ? Math.Round((consAcGrH - consAcGrHGUIA.Value) / consAcGrHGUIA.Value * 100, 2) : null,
                PesoHGUIA = guiaRaw != null ? ParseGuiaV(guiaRaw.PesoH) / PesoLevanteCalculos.GramosPorKilo : null,
                PorcDifPesoH = guiaRaw != null
                    ? PesoLevanteCalculos.PorcDiferencia(pesoH, ParseGuiaV(guiaRaw.PesoH), 2)
                    : null,
                UnifHGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.Uniformidad) : null,

                PorcMortMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.MortSemM) : null,
                DifMortM = guiaRaw != null ? Math.Round(porcMortM - ParseGuiaV(guiaRaw.MortSemM), 4) : null,
                RetiroMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.RetiroAcM) : null,
                ConsAcGrMGUIA = consAcGrMGUIA.HasValue ? Math.Round(consAcGrMGUIA.Value, 2) : null,
                GrAveDiaMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.GrAveDiaM) : null,
                IncrConsMGUIA = incrConsMGUIA.HasValue ? Math.Round(incrConsMGUIA.Value, 2) : null,
                DifConsM = consAcGrMGUIA.HasValue
                    ? Math.Round(consAcGrM - consAcGrMGUIA.Value, 2) : null,
                PesoMGUIA = guiaRaw != null ? ParseGuiaV(guiaRaw.PesoM) / PesoLevanteCalculos.GramosPorKilo : null,
                PorcDifPesoM = guiaRaw != null
                    ? PesoLevanteCalculos.PorcDiferencia(pesoM, ParseGuiaV(guiaRaw.PesoM), 2)
                    : null,
                UnifMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.Uniformidad) : null,

                DifConsAcH = consAcGrHGUIA.HasValue
                    ? Math.Round(acConsH - (consAcGrHGUIA.Value * avesHInicialesTotal / 1000.0), 3) : null,
                DifConsAcM = consAcGrMGUIA.HasValue
                    ? Math.Round(acConsM - (consAcGrMGUIA.Value * avesMInicialesTotal / 1000.0), 3) : null,
            });
        }

        return resultado;
    }
}
