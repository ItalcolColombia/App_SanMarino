// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.LevanteCompleto.cs
// Reporte de LEVANTE completo (diario + semanal + info del lote) para un lote base o un LPL puntual.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    public async Task<ReporteTecnicoLevanteCompletoDto> GenerarReporteLevanteCompletoAsync(
        int lotePosturaLevanteId,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        var lpl = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lpl == null)
            throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");

        if (!lpl.FechaEncaset.HasValue)
            throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");

        // Determinar lotes a procesar (consolidado o solo el actual)
        List<LotePosturaLevante> lotesAProcesar;
        var sublotesIncluidos = new List<string>();

        if (consolidarSublotes)
        {
            lotesAProcesar = await ObtenerSublotesLevantePorLoteBaseAsync(lotePosturaLevanteId, ct);
            if (!lotesAProcesar.Any())
            {
                lotesAProcesar = new List<LotePosturaLevante> { lpl };
            }

            // Agregar nombres de sublotes
            foreach (var lote in lotesAProcesar)
            {
                var nombreSublote = ExtraerSublote(lote.LoteNombre) ?? "Sin sublote";
                if (!sublotesIncluidos.Contains(nombreSublote))
                {
                    sublotesIncluidos.Add(nombreSublote);
                }
            }
        }
        else
        {
            lotesAProcesar = new List<LotePosturaLevante> { lpl };
            var sublote = ExtraerSublote(lpl.LoteNombre) ?? "Sin sublote";
            sublotesIncluidos.Add(sublote);
        }

        var infoLote = MapearInformacionLoteFromLPL(lpl);
        infoLote.Sublote = consolidarSublotes ? null : ExtraerSublote(lpl.LoteNombre);

        // Obtener seguimientos consolidados desde tabla unificada
        var todosSeguimientos = new List<SegLevanteParaReporte>();
        foreach (var lote in lotesAProcesar)
        {
            var seguimientosLote = await ObtenerSeguimientosLevantePorLPLAsync(lote.LotePosturaLevanteId ?? 0, ct);
            todosSeguimientos.AddRange(seguimientosLote);
        }

        // Filtrar solo semanas de levante (1-25)
        var seguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();

        // Obtener guía genética del lote (desde produccion_avicola_raw)
        // El lote levante tiene Raza y AnoTablaGenetica que se usan para buscar la guía
        Dictionary<int, Domain.Entities.ProduccionAvicolaRaw> guiasRaw = new();
        Dictionary<int, GuiaGeneticaDto> guiasGenetica = new();
        
        if (!string.IsNullOrWhiteSpace(lpl.Raza) && lpl.AnoTablaGenetica.HasValue)
        {
            try
            {
                var razaNorm = lpl.Raza.Trim().ToLower();
                var ano = lpl.AnoTablaGenetica.Value.ToString();
                
                // Santa Reyes tiene su guia en tabla propia (F2.2). Se pregunta primero; si la
                // empresa no tiene guia propia -Sanmarino, Panama, Ecuador- la lista vuelve vacia y
                // corre la consulta de siempre, sin tocarla.
                var guiasRawList = await GuiaGeneticaLookup.ObtenerFilasPropiasAsync(
                    _ctx, _currentUser.CompanyId, razaNorm, ano, ct);

                if (guiasRawList.Count == 0)
                {
                    // Obtener datos raw directamente para tener acceso a ConsAcH, ConsAcM, etc.
                    guiasRawList = await _ctx.ProduccionAvicolaRaw
                        .AsNoTracking()
                        .Where(p =>
                            p.Raza != null && p.AnioGuia != null &&
                            EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                            p.AnioGuia.Trim() == ano &&
                            p.CompanyId == _currentUser.CompanyId &&
                            p.DeletedAt == null
                        )
                        .ToListAsync(ct);
                }
                
                // Parsear edades y crear diccionario
                foreach (var guia in guiasRawList)
                {
                    var edadStr = guia.Edad;
                    if (int.TryParse(edadStr?.Trim().Replace(",", ".").Split('.')[0], out var edad))
                    {
                        if (edad >= 1 && edad <= 25)
                        {
                            guiasRaw[edad] = guia;
                        }
                    }
                }
                
                // También obtener los DTOs procesados para usar los métodos de parseo
                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(
                    lpl.Raza, 
                    lpl.AnoTablaGenetica.Value, 
                    edadDesde: 1, 
                    edadHasta: 25);
                
                guiasGenetica = guias.ToDictionary(g => g.Edad, g => g);
            }
            catch
            {
                // Si no se encuentra la guía, continuar sin valores GUIA
                // Los valores GUIA quedarán como null
            }
        }

        // Obtener traslados del lote para verificar reducciones (LoteOrigenId = lotes.lote_id si existe)
        var loteIdParaTraslados = lpl.LoteId ?? lotePosturaLevanteId;
        var traslados = await _ctx.Set<Domain.Entities.MovimientoAves>()
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteIdParaTraslados && 
                       m.Estado == "Completado" &&
                       m.DeletedAt == null)
            .OrderBy(m => m.FechaMovimiento)
            .ToListAsync(ct);

        // Helper para parsear valores de la guía raw
        static double ParseGuiaRaw(string? value) => 
            double.TryParse(value?.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;

        // Calcular datos semanales (semanas 1-25)
        var datosSemanales = new List<ReporteTecnicoLevanteSemanalDto>();

        // Sumar aves iniciales: si es consolidado de todos los lotes, si no solo del lote actual
        var hembraIni = consolidarSublotes
            ? lotesAProcesar.Sum(l => l.HembrasL ?? 0)
            : lpl.HembrasL ?? 0;
        var machoIni = consolidarSublotes
            ? lotesAProcesar.Sum(l => l.MachosL ?? 0)
            : lpl.MachosL ?? 0;

        // Variables acumuladas
        int acMortH = 0, acSelH = 0, acErrH = 0;
        int acMortM = 0, acSelM = 0, acErrM = 0;
        int acTrasSalH = 0, acTrasSalM = 0, acTrasIngH = 0, acTrasIngM = 0;
        int acVentaH = 0, acVentaM = 0;
        double acConsH = 0, acConsM = 0;
        double acKcalSemH = 0, acKcalSemM = 0;
        double acProtSemH = 0, acProtSemM = 0;
        double? consAcGrHAnterior = null;
        double? consAcGrMAnterior = null;
        // Variables para calcular incrementos de la guía genética
        double? consAcGrHGUIAAnterior = null;
        double? consAcGrMGUIAAnterior = null;
        double? pesoHGUIAAnterior = null;
        double? pesoMGUIAAnterior = null;

        for (int semana = 1; semana <= 25; semana++)
        {
            // Calcular rango de fechas para la semana
            var fechaInicioSemana = lpl.FechaEncaset!.Value.AddDays((semana - 1) * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            // Obtener registros de esta semana
            var registrosSemana = seguimientos.Where(s =>
            {
                var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, s.FechaRegistro);
                var edadSemanas = CalcularEdadSemanas(edadDias);
                return edadSemanas == semana;
            }).ToList();

            if (!registrosSemana.Any() && semana > 1)
            {
                // Si no hay registros y no es la primera semana, podemos saltarla o crear registro vacío
                // Por ahora, saltamos semanas sin datos
                continue;
            }

            // Calcular valores de la semana
            var mortH = registrosSemana.Sum(s => s.MortalidadHembras);
            var mortM = registrosSemana.Sum(s => s.MortalidadMachos);
            var selH = registrosSemana.Sum(s => Math.Max(0, s.SelH)); // Solo valores positivos
            var selM = registrosSemana.Sum(s => Math.Max(0, s.SelM)); // Solo valores positivos
            var errorH = registrosSemana.Sum(s => s.ErrorSexajeHembras);
            var errorM = registrosSemana.Sum(s => s.ErrorSexajeMachos);
            var consKgH = registrosSemana.Sum(s => s.ConsumoKgHembras);
            var consKgM = registrosSemana.Sum(s => s.ConsumoKgMachos ?? 0);
            var trasSalH = registrosSemana.Sum(s => s.TrasladoSalidaHembras);
            var trasSalM = registrosSemana.Sum(s => s.TrasladoSalidaMachos);
            var trasIngH = registrosSemana.Sum(s => s.TrasladoIngresoHembras);
            var trasIngM = registrosSemana.Sum(s => s.TrasladoIngresoMachos);
            var ventaH = registrosSemana.Sum(s => s.VentaAvesHembras);
            var ventaM = registrosSemana.Sum(s => s.VentaAvesMachos);

            // Calcular traslados de la semana (valores negativos de SelH/SelM)
            var trasladosSemana = registrosSemana.Sum(s =>
                Math.Abs(Math.Min(0, s.SelH)) + Math.Abs(Math.Min(0, s.SelM)));

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acSelH += selH;
            acSelM += selM;
            acErrH += errorH;
            acErrM += errorM;
            acTrasSalH += trasSalH;
            acTrasSalM += trasSalM;
            acTrasIngH += trasIngH;
            acTrasIngM += trasIngM;
            acVentaH += ventaH;
            acVentaM += ventaM;
            acConsH += consKgH;
            acConsM += consKgM;

            // Calcular saldos actuales.
            //
            // ⭐ 2026-08-17: pasa a resolverlo SaldoAvesLevanteCalculos, igual que los otros dos
            // caminos de este mismo service. Antes era `ini − mort − sel − err` a mano, o sea que
            // este endpoint (`/levante/completo/{loteId}`) ignoraba los TRASLADOS y la VENTA y
            // cerraba por encima del maestro y del otro endpoint del mismo reporte
            // (`/levante/tabs/{loteId}`), que sí los descuenta desde hace rato. El piso en 0 lo pone
            // la spec: un histórico mal cuadrado no debe producir aves negativas.
            var hembra = SaldoAvesLevanteCalculos.SaldoFinal(hembraIni,
                new[] { new SaldoAvesLevanteCalculos.MovimientoDia(acMortH, acSelH, acErrH, acTrasSalH, acTrasIngH, acVentaH) });
            var saldoMacho = SaldoAvesLevanteCalculos.SaldoFinal(machoIni,
                new[] { new SaldoAvesLevanteCalculos.MovimientoDia(acMortM, acSelM, acErrM, acTrasSalM, acTrasIngM, acVentaM) });

            // Obtener valores promedio de peso y uniformidad de la semana
            var pesoH = registrosSemana.Where(s => s.PesoPromH.HasValue)
                .Select(s => s.PesoPromH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var pesoM = registrosSemana.Where(s => s.PesoPromM.HasValue)
                .Select(s => s.PesoPromM!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var uniformH = registrosSemana.Where(s => s.UniformidadH.HasValue)
                .Select(s => s.UniformidadH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var uniformM = registrosSemana.Where(s => s.UniformidadM.HasValue)
                .Select(s => s.UniformidadM!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var cvH = registrosSemana.Where(s => s.CvH.HasValue)
                .Select(s => s.CvH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var cvM = registrosSemana.Where(s => s.CvM.HasValue)
                .Select(s => s.CvM!.Value)
                .DefaultIfEmpty(0)
                .Average();

            // Obtener valores nutricionales (promedio de la semana)
            // Nota: La entidad solo tiene KcalAlH y ProtAlH, usamos los mismos valores para machos
            var kcalAlH = registrosSemana.Where(s => s.KcalAlH.HasValue)
                .Select(s => s.KcalAlH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var protAlH = registrosSemana.Where(s => s.ProtAlH.HasValue)
                .Select(s => s.ProtAlH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            // Usar los mismos valores nutricionales de hembras para machos (mismo tipo de alimento)
            var kcalAlM = kcalAlH;
            var protAlM = protAlH;

            // Obtener guía genética para esta semana (desde produccion_avicola_raw)
            var guiaGenetica = guiasGenetica.TryGetValue(semana, out var guia) ? guia : null;
            var guiaRaw = guiasRaw.TryGetValue(semana, out var raw) ? raw : null;

            // Calcular campos según fórmulas Excel
            var dto = new ReporteTecnicoLevanteSemanalDto
            {
                CodGuia = lpl.CodigoGuiaGenetica,
                IdLoteRAP = null,
                Regional = lpl.Regional,
                Granja = lpl.Farm?.Name,
                Lote = lpl.LoteNombre,
                Raza = lpl.Raza,
                AnoG = lpl.AnoTablaGenetica,
                HembraIni = hembraIni,
                MachoIni = machoIni,
                Traslado = null,
                NucleoL = lpl.Nucleo?.NucleoNombre,
                Anon = null,
                Edad = CalcularEdadDias(lpl.FechaEncaset!.Value, fechaInicioSemana),
                Fecha = fechaInicioSemana,
                SemAno = GetSemanaAno(fechaInicioSemana),
                Semana = semana,

                // Datos hembras
                Hembra = hembra,
                MortH = mortH,
                SelH = selH,
                ErrorH = errorH,
                ConsKgH = consKgH,
                // El seguimiento guarda gramos y la columna del reporte es «kg Real», al lado de
                // la guía en kg — ver PesoLevanteCalculos.
                PesoH = PesoLevanteCalculos.AKilos(pesoH),
                UniformH = uniformH > 0 ? uniformH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,

                // Datos machos
                SaldoMacho = saldoMacho,
                MortM = mortM,
                SelM = selM,
                ErrorM = errorM,
                ConsKgM = consKgM,
                PesoM = PesoLevanteCalculos.AKilos(pesoM),
                UniformM = uniformM > 0 ? uniformM : null,
                CvM = cvM > 0 ? cvM : null,
                KcalAlM = kcalAlM > 0 ? kcalAlM : null,
                ProtAlM = protAlM > 0 ? protAlM : null,

                // Cálculos de eficiencia
                KcalAveH = hembra > 0 && kcalAlH > 0 ? (kcalAlH * consKgH) / hembra : null,
                ProtAveH = hembra > 0 && protAlH > 0 ? (protAlH * consKgH) / hembra : null,
                KcalAveM = saldoMacho > 0 && kcalAlM > 0 ? (kcalAlM * consKgM) / saldoMacho : null,
                ProtAveM = saldoMacho > 0 && protAlM > 0 ? (protAlM * consKgM) / saldoMacho : null,

                RelMH = hembra > 0 ? (saldoMacho / (double)hembra * 100) : null,
                PorcMortH = hembraIni > 0 ? (mortH / (double)hembraIni * 100) : null,
                PorcMortHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.MortSemH) : null,
                DifMortH = guiaRaw != null && hembraIni > 0
                    ? (mortH / (double)hembraIni * 100) - ParseGuiaRaw(guiaRaw.MortSemH)
                    : null,
                ACMortH = acMortH,

                PorcSelH = hembraIni > 0 ? (selH / (double)hembraIni * 100) : null,
                ACSelH = acSelH,
                PorcErrH = hembraIni > 0 ? (errorH / (double)hembraIni * 100) : null,
                ACErrH = acErrH,

                MSEH = mortH + selH + errorH,
                RetAcH = acMortH + acSelH + acErrH,
                PorcRetiroH = hembraIni > 0 ? ((acMortH + acSelH + acErrH) / (double)hembraIni * 100) : null,
                RetiroHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.RetiroAcH) : null,

                AcConsH = acConsH,
                ConsAcGrH = hembraIni > 0 ? (acConsH * 1000) / hembraIni : null,
                ConsAcGrHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcH) : null, // ConsAcH de la guía (consumo acumulado en gramos)
                GrAveDiaH = hembra > 0 ? (consKgH * 1000) / hembra / 7 : null,
                GrAveDiaGUIAH = guiaRaw != null ? ParseGuiaRaw(guiaRaw.GrAveDiaH) : null, // GrAveDiaH de la guía (g/ave/día)
                IncrConsH = consAcGrHAnterior.HasValue 
                    ? ((acConsH * 1000) / hembraIni) - consAcGrHAnterior.Value 
                    : null,
                IncrConsHGUIA = consAcGrHGUIAAnterior.HasValue && guiaRaw != null
                    ? ParseGuiaRaw(guiaRaw.ConsAcH) - consAcGrHGUIAAnterior.Value
                    : (semana == 1 && guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcH) : null), // Primera semana: el valor es el incremento inicial
                PorcDifConsH = guiaRaw != null && ParseGuiaRaw(guiaRaw.ConsAcH) > 0
                    ? (((acConsH * 1000) / hembraIni) - ParseGuiaRaw(guiaRaw.ConsAcH)) / ParseGuiaRaw(guiaRaw.ConsAcH) * 100
                    : null,

                // Poblar desde guiaRaw (ProduccionAvicolaRaw) que resuelve confiablemente por raza+año+edad,
                // igual que ConsAcGrHGUIA; guiaGenetica (ObtenerGuiaGeneticaRangoAsync) a veces viene vacío
                // y dejaba PesoHGUIA/UnifHGUIA en null pese a haber guía.
                PesoHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.PesoH) / PesoLevanteCalculos.GramosPorKilo : null, // guía peso_h (g) → kg
                PorcDifPesoH = guiaRaw != null
                    ? PesoLevanteCalculos.PorcDiferencia(pesoH, ParseGuiaRaw(guiaRaw.PesoH))
                    : null,
                UnifHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.Uniformidad) : null,

                PorcMortM = machoIni > 0 ? (mortM / (double)machoIni * 100) : null,
                PorcMortMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.MortSemM) : null,
                DifMortM = guiaRaw != null && machoIni > 0
                    ? (mortM / (double)machoIni * 100) - ParseGuiaRaw(guiaRaw.MortSemM)
                    : null,
                ACMortM = acMortM,

                PorcSelM = machoIni > 0 ? (selM / (double)machoIni * 100) : null,
                ACSelM = acSelM,
                PorcErrM = machoIni > 0 ? (errorM / (double)machoIni * 100) : null,
                ACErrM = acErrM,

                MSEM = mortM + selM + errorM,
                RetAcM = acMortM + acSelM + acErrM,
                PorcRetAcM = machoIni > 0 ? ((acMortM + acSelM + acErrM) / (double)machoIni * 100) : null,
                RetiroMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.RetiroAcM) : null,

                AcConsM = acConsM,
                ConsAcGrM = machoIni > 0 ? (acConsM * 1000) / machoIni : null,
                ConsAcGrMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcM) : null, // ConsAcM de la guía (consumo acumulado en gramos)
                GrAveDiaM = saldoMacho > 0 ? (consKgM * 1000) / saldoMacho / 7 : null,
                GrAveDiaMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.GrAveDiaM) : null, // GrAveDiaM de la guía (g/ave/día)
                IncrConsM = consAcGrMAnterior.HasValue
                    ? ((acConsM * 1000) / machoIni) - consAcGrMAnterior.Value
                    : null,
                IncrConsMGUIA = consAcGrMGUIAAnterior.HasValue && guiaRaw != null
                    ? ParseGuiaRaw(guiaRaw.ConsAcM) - consAcGrMGUIAAnterior.Value
                    : (semana == 1 && guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcM) : null), // Primera semana: el valor es el incremento inicial
                DifConsM = guiaRaw != null
                    ? ((acConsM * 1000) / machoIni) - ParseGuiaRaw(guiaRaw.ConsAcM)
                    : null,

                PesoMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.PesoM) / PesoLevanteCalculos.GramosPorKilo : null, // guía peso_m (g) → kg
                PorcDifPesoM = guiaRaw != null
                    ? PesoLevanteCalculos.PorcDiferencia(pesoM, ParseGuiaRaw(guiaRaw.PesoM))
                    : null,
                UnifMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.Uniformidad) : null,

                ErrSexAcH = null, // No está en la guía genética, se puede agregar manualmente si es necesario
                PorcErrSxAcH = null,
                ErrSexAcM = null, // No está en la guía genética, se puede agregar manualmente si es necesario
                PorcErrSxAcM = null,

                DifConsAcH = guiaRaw != null
                    ? acConsH - (ParseGuiaRaw(guiaRaw.ConsAcH) * hembraIni / 1000)
                    : null,
                DifConsAcM = guiaRaw != null
                    ? acConsM - (ParseGuiaRaw(guiaRaw.ConsAcM) * machoIni / 1000)
                    : null,

                // Datos nutricionales
                // Nota: Los valores nutricionales (Kcal, Prot) no están en la guía genética estándar
                // Se pueden agregar manualmente o desde otra fuente si es necesario
                AlimHGUIA = null, // Tipo de alimento (se puede obtener del seguimiento)
                KcalSemH = kcalAlH > 0 ? kcalAlH * consKgH : null,
                KcalSemAcH = acKcalSemH + (kcalAlH > 0 ? kcalAlH * consKgH : 0),
                KcalSemHGUIA = null, // No disponible en guía genética estándar
                KcalSemAcHGUIA = null,
                ProtSemH = protAlH > 0 ? (protAlH / 100) * consKgH : null,
                ProtSemAcH = acProtSemH + (protAlH > 0 ? (protAlH / 100) * consKgH : 0),
                ProtSemHGUIA = null, // No disponible en guía genética estándar
                ProtSemAcHGUIA = null,

                AlimMGUIA = null, // Tipo de alimento (se puede obtener del seguimiento)
                KcalSemM = kcalAlM > 0 ? kcalAlM * consKgM : null,
                KcalSemAcM = acKcalSemM + (kcalAlM > 0 ? kcalAlM * consKgM : 0),
                KcalSemMGUIA = null, // No disponible en guía genética estándar
                KcalSemAcMGUIA = null,
                ProtSemM = protAlM > 0 ? (protAlM / 100) * consKgM : null,
                ProtSemAcM = acProtSemM + (protAlM > 0 ? (protAlM / 100) * consKgM : 0),
                ProtSemMGUIA = null, // No disponible en guía genética estándar
                ProtSemAcMGUIA = null,

                Observaciones = string.Join("; ", registrosSemana
                    .Where(s => !string.IsNullOrEmpty(s.Observaciones))
                    .Select(s => s.Observaciones)
                    .Distinct())
            };

            // Actualizar acumulados nutricionales
            if (dto.KcalSemH.HasValue)
                acKcalSemH += dto.KcalSemH.Value;
            if (dto.KcalSemM.HasValue)
                acKcalSemM += dto.KcalSemM.Value;
            if (dto.ProtSemH.HasValue)
                acProtSemH += dto.ProtSemH.Value;
            if (dto.ProtSemM.HasValue)
                acProtSemM += dto.ProtSemM.Value;

            // Actualizar valores anteriores para siguiente semana
            if (dto.ConsAcGrH.HasValue)
                consAcGrHAnterior = dto.ConsAcGrH.Value;
            if (dto.ConsAcGrM.HasValue)
                consAcGrMAnterior = dto.ConsAcGrM.Value;
            
            // Actualizar valores anteriores de la guía genética para calcular incrementos
            if (dto.ConsAcGrHGUIA.HasValue)
                consAcGrHGUIAAnterior = dto.ConsAcGrHGUIA.Value;
            if (dto.ConsAcGrMGUIA.HasValue)
                consAcGrMGUIAAnterior = dto.ConsAcGrMGUIA.Value;
            if (dto.PesoHGUIA.HasValue)
                pesoHGUIAAnterior = dto.PesoHGUIA.Value;
            if (dto.PesoMGUIA.HasValue)
                pesoMGUIAAnterior = dto.PesoMGUIA.Value;

            datosSemanales.Add(dto);
        }

        return new ReporteTecnicoLevanteCompletoDto
        {
            InformacionLote = infoLote,
            DatosSemanales = datosSemanales,
            EsConsolidado = consolidarSublotes,
            SublotesIncluidos = sublotesIncluidos
        };
    }
}
