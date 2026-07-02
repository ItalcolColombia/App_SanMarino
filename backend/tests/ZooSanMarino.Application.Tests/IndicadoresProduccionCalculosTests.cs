using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del cálculo puro que arma la respuesta de indicadores de producción a partir de las filas
/// de la fn SQL. El cálculo numérico por semana vive en la BD y su equivalencia con el algoritmo C#
/// previo se verificó campo a campo (P-K345A/B, 43 semanas, 0 diferencias, eps 1e-6). Aquí se cubre
/// el mapeo double→decimal (sin pérdida) y la lógica de metadatos de la respuesta.
/// </summary>
public class IndicadoresProduccionCalculosTests
{
    private static IndicadorProduccionSemanalBdRow SampleRow(int semana = 26) => new()
    {
        Semana = semana,
        FechaInicioSemana = new DateTime(2025, 7, 22),
        FechaFinSemana = new DateTime(2025, 7, 28),
        TotalRegistros = 7,
        MortalidadHembras = 5,
        MortalidadMachos = 2,
        PorcentajeMortalidadHembras = 0.0658154534684744,
        PorcentajeMortalidadMachos = 0.2597402597402597,
        MortalidadGuiaHembras = 0.33,
        MortalidadGuiaMachos = 0.0112485679392769,
        DiferenciaMortalidadHembras = -80.05592319137139,
        DiferenciaMortalidadMachos = 2209.0962435610877,
        SeleccionHembras = 0,
        PorcentajeSeleccionHembras = 0,
        ConsumoKgHembras = 6835.1,
        ConsumoKgMachos = 646.9,
        ConsumoTotalKg = 7482.0,
        ConsumoPromedioDiarioKg = 1068.857142857143,
        ConsumoGuiaHembras = 132,
        ConsumoGuiaMachos = 127,
        DiferenciaConsumoHembras = -2.6927864733705302,
        DiferenciaConsumoMachos = -5.742028360444582,
        HuevosTotales = 12044,
        HuevosIncubables = 7905,
        PromedioHuevosPorDia = 1720.5714285714287,
        EficienciaProduccion = 22.648037759265875,
        HuevosTotalesGuia = 2.18028125,
        HuevosIncubablesGuia = 1.10104203125,
        PorcentajeProduccionGuia = 31.25,
        DiferenciaHuevosTotales = -27.286324039680142,
        DiferenciaHuevosIncubables = -5.494768609762804,
        DiferenciaPorcentajeProduccion = -27.5262791703492,
        PesoHuevoPromedio = 50.4571428571429,
        PesoHuevoGuia = 52.3,
        DiferenciaPesoHuevo = -3.523627424200956,
        PesoPromedioHembras = 3.3414,
        PesoPromedioMachos = 4.0256,
        PesoGuiaHembras = 3.235,
        PesoGuiaMachos = 3.96,
        DiferenciaPesoHembras = 3.2890262751159196,
        DiferenciaPesoMachos = 1.6565656565656566,
        UniformidadPromedio = null,
        UniformidadGuia = 0,
        DiferenciaUniformidad = null,
        CoeficienteVariacionPromedio = null,
        HuevosLimpios = 7166,
        HuevosTratados = 739,
        HuevosSucios = 191,
        HuevosDeformes = 50,
        HuevosBlancos = 0,
        HuevosDobleYema = 315,
        HuevosPiso = 613,
        HuevosPequenos = 2784,
        HuevosRotos = 83,
        HuevosDesecho = 103,
        HuevosOtro = 0,
        AvesHembrasInicioSemana = 7602,
        AvesMachosInicioSemana = 772,
        AvesHembrasFinSemana = 7592,
        AvesMachosFinSemana = 768,
        HtaaReal = 1.5853626431486112,
        HiaaReal = 1.0405423193365801,
    };

    [Fact]
    public void MapRow_CopiaTodosLosCamposEnteros()
    {
        var r = SampleRow();
        var d = IndicadoresProduccionCalculos.MapRow(r);

        Assert.Equal(26, d.Semana);
        Assert.Equal(7, d.TotalRegistros);
        Assert.Equal(5, d.MortalidadHembras);
        Assert.Equal(2, d.MortalidadMachos);
        Assert.Equal(0, d.SeleccionHembras);
        Assert.Equal(12044, d.HuevosTotales);
        Assert.Equal(7905, d.HuevosIncubables);
        Assert.Equal(7166, d.HuevosLimpios);
        Assert.Equal(2784, d.HuevosPequenos);
        Assert.Equal(7602, d.AvesHembrasInicioSemana);
        Assert.Equal(772, d.AvesMachosInicioSemana);
        Assert.Equal(7592, d.AvesHembrasFinSemana);
        Assert.Equal(768, d.AvesMachosFinSemana);
        Assert.Equal(new DateTime(2025, 7, 22), d.FechaInicioSemana);
        Assert.Equal(new DateTime(2025, 7, 28), d.FechaFinSemana);
    }

