// Liquidación Técnica para Ecuador: lote aves de engorde (lote_ave_engorde + seguimiento_diario_aves_engorde).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LiquidacionTecnicaEcuadorService : ILiquidacionTecnicaEcuadorService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public LiquidacionTecnicaEcuadorService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LiquidacionTecnicaDto> CalcularLiquidacionAsync(int loteAveEngordeId, DateTime? fechaHasta = null)
    {
        var lote = await ObtenerLoteAsync(loteAveEngordeId);
        var seguimientos = await ObtenerSeguimientosAsync(loteAveEngordeId, fechaHasta);
        var datosGuia = await ObtenerDatosGuiaAsync(lote.Raza, lote.AnoTablaGenetica);
        var metricas = CalcularMetricasAcumuladas(lote, seguimientos);
        var diferencias = CalcularDiferenciasConGuia(metricas, datosGuia);

        return new LiquidacionTecnicaDto(
            lote.LoteAveEngordeId?.ToString() ?? "0",
            lote.LoteNombre ?? "",
            lote.FechaEncaset ?? DateTime.MinValue,
            lote.Raza,
            lote.AnoTablaGenetica,
            lote.CodigoGuiaGenetica,
            lote.HembrasL,
            lote.MachosL,
            lote.AvesEncasetadas ?? (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0),
            metricas.PorcentajeMortalidadHembras,
            metricas.PorcentajeMortalidadMachos,
            metricas.PorcentajeSeleccionHembras,
            metricas.PorcentajeSeleccionMachos,
            metricas.PorcentajeErrorSexajeHembras,
            metricas.PorcentajeErrorSexajeMachos,
            metricas.PorcentajeRetiroTotalHembras,
            metricas.PorcentajeRetiroTotalMachos,
            metricas.PorcentajeRetiroTotalGeneral,
            diferencias.PorcentajeRetiroGuia,
            metricas.ConsumoTotalGramos,
            diferencias.ConsumoGuiaGramos,
            diferencias.PorcentajeDiferenciaConsumo,
            metricas.PesoFinalHembras,
            metricas.PesoFinalMachos,
            diferencias.PesoGuiaHembras,
            diferencias.PesoGuiaMachos,
            diferencias.PorcentajeDiferenciaPesoHembras,
            diferencias.PorcentajeDiferenciaPesoMachos,
            metricas.UniformidadFinalHembras,
            metricas.UniformidadFinalMachos,
            diferencias.UniformidadGuiaHembras,
            diferencias.UniformidadGuiaMachos,
            diferencias.PorcentajeDiferenciaUniformidadHembras,
            diferencias.PorcentajeDiferenciaUniformidadMachos,
            DateTime.UtcNow,
            seguimientos.Count,
            seguimientos.LastOrDefault()?.Fecha
        );
    }

    public async Task<LiquidacionTecnicaCompletaDto> ObtenerLiquidacionCompletaAsync(int loteAveEngordeId, DateTime? fechaHasta = null)
    {
        var liquidacion = await CalcularLiquidacionAsync(loteAveEngordeId, fechaHasta);
        var seguimientos = await ObtenerSeguimientosAsync(loteAveEngordeId, fechaHasta);
        var lote = await ObtenerLoteAsync(loteAveEngordeId);
        var datosGuia = await ObtenerDatosGuiaAsync(lote.Raza, lote.AnoTablaGenetica);

        var detalleSeguimiento = seguimientos.Select(s => new DetalleSeguimientoLiquidacionDto(
            s.Fecha,
            CalcularSemana(lote.FechaEncaset, s.Fecha),
            s.MortalidadHembras ?? 0,
            s.MortalidadMachos ?? 0,
            s.SelH ?? 0,
            s.SelM ?? 0,
            s.ErrorSexajeHembras ?? 0,
            s.ErrorSexajeMachos ?? 0,
            (double)(s.ConsumoKgHembras ?? 0),
            s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : 0,
            s.PesoPromHembras,
            s.PesoPromMachos,
            s.UniformidadHembras,
            s.UniformidadMachos
        )).ToList();

        return new LiquidacionTecnicaCompletaDto(liquidacion, detalleSeguimiento, datosGuia);
    }

    public async Task<LiquidacionTecnicaComparacionDto> CompararConGuiaGeneticaAsync(int loteAveEngordeId, DateTime? fechaHasta = null)
    {
        var lote = await ObtenerLoteAsync(loteAveEngordeId);
        var seguimientos = await ObtenerSeguimientosAsync(loteAveEngordeId, fechaHasta);
        var guiaGenetica = await ObtenerGuiaGeneticaAsync(lote.Raza, lote.AnoTablaGenetica);
        var metricasReales = CalcularMetricasReales(lote, seguimientos);
        return CompararConGuia(lote, metricasReales, guiaGenetica, seguimientos.Count);
    }

    public async Task<LiquidacionTecnicaComparacionCompletaDto> ObtenerComparacionCompletaAsync(int loteAveEngordeId, DateTime? fechaHasta = null)
    {
        var resumen = await CompararConGuiaGeneticaAsync(loteAveEngordeId, fechaHasta);
        var seguimientos = await ObtenerSeguimientosAsync(loteAveEngordeId, fechaHasta);
        var lote = await ObtenerLoteAsync(loteAveEngordeId);

        var detalleSeguimiento = seguimientos.Select(s => new DetalleSeguimientoLiquidacionDto(
            s.Fecha,
            CalcularSemana(lote.FechaEncaset, s.Fecha),
            s.MortalidadHembras ?? 0,
            s.MortalidadMachos ?? 0,
            s.SelH ?? 0,
            s.SelM ?? 0,
            s.ErrorSexajeHembras ?? 0,
            s.ErrorSexajeMachos ?? 0,
            (double)(s.ConsumoKgHembras ?? 0),
            s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : 0,
            s.PesoPromHembras,
            s.PesoPromMachos,
            s.UniformidadHembras,
            s.UniformidadMachos
        )).ToList();

        var comparacionesDetalladas = CrearComparacionesDetalladas(resumen);
        return new LiquidacionTecnicaComparacionCompletaDto(
            resumen,
            comparacionesDetalladas,
            detalleSeguimiento,
            GenerarObservaciones(resumen)
        );
    }

    public async Task<bool> ValidarLoteParaLiquidacionAsync(int loteAveEngordeId)
    {
        var loteExiste = await _context.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null)
            .AnyAsync();
        if (!loteExiste) return false;

        var tieneSeguimiento = await _context.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId)
            .AnyAsync();
        return tieneSeguimiento;
    }

    #region Privados

    private async Task<LoteAveEngorde> ObtenerLoteAsync(int loteAveEngordeId)
    {
        var lote = await _context.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{loteAveEngordeId}' no encontrado o no pertenece a la compañía.");

        return lote;
    }

    private async Task<List<SeguimientoDiarioAvesEngorde>> ObtenerSeguimientosAsync(int loteAveEngordeId, DateTime? fechaHasta)
    {
        var query = _context.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId);

        if (fechaHasta.HasValue)
            query = query.Where(s => s.Fecha.Date <= fechaHasta.Value.Date);

        var lote = await ObtenerLoteAsync(loteAveEngordeId);
        if (lote.FechaEncaset.HasValue)
        {
            var fechaMaxima = lote.FechaEncaset.Value.AddDays(175);
            query = query.Where(s => s.Fecha <= fechaMaxima);
        }

        return await query.OrderBy(s => s.Fecha).ToListAsync();
    }

    private async Task<DatosGuiaGeneticaDto?> ObtenerDatosGuiaAsync(string? raza, int? anoTablaGenetica)
    {
        if (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue) return null;

        var edadBuscar = new[] { "175", "25", "25.0" };
        ProduccionAvicolaRaw? datosGuia = null;

        foreach (var edad in edadBuscar)
        {
            datosGuia = await _context.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p => p.CompanyId == _currentUser.CompanyId &&
                            p.DeletedAt == null &&
                            p.Raza == raza &&
                            p.AnioGuia == anoTablaGenetica.ToString() &&
                            p.Edad == edad)
                .FirstOrDefaultAsync();
            if (datosGuia != null) break;
        }

        if (datosGuia == null) return null;

        return new DatosGuiaGeneticaDto(
            datosGuia.AnioGuia,
            datosGuia.Raza,
            datosGuia.Edad,
            ParseDecimal(datosGuia.PesoH),
            ParseDecimal(datosGuia.PesoM),
            ParseDecimal(datosGuia.Uniformidad),
            ParseDecimal(datosGuia.ConsAcH),
            ParseDecimal(datosGuia.RetiroAcH)
        );
    }

    private async Task<ProduccionAvicolaRaw?> ObtenerGuiaGeneticaAsync(string? raza, int? anoTablaGenetica)
    {
        if (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue) return null;

        var razasParaBuscar = new List<string> { raza };
        if (raza.Contains("Ross 308") || raza.Contains("Ross308"))
            razasParaBuscar.AddRange(new[] { "R308", "Ross 308", "Ross308" });
        else if (raza.Contains("Cobb 500") || raza.Contains("Cobb500"))
            razasParaBuscar.AddRange(new[] { "C500", "Cobb 500", "Cobb500" });
        else if (raza.Contains("AP"))
            razasParaBuscar.AddRange(new[] { "AP", "Arbor Acres Plus" });

        var guiaExacta = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.Raza != null && razasParaBuscar.Contains(p.Raza) &&
                        p.AnioGuia == anoTablaGenetica.Value.ToString() &&
                        p.CompanyId == _currentUser.CompanyId)
            .FirstOrDefaultAsync();

        if (guiaExacta != null) return guiaExacta;

        var anosCercanos = new[] {
            anoTablaGenetica.Value.ToString(),
            (anoTablaGenetica.Value - 1).ToString(),
            (anoTablaGenetica.Value - 2).ToString(),
            (anoTablaGenetica.Value + 1).ToString(),
            (anoTablaGenetica.Value + 2).ToString()
        };

        var resultados = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.Raza != null && razasParaBuscar.Contains(p.Raza) &&
                        anosCercanos.Contains(p.AnioGuia ?? "") &&
                        p.CompanyId == _currentUser.CompanyId)
            .ToListAsync();

        return resultados
            .OrderBy(p =>
            {
                if (int.TryParse(p.AnioGuia, out var ano))
                    return Math.Abs(ano - anoTablaGenetica.Value);
                return int.MaxValue;
            })
            .FirstOrDefault();
    }

    private static MetricasAcumuladas CalcularMetricasAcumuladas(LoteAveEngorde lote, List<SeguimientoDiarioAvesEngorde> seguimientos)
    {
        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;

        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras ?? 0);
        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos ?? 0);
        var totalSeleccionH = seguimientos.Sum(s => s.SelH ?? 0);
        var totalSeleccionM = seguimientos.Sum(s => s.SelM ?? 0);
        var totalErrorH = seguimientos.Sum(s => s.ErrorSexajeHembras ?? 0);
        var totalErrorM = seguimientos.Sum(s => s.ErrorSexajeMachos ?? 0);

        var porcMortalidadH = hembrasIniciales > 0 ? (decimal)totalMortalidadH / hembrasIniciales * 100 : 0;
        var porcMortalidadM = machosIniciales > 0 ? (decimal)totalMortalidadM / machosIniciales * 100 : 0;
        var porcSeleccionH = hembrasIniciales > 0 ? (decimal)totalSeleccionH / hembrasIniciales * 100 : 0;
        var porcSeleccionM = machosIniciales > 0 ? (decimal)totalSeleccionM / machosIniciales * 100 : 0;
        var porcErrorH = hembrasIniciales > 0 ? (decimal)totalErrorH / hembrasIniciales * 100 : 0;
        var porcErrorM = machosIniciales > 0 ? (decimal)totalErrorM / machosIniciales * 100 : 0;

        var porcRetiroH = porcMortalidadH + porcSeleccionH + porcErrorH;
        var porcRetiroM = porcMortalidadM + porcSeleccionM + porcErrorM;
        var totalAves = hembrasIniciales + machosIniciales;
        var porcRetiroGeneral = totalAves > 0
            ? (decimal)(totalMortalidadH + totalMortalidadM + totalSeleccionH + totalSeleccionM + totalErrorH + totalErrorM) / totalAves * 100
            : 0;

        var consumoTotal = (decimal)seguimientos.Sum(s => (s.ConsumoKgHembras ?? 0) + (s.ConsumoKgMachos ?? 0)) * 1000;

        var ultimo = seguimientos.LastOrDefault();
        var pesoFinalH = ultimo?.PesoPromHembras != null ? (decimal?)ultimo.PesoPromHembras : null;
        var pesoFinalM = ultimo?.PesoPromMachos != null ? (decimal?)ultimo.PesoPromMachos : null;
        var uniformidadFinalH = ultimo?.UniformidadHembras != null ? (decimal?)ultimo.UniformidadHembras : null;
        var uniformidadFinalM = ultimo?.UniformidadMachos != null ? (decimal?)ultimo.UniformidadMachos : null;

        return new MetricasAcumuladas(
            porcMortalidadH, porcMortalidadM,
            porcSeleccionH, porcSeleccionM,
            porcErrorH, porcErrorM,
            porcRetiroH, porcRetiroM, porcRetiroGeneral,
            consumoTotal,
            pesoFinalH, pesoFinalM,
            uniformidadFinalH, uniformidadFinalM
        );
    }

    private static MetricasReales CalcularMetricasReales(LoteAveEngorde lote, List<SeguimientoDiarioAvesEngorde> seguimientos)
    {
        if (seguimientos.Count == 0)
            return new MetricasReales(0, 0, 0, 0, null, null, null, null);

        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;
        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras ?? 0);
        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos ?? 0);
        var porcMortalidadH = hembrasIniciales > 0 ? (decimal)totalMortalidadH / hembrasIniciales * 100 : 0;
        var porcMortalidadM = machosIniciales > 0 ? (decimal)totalMortalidadM / machosIniciales * 100 : 0;
        var consumoTotalH = (decimal)seguimientos.Sum(s => s.ConsumoKgHembras ?? 0) * 1000;
        var consumoTotalM = (decimal)seguimientos.Sum(s => s.ConsumoKgMachos ?? 0) * 1000;
        var ultimo = seguimientos.LastOrDefault();
        var pesoFinalH = ultimo?.PesoPromHembras != null ? (decimal?)ultimo.PesoPromHembras : null;
        var pesoFinalM = ultimo?.PesoPromMachos != null ? (decimal?)ultimo.PesoPromMachos : null;
        var uniformidadFinalH = ultimo?.UniformidadHembras != null ? (decimal?)ultimo.UniformidadHembras : null;
        var uniformidadFinalM = ultimo?.UniformidadMachos != null ? (decimal?)ultimo.UniformidadMachos : null;

        return new MetricasReales(
            porcMortalidadH, porcMortalidadM,
            consumoTotalH, consumoTotalM,
            pesoFinalH, pesoFinalM,
            uniformidadFinalH, uniformidadFinalM
        );
    }

    private static DiferenciasConGuia CalcularDiferenciasConGuia(MetricasAcumuladas metricas, DatosGuiaGeneticaDto? guia)
    {
        if (guia == null)
            return new DiferenciasConGuia(null, null, null, null, null, null, null, null, null, null, null, null);

        var difConsumo = CalcularDiferenciaPorcentual(metricas.ConsumoTotalGramos, guia.ConsumoAcumulado);
        var difPesoH = CalcularDiferenciaPorcentual(metricas.PesoFinalHembras, guia.PesoHembras);
        var difPesoM = CalcularDiferenciaPorcentual(metricas.PesoFinalMachos, guia.PesoMachos);
        var difUniformidadH = CalcularDiferenciaPorcentual(metricas.UniformidadFinalHembras, guia.Uniformidad);
        var difUniformidadM = CalcularDiferenciaPorcentual(metricas.UniformidadFinalMachos, guia.Uniformidad);

        return new DiferenciasConGuia(
            guia.PorcentajeRetiro,
            guia.ConsumoAcumulado,
            difConsumo,
            guia.PesoHembras,
            guia.PesoMachos,
            difPesoH,
            difPesoM,
            guia.Uniformidad,
            guia.Uniformidad,
            difUniformidadH,
            difUniformidadM,
            guia.ConsumoAcumulado
        );
    }

    private LiquidacionTecnicaComparacionDto CompararConGuia(
        LoteAveEngorde lote,
        MetricasReales metricasReales,
        ProduccionAvicolaRaw? guiaGenetica,
        int totalSeguimientos)
    {
        int loteId = lote.LoteAveEngordeId ?? 0;

        if (guiaGenetica == null)
        {
            return new LiquidacionTecnicaComparacionDto(
                loteId,
                lote.LoteNombre ?? "",
                lote.Raza,
                lote.AnoTablaGenetica,
                null,
                null,
                metricasReales.PorcentajeMortalidadHembras,
                metricasReales.PorcentajeMortalidadMachos,
                metricasReales.ConsumoAcumuladoHembras,
                metricasReales.ConsumoAcumuladoMachos,
                metricasReales.PesoFinalHembras,
                metricasReales.PesoFinalMachos,
                metricasReales.UniformidadFinalHembras,
                metricasReales.UniformidadFinalMachos,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                "Sin guía genética",
                DateTime.UtcNow,
                totalSeguimientos,
                null
            );
        }

        var pesoH = ParseDecimal(guiaGenetica.PesoH);
        var pesoM = ParseDecimal(guiaGenetica.PesoM);
        var consumo = ParseDecimal(guiaGenetica.ConsAcH);
        var unif = ParseDecimal(guiaGenetica.Uniformidad);

        var difMortalidadH = CalcularDiferenciaPorcentual(metricasReales.PorcentajeMortalidadHembras, 5.0m);
        var difMortalidadM = CalcularDiferenciaPorcentual(metricasReales.PorcentajeMortalidadMachos, 5.0m);
        var difConsumoH = CalcularDiferenciaPorcentual(metricasReales.ConsumoAcumuladoHembras, consumo ?? 2000m);
        var difConsumoM = CalcularDiferenciaPorcentual(metricasReales.ConsumoAcumuladoMachos, consumo ?? 2000m);
        var difPesoH = CalcularDiferenciaPorcentual(metricasReales.PesoFinalHembras, pesoH ?? 2000m);
        var difPesoM = CalcularDiferenciaPorcentual(metricasReales.PesoFinalMachos, pesoM ?? 2000m);
        var difUniformidadH = CalcularDiferenciaPorcentual(metricasReales.UniformidadFinalHembras, unif ?? 85m);
        var difUniformidadM = CalcularDiferenciaPorcentual(metricasReales.UniformidadFinalMachos, unif ?? 85m);

        var tolerancia = 10m;
        var cumpleMortalidadH = EvaluarCumplimiento(difMortalidadH, tolerancia);
        var cumpleMortalidadM = EvaluarCumplimiento(difMortalidadM, tolerancia);
        var cumpleConsumoH = EvaluarCumplimiento(difConsumoH, tolerancia);
        var cumpleConsumoM = EvaluarCumplimiento(difConsumoM, tolerancia);
        var cumplePesoH = EvaluarCumplimiento(difPesoH, tolerancia);
        var cumplePesoM = EvaluarCumplimiento(difPesoM, tolerancia);
        var cumpleUniformidadH = EvaluarCumplimiento(difUniformidadH, tolerancia);
        var cumpleUniformidadM = EvaluarCumplimiento(difUniformidadM, tolerancia);

        var parametrosEvaluados = new[] { cumpleMortalidadH, cumpleMortalidadM, cumpleConsumoH, cumpleConsumoM, cumplePesoH, cumplePesoM, cumpleUniformidadH, cumpleUniformidadM };
        var totalParametros = parametrosEvaluados.Count(p => p.HasValue);
        var parametrosCumplidos = parametrosEvaluados.Count(p => p == true);
        var porcentajeCumplimiento = totalParametros > 0 ? (decimal)parametrosCumplidos / totalParametros * 100 : 0;
        var estadoGeneral = porcentajeCumplimiento >= 90 ? "Excelente" : porcentajeCumplimiento >= 75 ? "Bueno" : porcentajeCumplimiento >= 50 ? "Regular" : "Deficiente";

        return new LiquidacionTecnicaComparacionDto(
            loteId,
            lote.LoteNombre ?? "",
            lote.Raza,
            lote.AnoTablaGenetica,
            null,
            guiaGenetica.Raza,
            metricasReales.PorcentajeMortalidadHembras,
            metricasReales.PorcentajeMortalidadMachos,
            metricasReales.ConsumoAcumuladoHembras,
            metricasReales.ConsumoAcumuladoMachos,
            metricasReales.PesoFinalHembras,
            metricasReales.PesoFinalMachos,
            metricasReales.UniformidadFinalHembras,
            metricasReales.UniformidadFinalMachos,
            5.0m,
            5.0m,
            consumo ?? 2000m,
            consumo ?? 2000m,
            pesoH ?? 2000m,
            pesoM ?? 2000m,
            unif ?? 85m,
            unif ?? 85m,
            difMortalidadH,
            difMortalidadM,
            difConsumoH,
            difConsumoM,
            difPesoH,
            difPesoM,
            difUniformidadH,
            difUniformidadM,
            cumpleMortalidadH ?? false,
            cumpleMortalidadM ?? false,
            cumpleConsumoH ?? false,
            cumpleConsumoM ?? false,
            cumplePesoH ?? false,
            cumplePesoM ?? false,
            cumpleUniformidadH ?? false,
            cumpleUniformidadM ?? false,
            totalParametros,
            parametrosCumplidos,
            porcentajeCumplimiento,
            estadoGeneral,
            DateTime.UtcNow,
            totalSeguimientos,
            null
        );
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorEsperado)
    {
        if (!valorReal.HasValue || valorEsperado == 0) return null;
        return ((valorReal.Value - valorEsperado) / valorEsperado) * 100;
    }

    private static bool? EvaluarCumplimiento(decimal? diferencia, decimal tolerancia)
    {
        if (!diferencia.HasValue) return null;
        return Math.Abs(diferencia.Value) <= tolerancia;
    }

    private static int CalcularSemana(DateTime? fechaEncaset, DateTime fechaRegistro)
    {
        if (!fechaEncaset.HasValue) return 0;
        var dias = (fechaRegistro - fechaEncaset.Value).Days;
        return (dias / 7) + 1;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static List<ComparacionDetalladaDto> CrearComparacionesDetalladas(LiquidacionTecnicaComparacionDto c)
    {
        var list = new List<ComparacionDetalladaDto>();
        list.Add(new ComparacionDetalladaDto("Mortalidad Hembras", c.PorcentajeMortalidadHembras, c.MortalidadEsperadaHembras, c.DiferenciaMortalidadHembras, 10m, c.CumpleMortalidadHembras, c.CumpleMortalidadHembras ? "Cumple" : "Excede"));
        list.Add(new ComparacionDetalladaDto("Mortalidad Machos", c.PorcentajeMortalidadMachos, c.MortalidadEsperadaMachos, c.DiferenciaMortalidadMachos, 10m, c.CumpleMortalidadMachos, c.CumpleMortalidadMachos ? "Cumple" : "Excede"));
        list.Add(new ComparacionDetalladaDto("Consumo Hembras", c.ConsumoAcumuladoHembras, c.ConsumoEsperadoHembras, c.DiferenciaConsumoHembras, 10m, c.CumpleConsumoHembras, c.CumpleConsumoHembras ? "Cumple" : "Excede"));
        list.Add(new ComparacionDetalladaDto("Consumo Machos", c.ConsumoAcumuladoMachos, c.ConsumoEsperadoMachos, c.DiferenciaConsumoMachos, 10m, c.CumpleConsumoMachos, c.CumpleConsumoMachos ? "Cumple" : "Excede"));
        if (c.PesoFinalHembras.HasValue)
            list.Add(new ComparacionDetalladaDto("Peso Hembras", c.PesoFinalHembras.Value, c.PesoEsperadoHembras, c.DiferenciaPesoHembras, 10m, c.CumplePesoHembras, c.CumplePesoHembras ? "Cumple" : "Excede"));
        if (c.PesoFinalMachos.HasValue)
            list.Add(new ComparacionDetalladaDto("Peso Machos", c.PesoFinalMachos.Value, c.PesoEsperadoMachos, c.DiferenciaPesoMachos, 10m, c.CumplePesoMachos, c.CumplePesoMachos ? "Cumple" : "Excede"));
        if (c.UniformidadFinalHembras.HasValue)
            list.Add(new ComparacionDetalladaDto("Uniformidad Hembras", c.UniformidadFinalHembras.Value, c.UniformidadEsperadaHembras, c.DiferenciaUniformidadHembras, 10m, c.CumpleUniformidadHembras, c.CumpleUniformidadHembras ? "Cumple" : "Excede"));
        if (c.UniformidadFinalMachos.HasValue)
            list.Add(new ComparacionDetalladaDto("Uniformidad Machos", c.UniformidadFinalMachos.Value, c.UniformidadEsperadaMachos, c.DiferenciaUniformidadMachos, 10m, c.CumpleUniformidadMachos, c.CumpleUniformidadMachos ? "Cumple" : "Excede"));
        return list;
    }

    private static string? GenerarObservaciones(LiquidacionTecnicaComparacionDto c)
    {
        var obs = new List<string>();
        if (c.PorcentajeCumplimiento < 50) obs.Add("El lote presenta un cumplimiento deficiente con respecto a la guía genética.");
        else if (c.PorcentajeCumplimiento < 75) obs.Add("El lote presenta un cumplimiento regular con respecto a la guía genética.");
        if (!c.CumpleMortalidadHembras && c.DiferenciaMortalidadHembras.HasValue)
            obs.Add($"Mortalidad hembras supera lo esperado ({c.DiferenciaMortalidadHembras:F1}% de diferencia).");
        if (!c.CumpleMortalidadMachos && c.DiferenciaMortalidadMachos.HasValue)
            obs.Add($"Mortalidad machos supera lo esperado ({c.DiferenciaMortalidadMachos:F1}% de diferencia).");
        return obs.Count > 0 ? string.Join(" ", obs) : null;
    }

    #endregion

    #region Records internos

    private record MetricasAcumuladas(
        decimal PorcentajeMortalidadHembras,
        decimal PorcentajeMortalidadMachos,
        decimal PorcentajeSeleccionHembras,
        decimal PorcentajeSeleccionMachos,
        decimal PorcentajeErrorSexajeHembras,
        decimal PorcentajeErrorSexajeMachos,
        decimal PorcentajeRetiroTotalHembras,
        decimal PorcentajeRetiroTotalMachos,
        decimal PorcentajeRetiroTotalGeneral,
        decimal ConsumoTotalGramos,
        decimal? PesoFinalHembras,
        decimal? PesoFinalMachos,
        decimal? UniformidadFinalHembras,
        decimal? UniformidadFinalMachos
    );

    private record MetricasReales(
        decimal PorcentajeMortalidadHembras,
        decimal PorcentajeMortalidadMachos,
        decimal ConsumoAcumuladoHembras,
        decimal ConsumoAcumuladoMachos,
        decimal? PesoFinalHembras,
        decimal? PesoFinalMachos,
        decimal? UniformidadFinalHembras,
        decimal? UniformidadFinalMachos
    );

    private record DiferenciasConGuia(
        decimal? PorcentajeRetiroGuia,
        decimal? ConsumoGuiaGramos,
        decimal? PorcentajeDiferenciaConsumo,
        decimal? PesoGuiaHembras,
        decimal? PesoGuiaMachos,
        decimal? PorcentajeDiferenciaPesoHembras,
        decimal? PorcentajeDiferenciaPesoMachos,
        decimal? UniformidadGuiaHembras,
        decimal? UniformidadGuiaMachos,
        decimal? PorcentajeDiferenciaUniformidadHembras,
        decimal? PorcentajeDiferenciaUniformidadMachos,
        decimal? ConsumoGuia
    );

    #endregion
}
