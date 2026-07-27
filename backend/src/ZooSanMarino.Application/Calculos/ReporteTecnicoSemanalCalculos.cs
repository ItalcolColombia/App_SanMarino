using System.Globalization;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA del "Reporte Técnico Semanal" (Sanmarino postura, sin EF):
///   * parseo tolerante de la guía genética (texto: '', '-', coma decimal),
///   * derivación de las columnas del Excel por semana (base FIJA de aves
///     iniciales para %, acumulados de bajas/kg, incrementos, nutrición),
///   * consolidación multi-galpón (sumas de conteos/kg/huevos; promedio simple
///     de pesos/uniformidad, igual que las hojas "Gral" del Excel).
/// </summary>
public static class ReporteTecnicoSemanalCalculos
{
    // ─────────────────────────────────────────────────────────────────────────
    // Parseo guía (texto → double?): '' / '-' / no numérico ⇒ null; coma ⇒ punto.
    // ─────────────────────────────────────────────────────────────────────────
    public static double? ParseGuia(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim().Replace(',', '.');
        if (limpio == "-") return null;
        return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    /// <summary>Edad de la guía → semana entera (== fn_parse_edad_numerica): entero directo o primer grupo de dígitos.</summary>
    public static int? ParseEdadSemana(string? edad)
    {
        if (string.IsNullOrWhiteSpace(edad)) return null;
        var limpio = edad.Trim().Replace(',', '.');
        if (int.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entero))
            return entero;
        var digitos = new string(limpio.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return digitos.Length > 0 && int.TryParse(digitos, out var n) ? n : (int?)null;
    }

    /// <summary>num/den*100; null si el denominador no es positivo.</summary>
    public static double? Pct(double num, double den) => den > 0 ? num / den * 100.0 : null;

    /// <summary>num/den; null si el denominador no es positivo.</summary>
    public static double? Div(double num, double den) => den > 0 ? num / den : null;

    /// <summary>% desviación = real/guía*100-100; null si falta alguno o guía=0.</summary>
    public static double? DesvPct(double? real, double? guia) =>
        real.HasValue && guia.HasValue && guia.Value != 0
            ? real.Value / guia.Value * 100.0 - 100.0
            : null;

    // ─────────────────────────────────────────────────────────────────────────
    // Valores de guía por semana ya parseados (los arma el service desde
    // guia_genetica_sanmarino_colombia con ParseGuia/ParseEdadSemana).
    // ─────────────────────────────────────────────────────────────────────────
    public sealed record GuiaSemanaLevante(
        double? MortSemHembras,
        double? MortSemMachos,
        double? RetiroAcHembras,
        double? RetiroAcMachos,
        double? GrAveDiaHembras,
        double? GrAveDiaMachos,
        double? ConsAcHembras,
        double? ConsAcMachos,
        double? PesoHembras,
        double? PesoMachos,
        double? Uniformidad);

    /// <summary>
    /// Huevos incubables ENVIADOS a planta/incubadora agrupados por semana de vida
    /// ("HI Cargado" del Excel). Misma fórmula de semana que fn_indicadores_produccion_postura
    /// (división entera de días entre 7, +1) para que case 1:1 con las filas del reporte.
    /// El service pasa solo traslados Completado con destino Planta (limpio + tratado).
    /// </summary>
    public static Dictionary<int, int> AgruparCargadosPorSemana(
        IEnumerable<(DateTime Fecha, int Cantidad)> traslados,
        DateTime fechaEncaset)
    {
        var resultado = new Dictionary<int, int>();
        var encaset = fechaEncaset.Date;
        foreach (var (fecha, cantidad) in traslados)
        {
            var dias = (fecha.Date - encaset).Days;
            if (dias < 0) continue;               // traslado anterior al encaset: dato inconsistente, se ignora
            var semana = dias / 7 + 1;
            resultado[semana] = resultado.TryGetValue(semana, out var acum) ? acum + cantidad : cantidad;
        }
        return resultado;
    }

    public sealed record GuiaSemanaProduccion(
        double? AprovSem,
        double? AprovAc,
        double? MasaHuevo,
        double? GrHuevoInc,
        double? Apareo,
        double? NacimPorcentaje,
        double? PollitoAa,
        double? MortSemHembras,
        double? MortSemMachos);

    // ─────────────────────────────────────────────────────────────────────────
    // LEVANTE — filas del Excel a partir de la fn de extras + guía.
    // % con base FIJA de aves iniciales (Excel: F7 = E7/$C$7), acumulados =
    // conteos acumulados / base * 100, nutrición acumulada por ave
    // (Excel: AH = kcal*0.001*(gr_ave_semana) + AH_prev).
    // ─────────────────────────────────────────────────────────────────────────
    public static List<ReporteSemanalLevanteSemanaDto> ConstruirSemanasLevante(
        IReadOnlyList<ReporteSemanalLevanteExtrasRow> filas,
        IReadOnlyDictionary<int, GuiaSemanaLevante> guia)
    {
        var resultado = new List<ReporteSemanalLevanteSemanaDto>(filas.Count);

        double cumMortH = 0, cumMortM = 0, cumSelH = 0, cumSelM = 0, cumErrH = 0, cumErrM = 0;
        double cumKgH = 0, cumKgM = 0;
        double kcalAcum = 0, protAcum = 0;
        double? grAveDiaHPrev = null, grAveDiaMPrev = null;
        double? grAveDiaHGuiaPrev = null, grAveDiaMGuiaPrev = null;
        double? pesoHPrev = null, pesoMPrev = null;

        foreach (var f in filas.OrderBy(x => x.Semana))
        {
            guia.TryGetValue(f.Semana, out var g);

            cumMortH += f.MortalidadHembrasSem; cumMortM += f.MortalidadMachosSem;
            cumSelH += f.SeleccionHembrasSem;   cumSelM += f.SeleccionMachosSem;
            cumErrH += f.ErrorHembrasSem;       cumErrM += f.ErrorMachosSem;
            cumKgH += f.ConsumoKgHembrasSem;    cumKgM += f.ConsumoKgMachosSem;

            var dias = f.DiasConRegistro > 0 ? f.DiasConRegistro : 7;
            var grAveDiaH = f.AvesHembrasFin > 0 ? f.ConsumoKgHembrasSem * 1000.0 / (f.AvesHembrasFin * dias) : (double?)null;
            var grAveDiaM = f.AvesMachosFin > 0 ? f.ConsumoKgMachosSem * 1000.0 / (f.AvesMachosFin * dias) : (double?)null;

            // Nutrición acumulada por ave (Excel AH/AI): kcal_alimento*0.001 * gramos/ave de la semana.
            var grAveSemanaH = f.AvesHembrasFin > 0 ? f.ConsumoKgHembrasSem * 1000.0 / f.AvesHembrasFin : 0;
            if (f.KcalAlimentoHembras is > 0) kcalAcum += f.KcalAlimentoHembras.Value * 0.001 * grAveSemanaH;
            if (f.ProtAlimentoHembras is > 0) protAcum += f.ProtAlimentoHembras.Value * 0.01 * grAveSemanaH;

            var dto = new ReporteSemanalLevanteSemanaDto
            {
                Semana = f.Semana,
                FechaFinSemana = f.FechaFinSemana,
                DiasConRegistro = f.DiasConRegistro,
                AvesHembrasFin = f.AvesHembrasFin,
                AvesMachosFin = f.AvesMachosFin,
                RelacionMachosHembrasPct = Pct(f.AvesMachosFin, f.AvesHembrasFin),

                MortalidadHembras = f.MortalidadHembrasSem,
                MortalidadHembrasPct = Pct(f.MortalidadHembrasSem, f.BaseHembras),
                MortalidadHembrasAcumPct = Pct(cumMortH, f.BaseHembras),
                SeleccionHembras = f.SeleccionHembrasSem,
                SeleccionHembrasPct = Pct(f.SeleccionHembrasSem, f.BaseHembras),
                SeleccionHembrasAcumPct = Pct(cumSelH, f.BaseHembras),
                MortSelHembrasGuiaPct = g?.MortSemHembras,
                ErrorHembras = f.ErrorHembrasSem,
                ErrorHembrasPct = Pct(f.ErrorHembrasSem, f.BaseHembras),
                ErrorHembrasAcumPct = Pct(cumErrH, f.BaseHembras),
                RetiroAcumHembrasPct = Pct(cumMortH + cumSelH + cumErrH, f.BaseHembras),
                RetiroAcumHembrasGuiaPct = g?.RetiroAcHembras,

                ConsumoKgHembras = f.ConsumoKgHembrasSem,
                ConsumoKgHembrasAcum = cumKgH,
                GrAveDiaHembras = grAveDiaH,
                GrAveDiaHembrasGuia = g?.GrAveDiaHembras,
                IncrementoGrAveDiaHembras = grAveDiaH.HasValue && grAveDiaHPrev.HasValue ? grAveDiaH - grAveDiaHPrev : grAveDiaH,
                IncrementoGrAveDiaHembrasGuia = g?.GrAveDiaHembras is not null && grAveDiaHGuiaPrev.HasValue
                    ? g.GrAveDiaHembras - grAveDiaHGuiaPrev
                    : g?.GrAveDiaHembras,
                ConsumoAcumGrAveHembras = f.AvesHembrasFin > 0 ? cumKgH * 1000.0 / f.AvesHembrasFin : null,
                ConsumoAcumGrAveHembrasGuia = g?.ConsAcHembras,

                PesoHembras = f.PesoHembrasSem,
                PesoHembrasGuia = g?.PesoHembras,
                GananciaHembras = f.PesoHembrasSem.HasValue && pesoHPrev.HasValue ? f.PesoHembrasSem - pesoHPrev : f.PesoHembrasSem,
                DesviacionPesoHembrasPct = DesvPct(f.PesoHembrasSem, g?.PesoHembras),

                UniformidadHembras = f.UniformidadHembras,
                UniformidadGuia = g?.Uniformidad,
                CvHembras = f.CvHembras,

                KcalAlimentoHembras = f.KcalAlimentoHembras,
                ProtAlimentoHembras = f.ProtAlimentoHembras,
                KcalAveAcumHembras = kcalAcum > 0 ? kcalAcum : null,
                ProtAveAcumHembras = protAcum > 0 ? protAcum : null,

                MortalidadMachos = f.MortalidadMachosSem,
                MortalidadMachosPct = Pct(f.MortalidadMachosSem, f.BaseMachos),
                MortalidadMachosAcumPct = Pct(cumMortM, f.BaseMachos),
                SeleccionMachos = f.SeleccionMachosSem,
                SeleccionMachosPct = Pct(f.SeleccionMachosSem, f.BaseMachos),
                SeleccionMachosAcumPct = Pct(cumSelM, f.BaseMachos),
                MortSelMachosGuiaPct = g?.MortSemMachos,
                ErrorMachos = f.ErrorMachosSem,
                ErrorMachosPct = Pct(f.ErrorMachosSem, f.BaseMachos),
                ErrorMachosAcumPct = Pct(cumErrM, f.BaseMachos),
                RetiroAcumMachosPct = Pct(cumMortM + cumSelM + cumErrM, f.BaseMachos),
                RetiroAcumMachosGuiaPct = g?.RetiroAcMachos,

                ConsumoKgMachos = f.ConsumoKgMachosSem,
                ConsumoKgMachosAcum = cumKgM,
                GrAveDiaMachos = grAveDiaM,
                GrAveDiaMachosGuia = g?.GrAveDiaMachos,
                IncrementoGrAveDiaMachos = grAveDiaM.HasValue && grAveDiaMPrev.HasValue ? grAveDiaM - grAveDiaMPrev : grAveDiaM,
                IncrementoGrAveDiaMachosGuia = g?.GrAveDiaMachos is not null && grAveDiaMGuiaPrev.HasValue
                    ? g.GrAveDiaMachos - grAveDiaMGuiaPrev
                    : g?.GrAveDiaMachos,
                ConsumoAcumGrAveMachos = f.AvesMachosFin > 0 ? cumKgM * 1000.0 / f.AvesMachosFin : null,
                ConsumoAcumGrAveMachosGuia = g?.ConsAcMachos,

                PesoMachos = f.PesoMachosSem,
                PesoMachosGuia = g?.PesoMachos,
                GananciaMachos = f.PesoMachosSem.HasValue && pesoMPrev.HasValue ? f.PesoMachosSem - pesoMPrev : f.PesoMachosSem,
                DesviacionPesoMachosPct = DesvPct(f.PesoMachosSem, g?.PesoMachos)
            };

            resultado.Add(dto);

            if (grAveDiaH.HasValue) grAveDiaHPrev = grAveDiaH;
            if (grAveDiaM.HasValue) grAveDiaMPrev = grAveDiaM;
            if (g?.GrAveDiaHembras is not null) grAveDiaHGuiaPrev = g.GrAveDiaHembras;
            if (g?.GrAveDiaMachos is not null) grAveDiaMGuiaPrev = g.GrAveDiaMachos;
            if (f.PesoHembrasSem.HasValue) pesoHPrev = f.PesoHembrasSem;
            if (f.PesoMachosSem.HasValue) pesoMPrev = f.PesoMachosSem;
        }

        return resultado;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRODUCCIÓN — filas del Excel a partir de la fn base de indicadores + guía
    // extra. Los % de la fn base se conservan tal cual (misma aritmética que la
    // pantalla de indicadores); aquí solo se derivan acumulados y columnas que
    // la fn no emite (masa, conversión, apareo, %HI, gr/a/d reales).
    // ─────────────────────────────────────────────────────────────────────────
    public static List<ReporteSemanalProduccionSemanaDto> ConstruirSemanasProduccion(
        IReadOnlyList<IndicadorProduccionSemanalBdRow> filas,
        IReadOnlyDictionary<int, GuiaSemanaProduccion> guia,
        IReadOnlyDictionary<int, int>? cargadosPorSemana = null)
    {
        var resultado = new List<ReporteSemanalProduccionSemanaDto>(filas.Count);

        long cumHuevosTot = 0, cumHuevosInc = 0, cumCargados = 0;
        double cumKgH = 0, cumKgM = 0;
        double cumMortSelH = 0, cumMortSelM = 0;
        double baseH = 0, baseM = 0;               // aves iniciales (reconstruidas de la 1ª semana)
        double? mortGuiaHAcum = null, mortGuiaMAcum = null;
        double? grAveDiaHPrev = null;
        var primera = true;

        foreach (var f in filas.OrderBy(x => x.Semana))
        {
            guia.TryGetValue(f.Semana, out var g);

            // Saldo real de inicio de la semana = fin + mort + sel (el "inicio" emitido por la fn
            // está sobrecontado a propósito; ver spec fn_indicadores_produccion_postura).
            var iniH = f.AvesHembrasFinSemana + f.MortalidadHembras + f.SeleccionHembras;
            var iniM = f.AvesMachosFinSemana + f.MortalidadMachos;
            if (primera) { baseH = iniH; baseM = iniM; primera = false; }

            var cargados = cargadosPorSemana is not null && cargadosPorSemana.TryGetValue(f.Semana, out var c) ? c : 0;
            cumCargados += cargados;

            cumHuevosTot += f.HuevosTotales;
            cumHuevosInc += f.HuevosIncubables;
            cumKgH += f.ConsumoKgHembras;
            cumKgM += f.ConsumoKgMachos;
            cumMortSelH += f.MortalidadHembras + f.SeleccionHembras;
            // Machos sin selección en la fn de producción (desviación preservada de la spec).
            cumMortSelM += f.MortalidadMachos;

            if (g?.MortSemHembras is not null) mortGuiaHAcum = (mortGuiaHAcum ?? 0) + g.MortSemHembras;
            if (g?.MortSemMachos is not null) mortGuiaMAcum = (mortGuiaMAcum ?? 0) + g.MortSemMachos;

            var dias = f.TotalRegistros > 0 ? f.TotalRegistros : 7;
            var grAveDiaH = iniH > 0 ? f.ConsumoKgHembras * 1000.0 / (dias * iniH) : (double?)null;
            var grAveDiaM = iniM > 0 ? f.ConsumoKgMachos * 1000.0 / (dias * iniM) : (double?)null;

            var pctIncSem = f.HuevosTotales > 0 ? f.HuevosIncubables * 100.0 / f.HuevosTotales : (double?)null;
            var pctIncAcum = cumHuevosTot > 0 ? cumHuevosInc * 100.0 / cumHuevosTot : (double?)null;

            var dto = new ReporteSemanalProduccionSemanaDto
            {
                Semana = f.Semana,
                FechaInicioSemana = f.FechaInicioSemana,
                FechaFinSemana = f.FechaFinSemana,
                DiasConRegistro = f.TotalRegistros,

                AvesHembrasFin = f.AvesHembrasFinSemana,
                AvesMachosFin = f.AvesMachosFinSemana,
                ApareoPct = Pct(f.AvesMachosFinSemana, f.AvesHembrasFinSemana),
                ApareoGuiaPct = g?.Apareo,

                MortalidadHembras = f.MortalidadHembras,
                SeleccionHembras = f.SeleccionHembras,
                MortalidadHembrasPct = f.PorcentajeMortalidadHembras,
                MortalidadHembrasGuiaPct = f.MortalidadGuiaHembras,
                MortalidadHembrasAcumGuiaPct = mortGuiaHAcum,
                MortSelHembrasAcumPct = Pct(cumMortSelH, baseH),
                RetiroAcumHembrasGuiaPct = f.RetiroAcHGuia,

                MortalidadMachos = f.MortalidadMachos,
                SeleccionMachos = 0,
                MortalidadMachosPct = f.PorcentajeMortalidadMachos,
                MortalidadMachosGuiaPct = f.MortalidadGuiaMachos,
                MortSelMachosAcumPct = Pct(cumMortSelM, baseM),
                RetiroAcumMachosGuiaPct = f.RetiroAcMGuia,

                HuevosTotales = f.HuevosTotales,
                HuevosTotalesAcum = cumHuevosTot,
                Htaa = f.HtaaReal,
                HtaaGuia = f.HuevosTotalesGuia,
                PorcentajeProduccion = f.EficienciaProduccion,
                PorcentajeProduccionGuia = f.PorcentajeProduccionGuia,

                HuevosIncubables = f.HuevosIncubables,
                HuevosIncubablesAcum = cumHuevosInc,
                PorcentajeIncubables = pctIncSem,
                PorcentajeIncubablesGuia = g?.AprovSem,
                PorcentajeIncubablesAcum = pctIncAcum,
                PorcentajeIncubablesAcumGuia = g?.AprovAc,
                Hiaa = f.HiaaReal,
                HiaaGuia = f.HuevosIncubablesGuia,

                ConsumoKgHembras = f.ConsumoKgHembras,
                ConsumoKgHembrasAcum = cumKgH,
                GrAveDiaHembras = grAveDiaH,
                GrAveDiaHembrasGuia = f.ConsumoGuiaHembras,
                IncrementoGrAveDiaHembras = grAveDiaH.HasValue && grAveDiaHPrev.HasValue ? grAveDiaH - grAveDiaHPrev : grAveDiaH,

                ConsumoKgMachos = f.ConsumoKgMachos,
                ConsumoKgMachosAcum = cumKgM,
                GrAveDiaMachos = grAveDiaM,
                GrAveDiaMachosGuia = f.ConsumoGuiaMachos,

                ConversionGrHuevoInc = f.HuevosIncubables > 0
                    ? (f.ConsumoKgHembras + f.ConsumoKgMachos) * 1000.0 / f.HuevosIncubables
                    : null,
                ConversionGrHuevoIncGuia = g?.GrHuevoInc,

                PesoHuevo = f.PesoHuevoPromedio,
                PesoHuevoGuia = f.PesoHuevoGuia,
                // Masa lote (Excel AQ) = %producción/100 * peso huevo real.
                MasaHuevoLote = f.PesoHuevoPromedio.HasValue
                    ? f.EficienciaProduccion / 100.0 * f.PesoHuevoPromedio.Value
                    : null,
                MasaHuevoGuia = g?.MasaHuevo,

                // La fn base normaliza pesos a kg; el Excel los muestra en gramos.
                PesoHembras = f.PesoPromedioHembras * 1000.0,
                PesoHembrasGuia = f.PesoGuiaHembras * 1000.0,
                DesviacionPesoHembrasPct = DesvPct(f.PesoPromedioHembras, f.PesoGuiaHembras),
                PesoMachos = f.PesoPromedioMachos * 1000.0,
                PesoMachosGuia = f.PesoGuiaMachos * 1000.0,
                DesviacionPesoMachosPct = DesvPct(f.PesoPromedioMachos, f.PesoGuiaMachos),

                Uniformidad = f.UniformidadPromedio,
                UniformidadGuia = f.UniformidadGuia,
                CoeficienteVariacion = f.CoeficienteVariacionPromedio,

                HuevosCargadosPlanta = cargados,
                HuevosCargadosPlantaAcum = cumCargados,
                PorcentajeCargaSobreIncubables = Pct(cargados, f.HuevosIncubables),
                NacimientoGuiaPct = g?.NacimPorcentaje,
                PollitosAveGuia = g?.PollitoAa
            };

            resultado.Add(dto);

            if (grAveDiaH.HasValue) grAveDiaHPrev = grAveDiaH;
        }

        return resultado;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONSOLIDADO LEVANTE — como las hojas "Gral" del Excel: conteos/kg = suma,
    // % recomputados sobre las sumas (base = Σ bases), pesos/uniformidad/CV =
    // promedio simple entre galpones con dato, guía = primer valor no nulo.
    // ─────────────────────────────────────────────────────────────────────────
    public static ReporteSemanalLevanteTabDto ConsolidarLevante(IReadOnlyList<ReporteSemanalLevanteTabDto> tabs)
    {
        var header = new ReporteSemanalTabHeaderDto
        {
            LoteNombre = "Consolidado",
            EsConsolidado = true,
            BaseHembras = tabs.Sum(t => t.Header.BaseHembras),
            BaseMachos = tabs.Sum(t => t.Header.BaseMachos),
            Raza = tabs.Select(t => t.Header.Raza).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
            AnioGuia = tabs.Select(t => t.Header.AnioGuia).FirstOrDefault(a => a.HasValue),
            FechaEncaset = tabs.Select(t => t.Header.FechaEncaset).Where(x => x.HasValue).Min()
        };

        var porSemana = tabs
            .SelectMany(t => t.Semanas)
            .GroupBy(s => s.Semana)
            .OrderBy(g => g.Key)
            .ToList();

        var semanas = new List<ReporteSemanalLevanteSemanaDto>(porSemana.Count);
        double cumMortH = 0, cumMortM = 0, cumSelH = 0, cumSelM = 0, cumErrH = 0, cumErrM = 0;
        double cumKgH = 0, cumKgM = 0;
        double? pesoHPrev = null, pesoMPrev = null;
        double? grAveDiaHPrev = null, grAveDiaMPrev = null;

        foreach (var grupo in porSemana)
        {
            var filas = grupo.ToList();
            var baseH = header.BaseHembras;
            var baseM = header.BaseMachos;
            var avesHFin = filas.Sum(s => s.AvesHembrasFin);
            var avesMFin = filas.Sum(s => s.AvesMachosFin);
            var mortH = filas.Sum(s => s.MortalidadHembras);
            var mortM = filas.Sum(s => s.MortalidadMachos);
            var selH = filas.Sum(s => s.SeleccionHembras);
            var selM = filas.Sum(s => s.SeleccionMachos);
            var errH = filas.Sum(s => s.ErrorHembras);
            var errM = filas.Sum(s => s.ErrorMachos);
            var kgH = filas.Sum(s => s.ConsumoKgHembras);
            var kgM = filas.Sum(s => s.ConsumoKgMachos);
            var dias = filas.Max(s => s.DiasConRegistro);
            var diasCalc = dias > 0 ? dias : 7;

            cumMortH += mortH; cumMortM += mortM;
            cumSelH += selH; cumSelM += selM;
            cumErrH += errH; cumErrM += errM;
            cumKgH += kgH; cumKgM += kgM;

            var grAveDiaH = avesHFin > 0 ? kgH * 1000.0 / (avesHFin * diasCalc) : (double?)null;
            var grAveDiaM = avesMFin > 0 ? kgM * 1000.0 / (avesMFin * diasCalc) : (double?)null;
            var pesoH = Prom(filas.Select(s => s.PesoHembras));
            var pesoM = Prom(filas.Select(s => s.PesoMachos));

            var dto = new ReporteSemanalLevanteSemanaDto
            {
                Semana = grupo.Key,
                FechaFinSemana = filas.Select(s => s.FechaFinSemana).Where(x => x.HasValue).Max(),
                DiasConRegistro = dias,
                AvesHembrasFin = avesHFin,
                AvesMachosFin = avesMFin,
                RelacionMachosHembrasPct = Pct(avesMFin, avesHFin),

                MortalidadHembras = mortH,
                MortalidadHembrasPct = Pct(mortH, baseH),
                MortalidadHembrasAcumPct = Pct(cumMortH, baseH),
                SeleccionHembras = selH,
                SeleccionHembrasPct = Pct(selH, baseH),
                SeleccionHembrasAcumPct = Pct(cumSelH, baseH),
                MortSelHembrasGuiaPct = PrimeraGuia(filas.Select(s => s.MortSelHembrasGuiaPct)),
                ErrorHembras = errH,
                ErrorHembrasPct = Pct(errH, baseH),
                ErrorHembrasAcumPct = Pct(cumErrH, baseH),
                RetiroAcumHembrasPct = Pct(cumMortH + cumSelH + cumErrH, baseH),
                RetiroAcumHembrasGuiaPct = PrimeraGuia(filas.Select(s => s.RetiroAcumHembrasGuiaPct)),

                ConsumoKgHembras = kgH,
                ConsumoKgHembrasAcum = cumKgH,
                GrAveDiaHembras = grAveDiaH,
                GrAveDiaHembrasGuia = PrimeraGuia(filas.Select(s => s.GrAveDiaHembrasGuia)),
                IncrementoGrAveDiaHembras = grAveDiaH.HasValue && grAveDiaHPrev.HasValue ? grAveDiaH - grAveDiaHPrev : grAveDiaH,
                IncrementoGrAveDiaHembrasGuia = PrimeraGuia(filas.Select(s => s.IncrementoGrAveDiaHembrasGuia)),
                ConsumoAcumGrAveHembras = avesHFin > 0 ? cumKgH * 1000.0 / avesHFin : null,
                ConsumoAcumGrAveHembrasGuia = PrimeraGuia(filas.Select(s => s.ConsumoAcumGrAveHembrasGuia)),

                PesoHembras = pesoH,
                PesoHembrasGuia = PrimeraGuia(filas.Select(s => s.PesoHembrasGuia)),
                GananciaHembras = pesoH.HasValue && pesoHPrev.HasValue ? pesoH - pesoHPrev : pesoH,
                DesviacionPesoHembrasPct = DesvPct(pesoH, PrimeraGuia(filas.Select(s => s.PesoHembrasGuia))),

                UniformidadHembras = Prom(filas.Select(s => s.UniformidadHembras)),
                UniformidadGuia = PrimeraGuia(filas.Select(s => s.UniformidadGuia)),
                CvHembras = Prom(filas.Select(s => s.CvHembras)),

                KcalAlimentoHembras = Prom(filas.Select(s => s.KcalAlimentoHembras)),
                ProtAlimentoHembras = Prom(filas.Select(s => s.ProtAlimentoHembras)),
                KcalAveAcumHembras = Prom(filas.Select(s => s.KcalAveAcumHembras)),
                ProtAveAcumHembras = Prom(filas.Select(s => s.ProtAveAcumHembras)),

                MortalidadMachos = mortM,
                MortalidadMachosPct = Pct(mortM, baseM),
                MortalidadMachosAcumPct = Pct(cumMortM, baseM),
                SeleccionMachos = selM,
                SeleccionMachosPct = Pct(selM, baseM),
                SeleccionMachosAcumPct = Pct(cumSelM, baseM),
                MortSelMachosGuiaPct = PrimeraGuia(filas.Select(s => s.MortSelMachosGuiaPct)),
                ErrorMachos = errM,
                ErrorMachosPct = Pct(errM, baseM),
                ErrorMachosAcumPct = Pct(cumErrM, baseM),
                RetiroAcumMachosPct = Pct(cumMortM + cumSelM + cumErrM, baseM),
                RetiroAcumMachosGuiaPct = PrimeraGuia(filas.Select(s => s.RetiroAcumMachosGuiaPct)),

                ConsumoKgMachos = kgM,
                ConsumoKgMachosAcum = cumKgM,
                GrAveDiaMachos = grAveDiaM,
                GrAveDiaMachosGuia = PrimeraGuia(filas.Select(s => s.GrAveDiaMachosGuia)),
                IncrementoGrAveDiaMachos = grAveDiaM.HasValue && grAveDiaMPrev.HasValue ? grAveDiaM - grAveDiaMPrev : grAveDiaM,
                IncrementoGrAveDiaMachosGuia = PrimeraGuia(filas.Select(s => s.IncrementoGrAveDiaMachosGuia)),
                ConsumoAcumGrAveMachos = avesMFin > 0 ? cumKgM * 1000.0 / avesMFin : null,
                ConsumoAcumGrAveMachosGuia = PrimeraGuia(filas.Select(s => s.ConsumoAcumGrAveMachosGuia)),

                PesoMachos = pesoM,
                PesoMachosGuia = PrimeraGuia(filas.Select(s => s.PesoMachosGuia)),
                GananciaMachos = pesoM.HasValue && pesoMPrev.HasValue ? pesoM - pesoMPrev : pesoM,
                DesviacionPesoMachosPct = DesvPct(pesoM, PrimeraGuia(filas.Select(s => s.PesoMachosGuia)))
            };

            semanas.Add(dto);

            if (grAveDiaH.HasValue) grAveDiaHPrev = grAveDiaH;
            if (grAveDiaM.HasValue) grAveDiaMPrev = grAveDiaM;
            if (pesoH.HasValue) pesoHPrev = pesoH;
            if (pesoM.HasValue) pesoMPrev = pesoM;
        }

        return new ReporteSemanalLevanteTabDto { Header = header, Semanas = semanas };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONSOLIDADO PRODUCCIÓN — sumas de conteos/kg/huevos, % recomputados sobre
    // las sumas, pesos/peso huevo/uniformidad = promedio simple, guía = primer
    // valor no nulo (misma guía para todos los galpones del lote base).
    // ─────────────────────────────────────────────────────────────────────────
    public static ReporteSemanalProduccionTabDto ConsolidarProduccion(IReadOnlyList<ReporteSemanalProduccionTabDto> tabs)
    {
        var header = new ReporteSemanalTabHeaderDto
        {
            LoteNombre = "Consolidado",
            EsConsolidado = true,
            BaseHembras = tabs.Sum(t => t.Header.BaseHembras),
            BaseMachos = tabs.Sum(t => t.Header.BaseMachos),
            Raza = tabs.Select(t => t.Header.Raza).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
            AnioGuia = tabs.Select(t => t.Header.AnioGuia).FirstOrDefault(a => a.HasValue),
            FechaEncaset = tabs.Select(t => t.Header.FechaEncaset).Where(x => x.HasValue).Min()
        };

        var porSemana = tabs
            .SelectMany(t => t.Semanas)
            .GroupBy(s => s.Semana)
            .OrderBy(g => g.Key)
            .ToList();

        var semanas = new List<ReporteSemanalProduccionSemanaDto>(porSemana.Count);
        long cumHuevosTot = 0, cumHuevosInc = 0, cumCargados = 0;
        double cumKgH = 0, cumKgM = 0;
        double cumMortSelH = 0, cumMortSelM = 0;
        double? grAveDiaHPrev = null;
        double baseH = 0, baseM = 0;
        var primera = true;

        foreach (var grupo in porSemana)
        {
            var filas = grupo.ToList();
            var avesHFin = filas.Sum(s => s.AvesHembrasFin);
            var avesMFin = filas.Sum(s => s.AvesMachosFin);
            var mortH = filas.Sum(s => s.MortalidadHembras);
            var mortM = filas.Sum(s => s.MortalidadMachos);
            var selH = filas.Sum(s => s.SeleccionHembras);
            var selM = filas.Sum(s => s.SeleccionMachos);
            var huevosTot = filas.Sum(s => s.HuevosTotales);
            var huevosInc = filas.Sum(s => s.HuevosIncubables);
            var kgH = filas.Sum(s => s.ConsumoKgHembras);
            var kgM = filas.Sum(s => s.ConsumoKgMachos);
            var dias = filas.Max(s => s.DiasConRegistro);
            var diasCalc = dias > 0 ? dias : 7;

            var iniH = avesHFin + mortH + selH;
            var iniM = avesMFin + mortM;
            if (primera) { baseH = iniH; baseM = iniM; primera = false; }

            var cargados = filas.Sum(s => s.HuevosCargadosPlanta);
            cumCargados += cargados;

            cumHuevosTot += huevosTot;
            cumHuevosInc += huevosInc;
            cumKgH += kgH;
            cumKgM += kgM;
            cumMortSelH += mortH + selH;
            cumMortSelM += mortM + selM;

            var grAveDiaH = iniH > 0 ? kgH * 1000.0 / (diasCalc * iniH) : (double?)null;
            var grAveDiaM = iniM > 0 ? kgM * 1000.0 / (diasCalc * iniM) : (double?)null;
            var pctProduccion = iniH > 0 ? (huevosTot / (double)diasCalc) / iniH * 100.0 : (double?)null;
            var pesoHuevo = Prom(filas.Select(s => s.PesoHuevo));

            var dto = new ReporteSemanalProduccionSemanaDto
            {
                Semana = grupo.Key,
                FechaInicioSemana = filas.Select(s => s.FechaInicioSemana).Where(x => x.HasValue).Min(),
                FechaFinSemana = filas.Select(s => s.FechaFinSemana).Where(x => x.HasValue).Max(),
                DiasConRegistro = dias,

                AvesHembrasFin = avesHFin,
                AvesMachosFin = avesMFin,
                ApareoPct = Pct(avesMFin, avesHFin),
                ApareoGuiaPct = PrimeraGuia(filas.Select(s => s.ApareoGuiaPct)),

                MortalidadHembras = mortH,
                SeleccionHembras = selH,
                MortalidadHembrasPct = Pct(mortH, iniH),
                MortalidadHembrasGuiaPct = PrimeraGuia(filas.Select(s => s.MortalidadHembrasGuiaPct)),
                MortalidadHembrasAcumGuiaPct = PrimeraGuia(filas.Select(s => s.MortalidadHembrasAcumGuiaPct)),
                MortSelHembrasAcumPct = Pct(cumMortSelH, baseH),
                RetiroAcumHembrasGuiaPct = PrimeraGuia(filas.Select(s => s.RetiroAcumHembrasGuiaPct)),

                MortalidadMachos = mortM,
                SeleccionMachos = selM,
                MortalidadMachosPct = Pct(mortM, iniM),
                MortalidadMachosGuiaPct = PrimeraGuia(filas.Select(s => s.MortalidadMachosGuiaPct)),
                MortSelMachosAcumPct = Pct(cumMortSelM, baseM),
                RetiroAcumMachosGuiaPct = PrimeraGuia(filas.Select(s => s.RetiroAcumMachosGuiaPct)),

                HuevosTotales = huevosTot,
                HuevosTotalesAcum = cumHuevosTot,
                Htaa = baseH > 0 ? cumHuevosTot / baseH : null,
                HtaaGuia = PrimeraGuia(filas.Select(s => s.HtaaGuia)),
                PorcentajeProduccion = pctProduccion,
                PorcentajeProduccionGuia = PrimeraGuia(filas.Select(s => s.PorcentajeProduccionGuia)),

                HuevosIncubables = huevosInc,
                HuevosIncubablesAcum = cumHuevosInc,
                PorcentajeIncubables = huevosTot > 0 ? huevosInc * 100.0 / huevosTot : null,
                PorcentajeIncubablesGuia = PrimeraGuia(filas.Select(s => s.PorcentajeIncubablesGuia)),
                PorcentajeIncubablesAcum = cumHuevosTot > 0 ? cumHuevosInc * 100.0 / cumHuevosTot : null,
                PorcentajeIncubablesAcumGuia = PrimeraGuia(filas.Select(s => s.PorcentajeIncubablesAcumGuia)),
                Hiaa = baseH > 0 ? cumHuevosInc / baseH : null,
                HiaaGuia = PrimeraGuia(filas.Select(s => s.HiaaGuia)),

                ConsumoKgHembras = kgH,
                ConsumoKgHembrasAcum = cumKgH,
                GrAveDiaHembras = grAveDiaH,
                GrAveDiaHembrasGuia = PrimeraGuia(filas.Select(s => s.GrAveDiaHembrasGuia)),
                IncrementoGrAveDiaHembras = grAveDiaH.HasValue && grAveDiaHPrev.HasValue ? grAveDiaH - grAveDiaHPrev : grAveDiaH,

                ConsumoKgMachos = kgM,
                ConsumoKgMachosAcum = cumKgM,
                GrAveDiaMachos = grAveDiaM,
                GrAveDiaMachosGuia = PrimeraGuia(filas.Select(s => s.GrAveDiaMachosGuia)),

                ConversionGrHuevoInc = huevosInc > 0 ? (kgH + kgM) * 1000.0 / huevosInc : null,
                ConversionGrHuevoIncGuia = PrimeraGuia(filas.Select(s => s.ConversionGrHuevoIncGuia)),

                PesoHuevo = pesoHuevo,
                PesoHuevoGuia = PrimeraGuia(filas.Select(s => s.PesoHuevoGuia)),
                MasaHuevoLote = pctProduccion.HasValue && pesoHuevo.HasValue
                    ? pctProduccion.Value / 100.0 * pesoHuevo.Value
                    : null,
                MasaHuevoGuia = PrimeraGuia(filas.Select(s => s.MasaHuevoGuia)),

                PesoHembras = Prom(filas.Select(s => s.PesoHembras)),
                PesoHembrasGuia = PrimeraGuia(filas.Select(s => s.PesoHembrasGuia)),
                DesviacionPesoHembrasPct = DesvPct(
                    Prom(filas.Select(s => s.PesoHembras)),
                    PrimeraGuia(filas.Select(s => s.PesoHembrasGuia))),
                PesoMachos = Prom(filas.Select(s => s.PesoMachos)),
                PesoMachosGuia = PrimeraGuia(filas.Select(s => s.PesoMachosGuia)),
                DesviacionPesoMachosPct = DesvPct(
                    Prom(filas.Select(s => s.PesoMachos)),
                    PrimeraGuia(filas.Select(s => s.PesoMachosGuia))),

                Uniformidad = Prom(filas.Select(s => s.Uniformidad)),
                UniformidadGuia = PrimeraGuia(filas.Select(s => s.UniformidadGuia)),
                CoeficienteVariacion = Prom(filas.Select(s => s.CoeficienteVariacion)),

                HuevosCargadosPlanta = cargados,
                HuevosCargadosPlantaAcum = cumCargados,
                PorcentajeCargaSobreIncubables = Pct(cargados, huevosInc),
                NacimientoGuiaPct = PrimeraGuia(filas.Select(s => s.NacimientoGuiaPct)),
                PollitosAveGuia = PrimeraGuia(filas.Select(s => s.PollitosAveGuia))
            };

            semanas.Add(dto);
            if (grAveDiaH.HasValue) grAveDiaHPrev = grAveDiaH;
        }

        return new ReporteSemanalProduccionTabDto { Header = header, Semanas = semanas };
    }

    /// <summary>Promedio simple de los valores con dato; null si ninguno tiene.</summary>
    public static double? Prom(IEnumerable<double?> valores)
    {
        var conDato = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return conDato.Count == 0 ? null : conDato.Average();
    }

    /// <summary>Primer valor de guía no nulo (todos los galpones del lote base comparten guía).</summary>
    public static double? PrimeraGuia(IEnumerable<double?> valores) =>
        valores.FirstOrDefault(v => v.HasValue);
}