    [Fact]
    public void MapRow_ConvierteDoubleADecimalSinPerder()
    {
        var r = SampleRow();
        var d = IndicadoresProduccionCalculos.MapRow(r);

        Assert.Equal((decimal)r.PorcentajeMortalidadHembras, d.PorcentajeMortalidadHembras);
        Assert.Equal((decimal)r.EficienciaProduccion, d.EficienciaProduccion);
        Assert.Equal((decimal)r.ConsumoTotalKg, d.ConsumoTotalKg);
        Assert.Equal((decimal)r.HtaaReal, d.HtaaReal);
        Assert.Equal((decimal)r.HiaaReal, d.HiaaReal);
        Assert.Equal((decimal)r.PesoPromedioHembras!.Value, d.PesoPromedioHembras);
    }

    [Fact]
    public void MapRow_NullDeLaFnQuedaNullEnElDto()
    {
        var r = SampleRow();
        var d = IndicadoresProduccionCalculos.MapRow(r);

        Assert.Null(d.UniformidadPromedio);       // fn devolvió NULL
        Assert.Null(d.DiferenciaUniformidad);     // guía = 0 → dif null
        Assert.Null(d.CoeficienteVariacionPromedio);
        Assert.Equal(0m, d.UniformidadGuia);      // guía 0 sí se mapea (no null)
    }

    [Fact]
    public void MapRow_GuiaPresenteMapeaValoresGuia()
    {
        var r = SampleRow();
        var d = IndicadoresProduccionCalculos.MapRow(r);

        Assert.Equal(0.33m, d.MortalidadGuiaHembras);
        Assert.Equal(132m, d.ConsumoGuiaHembras);
        Assert.Equal(31.25m, d.PorcentajeProduccionGuia);
        Assert.Equal(3.235m, d.PesoGuiaHembras);
        Assert.Equal(2.18028125m, d.HuevosTotalesGuia);
    }

    [Fact]
    public void BuildResponse_CalculaSemanaInicialYFinal()
    {
        var rows = new List<IndicadorProduccionSemanalBdRow>
        {
            SampleRow(26), SampleRow(27), SampleRow(30)
        };

        var resp = IndicadoresProduccionCalculos.BuildResponse(rows, true, true, "AP", 2026);

        Assert.Equal(3, resp.TotalSemanas);
        Assert.Equal(3, resp.Indicadores.Count);
        Assert.Equal(26, resp.SemanaInicial);
        Assert.Equal(30, resp.SemanaFinal);
        Assert.True(resp.TieneDatosGuiaGenetica);
        Assert.Null(resp.MensajeGuiaGenetica);
    }

    [Fact]
    public void BuildResponse_SinFilas_RespuestaVacia()
    {
        var resp = IndicadoresProduccionCalculos.BuildResponse(
            new List<IndicadorProduccionSemanalBdRow>(), false, false, "", null);

        Assert.Empty(resp.Indicadores);
        Assert.Equal(0, resp.TotalSemanas);
        Assert.Equal(0, resp.SemanaInicial);
        Assert.Equal(0, resp.SemanaFinal);
        Assert.False(resp.TieneDatosGuiaGenetica);
        Assert.Null(resp.MensajeGuiaGenetica);
    }

    [Fact]
    public void BuildResponse_ConRazaYAnoSinDatosGuia_MensajeInformativo()
    {
        var rows = new List<IndicadorProduccionSemanalBdRow> { SampleRow(26) };

        // tieneGuia = true (raza+año) pero hayFilasGuia = false
        var resp = IndicadoresProduccionCalculos.BuildResponse(rows, true, false, "AP", 2026);

        Assert.False(resp.TieneDatosGuiaGenetica);
        Assert.NotNull(resp.MensajeGuiaGenetica);
        Assert.Contains("AP", resp.MensajeGuiaGenetica!);
        Assert.Contains("2026", resp.MensajeGuiaGenetica!);
    }

    [Fact]
    public void BuildResponse_SinGuia_NoGeneraMensaje()
    {
        var rows = new List<IndicadorProduccionSemanalBdRow> { SampleRow(26) };

        var resp = IndicadoresProduccionCalculos.BuildResponse(rows, false, false, "", null);

        Assert.False(resp.TieneDatosGuiaGenetica);
        Assert.Null(resp.MensajeGuiaGenetica);
    }
}
