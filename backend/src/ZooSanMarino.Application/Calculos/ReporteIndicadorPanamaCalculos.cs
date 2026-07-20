// Cálculo puro del consolidado de una CORRIDA de liquidación Panamá.
// Réplica EXACTA de las fórmulas de fn_reporte_indicadores_panama aplicadas sobre los
// insumos SUMADOS de los lotes/galpones de la corrida (mismos guards de división por cero).
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

public static class ReporteIndicadorPanamaCalculos
{
    /// <summary>Factor de la fn para pasar el consumo de quintales (qq) a kilogramos.</summary>
    private const decimal KgPorQuintal = 45.36m;

    /// <summary>Factor libra/kilo de la fn (eficiencia americana y productividad).</summary>
    private const decimal LibrasPorKilo = 2.2046m;

    /// <summary>
    /// Consolida los reportes por lote/galpón de una corrida en un único reporte:
    /// suma los insumos y agregados crudos y recalcula los derivados con las MISMAS fórmulas
    /// de la fn. Días de engorde / en granja = promedio ponderado por aves encasetadas
    /// (redondeado a entero; si no hay encasetadas, promedio simple). Null si la lista está vacía.
    /// </summary>
    public static ReporteIndicadoresPanamaDto? ConsolidarCorrida(IReadOnlyList<ReporteIndicadoresPanamaDto> reportes)
    {
        if (reportes is null || reportes.Count == 0) return null;

        // ── Sumas de insumos digitados y agregados del seguimiento ──
        decimal metrosCuadrados   = reportes.Sum(r => r.Liquidacion.MetrosCuadrados);
        decimal avesFinalGranja   = reportes.Sum(r => r.Liquidacion.AvesFinalGranja);
        decimal produccionKiloPie = reportes.Sum(r => r.Liquidacion.ProduccionKiloPie);
        int     avesBeneficiada   = reportes.Sum(r => r.Liquidacion.AvesBeneficiada);
        int     avesEncasetadas   = reportes.Sum(r => r.AvesEncasetadas);
        decimal consumoQq         = reportes.Sum(r => r.InfoProductiva.ConsumoAlimentoTotal);
        decimal totalSeleccion    = reportes.Sum(r => r.InfoProductiva.TotalAvesSeleccion);
        decimal totalMuertas      = reportes.Sum(r => r.InfoProductiva.TotalAvesMuertas);
        decimal consumoKgTotal    = consumoQq * KgPorQuintal;

        int diasEngorde  = PromedioPonderadoDias(reportes, r => r.Liquidacion.DiasEngorde);
        int diasEnGranja = PromedioPonderadoDias(reportes, r => r.Liquidacion.DiasEnGranja);

        // ── Derivados (mismas fórmulas y guards que la fn) ──
        decimal pesoPromedio   = avesBeneficiada   > 0 ? produccionKiloPie / avesBeneficiada        : 0m;
        decimal mortalidadPorc = avesEncasetadas   > 0 ? totalMuertas    / avesEncasetadas * 100m   : 0m;
        decimal seleccionPorc  = avesEncasetadas   > 0 ? totalSeleccion  / avesEncasetadas * 100m   : 0m;
        decimal conversion     = produccionKiloPie > 0 ? consumoKgTotal / produccionKiloPie         : 0m;
        decimal consumoAve     = avesBeneficiada   > 0 ? consumoKgTotal / avesBeneficiada           : 0m;

        decimal porcMortalidadTotal = mortalidadPorc + seleccionPorc;
        decimal supervivencia       = 100m - porcMortalidadTotal;

        decimal eficienciaAmericana = diasEngorde > 0 ? pesoPromedio * LibrasPorKilo / diasEngorde : 0m;
        decimal eef = diasEngorde > 0 && conversion > 0
            ? produccionKiloPie * supervivencia / (diasEngorde * conversion) * 100m : 0m;
        decimal eefDos = diasEngorde > 0 && conversion > 0
            ? pesoPromedio * supervivencia / (diasEngorde * conversion) * 100m : 0m;
        decimal avesMetrosCua  = metrosCuadrados > 0 ? avesBeneficiada   / metrosCuadrados : 0m;
        decimal kilosMetrosCua = metrosCuadrados > 0 ? produccionKiloPie / metrosCuadrados : 0m;
        decimal productividad = diasEngorde > 0 && conversion > 0
            ? (pesoPromedio * LibrasPorKilo / diasEngorde) / conversion : 0m;
        decimal faltanteSobra = avesFinalGranja - avesBeneficiada;

        return new ReporteIndicadoresPanamaDto(
            new LiquidacionPanamaDto(
                Id: 0,
                IdUsuarioRegistro: null,
                IdLote: 0,
                MetrosCuadrados: metrosCuadrados,
                AvesFinalGranja: avesFinalGranja,
                ProduccionKiloPie: produccionKiloPie,
                DiasEngorde: diasEngorde,
                DiasEnGranja: diasEnGranja,
                AvesBeneficiada: avesBeneficiada,
                PesoPromedio: pesoPromedio,
                MortalidadPorc: mortalidadPorc,
                SeleccionPorc: seleccionPorc,
                PorcMortalidadTotal: porcMortalidadTotal,
                Supervivencia: supervivencia,
                ConsumoAve: consumoAve,
                Conversion: conversion,
                EficienciaAmericana: eficienciaAmericana,
                EeF: eef,
                EefDos: eefDos,
                AvesMetrosCua: avesMetrosCua,
                KilosMetrosCua: kilosMetrosCua,
                Productividad: productividad,
                FaltanteSobra: faltanteSobra),
            new InfoProductivaPanamaDto(consumoQq, totalSeleccion, totalMuertas),
            avesEncasetadas);
    }

    /// <summary>
    /// Días consolidados de la corrida: promedio ponderado por aves encasetadas del lote,
    /// redondeado a entero (AwayFromZero). Sin aves encasetadas ⇒ promedio simple.
    /// </summary>
    private static int PromedioPonderadoDias(
        IReadOnlyList<ReporteIndicadoresPanamaDto> reportes,
        Func<ReporteIndicadoresPanamaDto, int> dias)
    {
        decimal pesoTotal = reportes.Sum(r => (decimal)r.AvesEncasetadas);
        if (pesoTotal > 0)
        {
            decimal ponderado = reportes.Sum(r => dias(r) * (decimal)r.AvesEncasetadas) / pesoTotal;
            return (int)Math.Round(ponderado, MidpointRounding.AwayFromZero);
        }
        decimal simple = (decimal)reportes.Sum(dias) / reportes.Count;
        return (int)Math.Round(simple, MidpointRounding.AwayFromZero);
    }
}
