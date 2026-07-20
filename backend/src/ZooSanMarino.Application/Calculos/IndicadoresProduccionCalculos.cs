using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo puro (sin EF ni estado) del armado de la respuesta de indicadores de producción a
/// partir de las filas que devuelve la función SQL <c>fn_indicadores_produccion_postura</c>.
/// El cálculo numérico por semana vive en la BD (misma aritmética que el C# previo, verificada
/// por test de equivalencia); aquí solo se mapea la fila cruda al contrato público
/// <see cref="IndicadorProduccionSemanalDto"/> (double → decimal, sin alterar valores) y se
/// construyen los metadatos de la respuesta. Determinista y testeable.
/// </summary>
public static class IndicadoresProduccionCalculos
{
    /// <summary>
    /// Mapea una fila cruda de la fn SQL al DTO público. La conversión double→decimal preserva el
    /// valor (los indicadores no se redondean; el redondeo lo hace la capa de presentación).
    /// </summary>
    public static IndicadorProduccionSemanalDto MapRow(IndicadorProduccionSemanalBdRow r) =>
        new IndicadorProduccionSemanalDto(
            r.Semana,
            r.FechaInicioSemana,
            r.FechaFinSemana,
            r.TotalRegistros,
            r.MortalidadHembras,
            r.MortalidadMachos,
            (decimal)r.PorcentajeMortalidadHembras,
            (decimal)r.PorcentajeMortalidadMachos,
            Dec(r.MortalidadGuiaHembras),
            Dec(r.MortalidadGuiaMachos),
            Dec(r.DiferenciaMortalidadHembras),
            Dec(r.DiferenciaMortalidadMachos),
            r.SeleccionHembras,
            (decimal)r.PorcentajeSeleccionHembras,
            (decimal)r.ConsumoKgHembras,
            (decimal)r.ConsumoKgMachos,
            (decimal)r.ConsumoTotalKg,
            (decimal)r.ConsumoPromedioDiarioKg,
            Dec(r.ConsumoGuiaHembras),
            Dec(r.ConsumoGuiaMachos),
            Dec(r.DiferenciaConsumoHembras),
            Dec(r.DiferenciaConsumoMachos),
            r.HuevosTotales,
            r.HuevosIncubables,
            (decimal)r.PromedioHuevosPorDia,
            (decimal)r.EficienciaProduccion,
            Dec(r.HuevosTotalesGuia),
            Dec(r.HuevosIncubablesGuia),
            Dec(r.PorcentajeProduccionGuia),
            Dec(r.DiferenciaHuevosTotales),
            Dec(r.DiferenciaHuevosIncubables),
            Dec(r.DiferenciaPorcentajeProduccion),
            Dec(r.PesoHuevoPromedio),
            Dec(r.PesoHuevoGuia),
            Dec(r.DiferenciaPesoHuevo),
            Dec(r.PesoPromedioHembras),
            Dec(r.PesoPromedioMachos),
            Dec(r.PesoGuiaHembras),
            Dec(r.PesoGuiaMachos),
            Dec(r.DiferenciaPesoHembras),
            Dec(r.DiferenciaPesoMachos),
            Dec(r.UniformidadPromedio),
            Dec(r.UniformidadGuia),
            Dec(r.DiferenciaUniformidad),
            Dec(r.CoeficienteVariacionPromedio),
            r.HuevosLimpios,
            r.HuevosTratados,
            r.HuevosSucios,
            r.HuevosDeformes,
            r.HuevosBlancos,
            r.HuevosDobleYema,
            r.HuevosPiso,
            r.HuevosPequenos,
            r.HuevosRotos,
            r.HuevosDesecho,
            r.HuevosOtro,
            r.AvesHembrasInicioSemana,
            r.AvesMachosInicioSemana,
            r.AvesHembrasFinSemana,
            r.AvesMachosFinSemana,
            (decimal)r.HtaaReal,
            (decimal)r.HiaaReal,
            // REQ-004: %Retiro real (mismo double→decimal sin alterar el valor)
            (decimal)r.RetiroSemH,
            (decimal)r.RetiroSemM,
            (decimal)r.RetiroAcH,
            (decimal)r.RetiroAcM,
            // Verenice rev 6-jul-26: %Retiro acumulado de guía por sexo (NULL si la fn no la tiene)
            Dec(r.RetiroAcHGuia),
            Dec(r.RetiroAcMGuia));

    /// <summary>
    /// Construye la respuesta a partir de las filas de la fn y del estado de la guía genética.
    /// Replica la lógica de <c>CalcularIndicadoresAsync</c>: semanaInicial/Final = min/max de las
    /// semanas devueltas; tieneDatosGuia = hay guía + al menos una fila de guía; mensaje cuando hay
    /// raza/año pero no datos de guía cargados.
    /// </summary>
    public static IndicadoresProduccionResponse BuildResponse(
        IReadOnlyList<IndicadorProduccionSemanalBdRow> rows,
        bool tieneGuiaGenetica,
        bool hayFilasGuia,
        string raza,
        int? anoTablaGenetica)
    {
        var indicadores = new List<IndicadorProduccionSemanalDto>(rows.Count);
        foreach (var r in rows)
            indicadores.Add(MapRow(r));

        var semanaInicial = 0;
        var semanaFinal = 0;
        if (rows.Count > 0)
        {
            semanaInicial = int.MaxValue;
            foreach (var r in rows)
            {
                if (r.Semana < semanaInicial) semanaInicial = r.Semana;
                if (r.Semana > semanaFinal) semanaFinal = r.Semana;
            }
        }

        var tieneDatosGuia = tieneGuiaGenetica && hayFilasGuia;
        string? mensajeGuia = null;
        if (tieneGuiaGenetica && !hayFilasGuia)
            mensajeGuia = $"El lote tiene Raza ({raza}) y Año genético ({anoTablaGenetica}) pero no hay datos de guía cargados para esa combinación en su compañía. Cargue la guía genética (produccion_avicola_raw) para Raza/Ano correspondiente.";

        return new IndicadoresProduccionResponse(
            indicadores,
            indicadores.Count,
            semanaInicial,
            semanaFinal,
            tieneDatosGuia,
            mensajeGuia);
    }

    private static decimal? Dec(double? v) => v.HasValue ? (decimal)v.Value : null;
}
