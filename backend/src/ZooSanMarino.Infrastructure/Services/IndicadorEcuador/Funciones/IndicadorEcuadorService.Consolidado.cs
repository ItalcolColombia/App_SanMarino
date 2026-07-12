// Agregación consolidada de indicadores de todas las granjas (vista General).
// Partial de IndicadorEcuadorService.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class IndicadorEcuadorService
{
    public async Task<IndicadorEcuadorConsolidadoDto> CalcularConsolidadoAsync(IndicadorEcuadorRequest request)
    {
        var indicadores = await CalcularIndicadoresAsync(request);
        var indicadoresList = indicadores.ToList();

        if (!indicadoresList.Any())
        {
            return new IndicadorEcuadorConsolidadoDto(
                DateTime.UtcNow,  // FechaCalculo
                0,  // TotalGranjas
                0,  // TotalLotes
                0,  // TotalLotesCerrados
                0,  // TotalAvesEncasetadas
                0,  // TotalAvesSacrificadas
                0,  // TotalMortalidad
                0m, // PromedioMortalidadPorcentaje
                0m, // PromedioSupervivenciaPorcentaje
                0m, // TotalConsumoAlimentoKg
                0m, // PromedioConsumoAveGramos
                0m, // TotalKgCarnePollos
                0m, // PromedioPesoKilos
                0m, // PromedioConversion
                0m, // PromedioConversionAjustada
                0m, // PromedioEdad
                0m, // TotalMetrosCuadrados
                0m, // PromedioAvesPorMetroCuadrado
                0m, // PromedioKgPorMetroCuadrado
                0m, // PromedioEficienciaAmericana
                0m, // PromedioEficienciaEuropea
                0m, // PromedioIndiceProductividad
                0m, // PromedioGananciaDia
                Enumerable.Empty<IndicadorEcuadorDto>() // IndicadoresPorGranja
            );
        }

        var totalGranjas = indicadoresList.Select(i => i.GranjaId).Distinct().Count();
        var totalLotes = indicadoresList.Count;
        var totalLotesCerrados = indicadoresList.Count(i => i.LoteCerrado);

        // Totales (conglomerado de todas las granjas)
        var totalAvesEncasetadas = indicadoresList.Sum(i => i.AvesEncasetadas);
        var totalAvesSacrificadas = indicadoresList.Sum(i => i.AvesSacrificadas);
        var totalMortalidad = indicadoresList.Sum(i => i.Mortalidad);
        var totalConsumoAlimento = indicadoresList.Sum(i => i.ConsumoTotalAlimentoKg);
        var totalKgCarne = indicadoresList.Sum(i => i.KgCarnePollos);
        var totalMetrosCuadrados = indicadoresList.Sum(i => i.MetrosCuadrados);

        // Indicadores consolidados calculados desde totales (fórmulas LIQUIDACIÓN TÉCNICA)
        var promedioMortalidad = totalAvesEncasetadas > 0
            ? (decimal)totalMortalidad / totalAvesEncasetadas * 100
            : 0;
        var supervivenciaPorcentajeConsolidado = totalAvesEncasetadas > 0
            ? (decimal)(totalAvesEncasetadas - totalMortalidad) / totalAvesEncasetadas * 100
            : 0;
        var promedioConsumoAve = totalAvesSacrificadas > 0
            ? totalConsumoAlimento / totalAvesSacrificadas * 1000  // Consumo ave (g)
            : 0;
        var promedioPeso = totalAvesSacrificadas > 0
            ? totalKgCarne / totalAvesSacrificadas  // Peso promedio Kilos
            : 0;
        var conversionConsolidada = totalKgCarne > 0
            ? totalConsumoAlimento / totalKgCarne  // Conversion = Consumo total / Kg Carne
            : 0;
        var pesoAjuste = indicadoresList.FirstOrDefault()?.PesoAjusteVariable ?? PesoAjusteDefault;
        var divisorAjuste = indicadoresList.FirstOrDefault()?.DivisorAjusteVariable ?? DivisorAjusteDefault;
        var promedioConversionAjustada = CalcularConversionAjustada(conversionConsolidada, promedioPeso, pesoAjuste, divisorAjuste);
        // Edad = promedio de los saques de pollo (galpones/lotes)
        var lotesConEdad = indicadoresList.Where(i => i.EdadPromedio > 0).ToList();
        var promedioEdad = lotesConEdad.Any()
            ? lotesConEdad.Average(i => i.EdadPromedio)
            : 0;
        var promedioAvesPorM2 = totalMetrosCuadrados > 0
            ? totalAvesSacrificadas / totalMetrosCuadrados  // Aves / M²
            : 0;
        var promedioKgPorM2 = totalMetrosCuadrados > 0
            ? totalKgCarne / totalMetrosCuadrados  // KG/M²
            : 0;
        var promedioEficienciaAmericana = conversionConsolidada > 0
            ? (promedioPeso / conversionConsolidada) * 100  // (Peso Promedio / Conversion) * 100
            : 0;
        var promedioEficienciaEuropea = (conversionConsolidada > 0 && promedioEdad > 0)
            ? ((promedioPeso * supervivenciaPorcentajeConsolidado) / (promedioEdad * conversionConsolidada)) * 100  // Eficiencia Europea
            : 0;
        var promedioIndiceProductividad = conversionConsolidada > 0
            ? (promedioPeso / conversionConsolidada) / conversionConsolidada * 100  // I. Productividad
            : 0;
        var promedioGananciaDia = promedioEdad > 0
            ? (promedioPeso / promedioEdad) * 1000  // Ganancia Día
            : 0;

        return new IndicadorEcuadorConsolidadoDto(
            DateTime.UtcNow,
            totalGranjas,
            totalLotes,
            totalLotesCerrados,
            totalAvesEncasetadas,
            totalAvesSacrificadas,
            totalMortalidad,
            promedioMortalidad,
            supervivenciaPorcentajeConsolidado,
            totalConsumoAlimento,
            promedioConsumoAve,
            totalKgCarne,
            promedioPeso,
            conversionConsolidada,
            promedioConversionAjustada,
            promedioEdad,
            totalMetrosCuadrados,
            promedioAvesPorM2,
            promedioKgPorM2,
            promedioEficienciaAmericana,
            promedioEficienciaEuropea,
            promedioIndiceProductividad,
            promedioGananciaDia,
            indicadoresList,
            // Totales de mermas, ajuste y sobrante (R1-R2). NULL = lote sin merma registrada (no suma).
            indicadoresList.Sum(i => i.MermaUnidades ?? 0),
            indicadoresList.Sum(i => i.MermaKilos ?? 0m),
            indicadoresList.Sum(i => i.AjusteAves ?? 0),
            indicadoresList.Sum(i => i.ProduccionKiloEnPie),
            indicadoresList.Sum(i => i.TotalKilosDespachadosCliente ?? 0m),
            indicadoresList.Sum(i => i.AvesSobrante)
        );
    }
}
