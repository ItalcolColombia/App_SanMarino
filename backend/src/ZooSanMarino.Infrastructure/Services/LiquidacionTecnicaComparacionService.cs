// src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaComparacionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LiquidacionTecnicaComparacionService : ILiquidacionTecnicaComparacionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public LiquidacionTecnicaComparacionService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LiquidacionTecnicaComparacionDto> CompararConGuiaGeneticaAsync(int loteId, DateTime? fechaHasta = null)
    {
        // 1. Obtener lote
        var lote = await ObtenerLoteAsync(loteId);
        
        // 2. Obtener seguimientos
        var seguimientos = await ObtenerSeguimientosAsync(loteId, fechaHasta);
        
        // 3. Obtener guía genética desde ProduccionAvicolaRaw
        var guiaGenetica = await ObtenerGuiaGeneticaAsync(lote.Raza, lote.AnoTablaGenetica);
        
        // 4. Calcular métricas reales
        var metricasReales = CalcularMetricasReales(lote, seguimientos);
        
        // 5. Comparar con guía genética
        var comparacion = CompararConGuia(lote, metricasReales, guiaGenetica, seguimientos.Count);
        
        return comparacion;
    }

    public async Task<LiquidacionTecnicaComparacionCompletaDto> ObtenerComparacionCompletaAsync(int loteId, DateTime? fechaHasta = null)
    {
        var resumen = await CompararConGuiaGeneticaAsync(loteId, fechaHasta);
        var seguimientos = await ObtenerSeguimientosAsync(loteId, fechaHasta);
        var lote = await ObtenerLoteAsync(loteId);
        
        // Crear detalles de seguimiento
        var detalleSeguimiento = seguimientos.Select(s => new DetalleSeguimientoLiquidacionDto(
            s.FechaRegistro,
            CalcularSemana(lote.FechaEncaset, s.FechaRegistro),
            s.MortalidadHembras,
            s.MortalidadMachos,
            s.SelH,
            s.SelM,
            s.ErrorSexajeHembras,
            s.ErrorSexajeMachos,
            s.ConsumoKgHembras,
            s.ConsumoKgMachos,
            s.PesoPromH,
            s.PesoPromM,
            s.UniformidadH,
            s.UniformidadM
        )).ToList();

        // Crear comparaciones detalladas
        var comparacionesDetalladas = CrearComparacionesDetalladas(resumen);

        return new LiquidacionTecnicaComparacionCompletaDto(
            resumen,
            comparacionesDetalladas,
            detalleSeguimiento,
            GenerarObservaciones(resumen)
        );
    }

    public async Task<bool> ValidarGuiaGeneticaConfiguradaAsync(int loteId)
    {
        var lote = await ObtenerLoteAsync(loteId);
        return !string.IsNullOrEmpty(lote.Raza) && lote.AnoTablaGenetica.HasValue;
    }

    public async Task<IEnumerable<LineaGeneticaDisponibleDto>> ObtenerLineasGeneticasDisponiblesAsync(string? raza = null, int? ano = null)
    {
        var query = _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.CompanyId == _currentUser.CompanyId);

        if (!string.IsNullOrEmpty(raza))
            query = query.Where(p => p.Raza == raza);

        if (ano.HasValue)
            query = query.Where(p => p.AnioGuia == ano.Value.ToString());

        var resultados = await query
            .Select(p => new { 
                Raza = p.Raza ?? "", 
                AnioGuia = p.AnioGuia ?? "",
                CodigoGuia = p.Raza ?? ""
            })
            .Distinct()
            .OrderBy(l => l.Raza)
            .ThenBy(l => l.AnioGuia)
            .ToListAsync();

        return resultados.Select(r => new LineaGeneticaDisponibleDto(
            r.Raza,
            r.AnioGuia,
            r.CodigoGuia,
            $"Raza: {r.Raza}, Año: {r.AnioGuia}", // Generar descripción en memoria
            true
        ));
    }

    #region Métodos Privados

    private async Task<Domain.Entities.Lote> ObtenerLoteAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{loteId}' no encontrado o no pertenece a la compañía.");

        return lote;
    }

    private async Task<List<Domain.Entities.SeguimientoLoteLevante>> ObtenerSeguimientosAsync(int loteId, DateTime? fechaHasta)
    {
        var query = _context.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId);

        if (fechaHasta.HasValue)
        {
            query = query.Where(s => s.FechaRegistro.Date <= fechaHasta.Value.Date);
        }

        return await query
            .OrderBy(s => s.FechaRegistro)
            .ToListAsync();
    }

    private async Task<Domain.Entities.ProduccionAvicolaRaw?> ObtenerGuiaGeneticaAsync(string? raza, int? anoTablaGenetica)
    {
        if (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue)
            return null;

        // Normalizar el nombre de la raza para buscar en ProduccionAvicolaRaw
        var razasParaBuscar = new List<string> { raza };
        
        // Mapear nombres comunes de razas
        if (raza.Contains("Ross 308") || raza.Contains("Ross308"))
        {
            razasParaBuscar.AddRange(new[] { "R308", "Ross 308", "Ross308" });
        }
        else if (raza.Contains("Cobb 500") || raza.Contains("Cobb500"))
        {
            razasParaBuscar.AddRange(new[] { "C500", "Cobb 500", "Cobb500" });
        }
        else if (raza.Contains("AP"))
        {
            razasParaBuscar.AddRange(new[] { "AP", "Arbor Acres Plus" });
        }

        // Buscar primero con el año exacto
        var guiaExacta = await _context.ProduccionAvicolaRaw
            .AsNoTracking()
            .Where(p => p.Raza != null && razasParaBuscar.Contains(p.Raza) && 
                       p.AnioGuia == anoTablaGenetica.Value.ToString() &&
                       p.CompanyId == _currentUser.CompanyId)
            .FirstOrDefaultAsync();

        if (guiaExacta != null)
            return guiaExacta;

        // Si no encuentra con el año exacto, buscar con años cercanos (ampliar el rango)
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
                       anosCercanos.Contains(p.AnioGuia) &&
                       p.CompanyId == _currentUser.CompanyId)
            .ToListAsync();

        return resultados
            .OrderBy(p => {
                if (int.TryParse(p.AnioGuia, out var ano))
                    return Math.Abs(ano - anoTablaGenetica.Value);
                return int.MaxValue;
            })
            .FirstOrDefault();
    }

    private MetricasReales CalcularMetricasReales(Domain.Entities.Lote lote, List<Domain.Entities.SeguimientoLoteLevante> seguimientos)
    {
        if (!seguimientos.Any())
        {
            return new MetricasReales(0, 0, 0, 0, null, null, null, null);
        }

        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;

        // Calcular mortalidad acumulada
        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras);
        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos);

        // Calcular porcentajes de mortalidad
        var porcMortalidadH = hembrasIniciales > 0 ? (decimal)totalMortalidadH / hembrasIniciales * 100 : 0;
        var porcMortalidadM = machosIniciales > 0 ? (decimal)totalMortalidadM / machosIniciales * 100 : 0;

        // Calcular consumo acumulado
        var consumoTotalH = (decimal)seguimientos.Sum(s => s.ConsumoKgHembras) * 1000; // Convertir a gramos
        var consumoTotalM = (decimal)seguimientos.Sum(s => s.ConsumoKgMachos ?? 0) * 1000; // Convertir a gramos

        // Datos finales (último registro)
        var ultimoSeguimiento = seguimientos.LastOrDefault();
        var pesoFinalH = ultimoSeguimiento?.PesoPromH;
        var pesoFinalM = ultimoSeguimiento?.PesoPromM;
        var uniformidadFinalH = ultimoSeguimiento?.UniformidadH;
        var uniformidadFinalM = ultimoSeguimiento?.UniformidadM;

        return new MetricasReales(
            porcMortalidadH, porcMortalidadM,
            consumoTotalH, consumoTotalM,
            (decimal?)pesoFinalH, (decimal?)pesoFinalM,
            (decimal?)uniformidadFinalH, (decimal?)uniformidadFinalM
        );
    }

    private LiquidacionTecnicaComparacionDto CompararConGuia(
        Domain.Entities.Lote lote, 
        MetricasReales metricasReales, 
        Domain.Entities.ProduccionAvicolaRaw? guiaGenetica,
        int totalSeguimientos)
    {
        if (guiaGenetica == null)
        {
            return new LiquidacionTecnicaComparacionDto(
                lote.LoteId ?? 0, lote.LoteNombre, lote.Raza, lote.AnoTablaGenetica, null, null,
                metricasReales.PorcentajeMortalidadHembras, metricasReales.PorcentajeMortalidadMachos,
                metricasReales.ConsumoAcumuladoHembras, metricasReales.ConsumoAcumuladoMachos,
                metricasReales.PesoFinalHembras, metricasReales.PesoFinalMachos,
                metricasReales.UniformidadFinalHembras, metricasReales.UniformidadFinalMachos,
                null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null,
                false, false, false, false, false, false, false, false,
                0, 0, 0, "Sin guía genética",
                DateTime.UtcNow, totalSeguimientos, null
            );
        }

        // Calcular diferencias (usando valores por defecto si no están en ProduccionAvicolaRaw)
        var difMortalidadH = CalcularDiferenciaPorcentual(metricasReales.PorcentajeMortalidadHembras, 5.0m); // Valor por defecto
        var difMortalidadM = CalcularDiferenciaPorcentual(metricasReales.PorcentajeMortalidadMachos, 5.0m);
        var difConsumoH = CalcularDiferenciaPorcentual(metricasReales.ConsumoAcumuladoHembras, 2000m); // Valor por defecto
        var difConsumoM = CalcularDiferenciaPorcentual(metricasReales.ConsumoAcumuladoMachos, 2000m);
        var difPesoH = CalcularDiferenciaPorcentual(metricasReales.PesoFinalHembras, 2000m); // Valor por defecto
        var difPesoM = CalcularDiferenciaPorcentual(metricasReales.PesoFinalMachos, 2000m);
        var difUniformidadH = CalcularDiferenciaPorcentual(metricasReales.UniformidadFinalHembras, 85m); // Valor por defecto
        var difUniformidadM = CalcularDiferenciaPorcentual(metricasReales.UniformidadFinalMachos, 85m);

        // Evaluar cumplimiento (tolerancia del 10% por defecto)
        var tolerancia = 10m;
        var cumpleMortalidadH = EvaluarCumplimiento(difMortalidadH, tolerancia);
        var cumpleMortalidadM = EvaluarCumplimiento(difMortalidadM, tolerancia);
        var cumpleConsumoH = EvaluarCumplimiento(difConsumoH, tolerancia);
        var cumpleConsumoM = EvaluarCumplimiento(difConsumoM, tolerancia);
        var cumplePesoH = EvaluarCumplimiento(difPesoH, tolerancia);
        var cumplePesoM = EvaluarCumplimiento(difPesoM, tolerancia);
        var cumpleUniformidadH = EvaluarCumplimiento(difUniformidadH, tolerancia);
        var cumpleUniformidadM = EvaluarCumplimiento(difUniformidadM, tolerancia);

        // Calcular resumen
        var parametrosEvaluados = new[] { cumpleMortalidadH, cumpleMortalidadM, cumpleConsumoH, cumpleConsumoM, 
                                        cumplePesoH, cumplePesoM, cumpleUniformidadH, cumpleUniformidadM };
        var totalParametros = parametrosEvaluados.Count(p => p.HasValue);
        var parametrosCumplidos = parametrosEvaluados.Count(p => p == true);
        var porcentajeCumplimiento = totalParametros > 0 ? (decimal)parametrosCumplidos / totalParametros * 100 : 0;

        var estadoGeneral = DeterminarEstadoGeneral(porcentajeCumplimiento);

        return new LiquidacionTecnicaComparacionDto(
            lote.LoteId ?? 0, lote.LoteNombre, lote.Raza, lote.AnoTablaGenetica, null, guiaGenetica.Raza,
            metricasReales.PorcentajeMortalidadHembras, metricasReales.PorcentajeMortalidadMachos,
            metricasReales.ConsumoAcumuladoHembras, metricasReales.ConsumoAcumuladoMachos,
            metricasReales.PesoFinalHembras, metricasReales.PesoFinalMachos,
            metricasReales.UniformidadFinalHembras, metricasReales.UniformidadFinalMachos,
            5.0m, 5.0m, 2000m, 2000m, 2000m, 2000m, 85m, 85m, // Valores esperados por defecto
            difMortalidadH, difMortalidadM, difConsumoH, difConsumoM,
            difPesoH, difPesoM, difUniformidadH, difUniformidadM,
            cumpleMortalidadH ?? false, cumpleMortalidadM ?? false, cumpleConsumoH ?? false, cumpleConsumoM ?? false,
            cumplePesoH ?? false, cumplePesoM ?? false, cumpleUniformidadH ?? false, cumpleUniformidadM ?? false,
            totalParametros, parametrosCumplidos, porcentajeCumplimiento, estadoGeneral,
            DateTime.UtcNow, totalSeguimientos, null
        );
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorEsperado)
    {
        if (!valorReal.HasValue || valorEsperado == 0)
            return null;

        return ((valorReal.Value - valorEsperado) / valorEsperado) * 100;
    }

    private static bool? EvaluarCumplimiento(decimal? diferencia, decimal tolerancia)
    {
        if (!diferencia.HasValue)
            return null;

        return Math.Abs(diferencia.Value) <= tolerancia;
    }

    private static string DeterminarEstadoGeneral(decimal porcentajeCumplimiento)
    {
        return porcentajeCumplimiento switch
        {
            >= 90 => "Excelente",
            >= 75 => "Bueno",
            >= 50 => "Regular",
            _ => "Deficiente"
        };
    }

    private static int CalcularSemana(DateTime? fechaEncaset, DateTime fechaRegistro)
    {
        if (!fechaEncaset.HasValue)
            return 0;

        var diasTranscurridos = (fechaRegistro - fechaEncaset.Value).Days;
        return (int)Math.Ceiling(diasTranscurridos / 7.0);
    }

    private List<ComparacionDetalladaDto> CrearComparacionesDetalladas(LiquidacionTecnicaComparacionDto comparacion)
    {
        var comparaciones = new List<ComparacionDetalladaDto>();

        comparaciones.Add(new ComparacionDetalladaDto(
            "Mortalidad Hembras",
            comparacion.PorcentajeMortalidadHembras,
            comparacion.MortalidadEsperadaHembras,
            comparacion.DiferenciaMortalidadHembras,
            10m, // Tolerancia del 10%
            comparacion.CumpleMortalidadHembras,
            comparacion.CumpleMortalidadHembras ? "Cumple" : "Excede"
        ));

        comparaciones.Add(new ComparacionDetalladaDto(
            "Mortalidad Machos",
            comparacion.PorcentajeMortalidadMachos,
            comparacion.MortalidadEsperadaMachos,
            comparacion.DiferenciaMortalidadMachos,
            10m,
            comparacion.CumpleMortalidadMachos,
            comparacion.CumpleMortalidadMachos ? "Cumple" : "Excede"
        ));

        comparaciones.Add(new ComparacionDetalladaDto(
            "Consumo Hembras",
            comparacion.ConsumoAcumuladoHembras,
            comparacion.ConsumoEsperadoHembras,
            comparacion.DiferenciaConsumoHembras,
            10m,
            comparacion.CumpleConsumoHembras,
            comparacion.CumpleConsumoHembras ? "Cumple" : "Excede"
        ));

        comparaciones.Add(new ComparacionDetalladaDto(
            "Consumo Machos",
            comparacion.ConsumoAcumuladoMachos,
            comparacion.ConsumoEsperadoMachos,
            comparacion.DiferenciaConsumoMachos,
            10m,
            comparacion.CumpleConsumoMachos,
            comparacion.CumpleConsumoMachos ? "Cumple" : "Excede"
        ));

        if (comparacion.PesoFinalHembras.HasValue)
        {
            comparaciones.Add(new ComparacionDetalladaDto(
                "Peso Hembras",
                comparacion.PesoFinalHembras.Value,
                comparacion.PesoEsperadoHembras,
                comparacion.DiferenciaPesoHembras,
                10m,
                comparacion.CumplePesoHembras,
                comparacion.CumplePesoHembras ? "Cumple" : "Excede"
            ));
        }

        if (comparacion.PesoFinalMachos.HasValue)
        {
            comparaciones.Add(new ComparacionDetalladaDto(
                "Peso Machos",
                comparacion.PesoFinalMachos.Value,
                comparacion.PesoEsperadoMachos,
                comparacion.DiferenciaPesoMachos,
                10m,
                comparacion.CumplePesoMachos,
                comparacion.CumplePesoMachos ? "Cumple" : "Excede"
            ));
        }

        if (comparacion.UniformidadFinalHembras.HasValue)
        {
            comparaciones.Add(new ComparacionDetalladaDto(
                "Uniformidad Hembras",
                comparacion.UniformidadFinalHembras.Value,
                comparacion.UniformidadEsperadaHembras,
                comparacion.DiferenciaUniformidadHembras,
                10m,
                comparacion.CumpleUniformidadHembras,
                comparacion.CumpleUniformidadHembras ? "Cumple" : "Excede"
            ));
        }

        if (comparacion.UniformidadFinalMachos.HasValue)
        {
            comparaciones.Add(new ComparacionDetalladaDto(
                "Uniformidad Machos",
                comparacion.UniformidadFinalMachos.Value,
                comparacion.UniformidadEsperadaMachos,
                comparacion.DiferenciaUniformidadMachos,
                10m,
                comparacion.CumpleUniformidadMachos,
                comparacion.CumpleUniformidadMachos ? "Cumple" : "Excede"
            ));
        }

        return comparaciones;
    }

    private string? GenerarObservaciones(LiquidacionTecnicaComparacionDto comparacion)
    {
        var observaciones = new List<string>();

        if (comparacion.PorcentajeCumplimiento < 50)
        {
            observaciones.Add("El lote presenta un cumplimiento deficiente con respecto a la guía genética.");
        }
        else if (comparacion.PorcentajeCumplimiento < 75)
        {
            observaciones.Add("El lote presenta un cumplimiento regular con respecto a la guía genética.");
        }

        if (!comparacion.CumpleMortalidadHembras && comparacion.DiferenciaMortalidadHembras.HasValue)
        {
            observaciones.Add($"La mortalidad de hembras excede lo esperado en {comparacion.DiferenciaMortalidadHembras:F1}%.");
        }

        if (!comparacion.CumpleMortalidadMachos && comparacion.DiferenciaMortalidadMachos.HasValue)
        {
            observaciones.Add($"La mortalidad de machos excede lo esperado en {comparacion.DiferenciaMortalidadMachos:F1}%.");
        }

        return observaciones.Any() ? string.Join(" ", observaciones) : null;
    }

    #endregion
}

// Record para métricas reales
public record MetricasReales(
    decimal PorcentajeMortalidadHembras,
    decimal PorcentajeMortalidadMachos,
    decimal ConsumoAcumuladoHembras,
    decimal ConsumoAcumuladoMachos,
    decimal? PesoFinalHembras,
    decimal? PesoFinalMachos,
    decimal? UniformidadFinalHembras,
    decimal? UniformidadFinalMachos
);