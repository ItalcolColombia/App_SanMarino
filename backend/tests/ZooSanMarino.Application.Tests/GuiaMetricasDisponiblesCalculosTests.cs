using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Qué columnas de comparación contra la guía tienen dato real.
///
/// <para>La guía propia (<c>guia_genetica_santa_reyes</c>) sólo trae 3 métricas —medido el
/// 1-sep-2026: <c>prod_porcentaje</c>, <c>retiro_ac_h</c>, <c>gr_ave_dia_h</c>— y
/// <c>GuiaGeneticaLookup.ATransitoria</c> deja el resto en <c>null</c>. La guía compartida se
/// informa completa sin inspeccionar, para no cambiarle nada a quien no tiene guía propia.</para>
/// </summary>
public class GuiaMetricasDisponiblesCalculosTests
{
    /// <summary>Fila como la deja <c>ATransitoria</c> desde la guía propia: 3 métricas, el resto null.</summary>
    private static FilaGuiaMetricas FilaGuiaPropia(
        string? prodPorcentaje = "78,5", string? retiroAcH = "2,1", string? grAveDiaH = "112") =>
        new(ProdPorcentaje: prodPorcentaje,
            PesoHuevo: null, HTotalAa: null, Uniformidad: null,
            PesoH: null, PesoM: null, MortSemH: null, MortSemM: null,
            RetiroAcH: retiroAcH, RetiroAcM: null, ConsAcH: null, ConsAcM: null,
            GrAveDiaH: grAveDiaH, GrAveDiaM: null);

    private static FilaGuiaMetricas FilaCompleta() =>
        new("78,5", "62,3", "180,4", "88", "1,85", "2,9", "0,12", "0,15",
            "2,1", "2,4", "7800", "8100", "112", "128");

    // ── Guía propia: sólo lo que tiene ──────────────────────────────────────────────────────────

    [Fact]
    public void GuiaPropia_marca_disponibles_exactamente_las_3_metricas_que_tiene()
    {
        var d = GuiaMetricasDisponiblesCalculos.Detectar(new[] { FilaGuiaPropia(), FilaGuiaPropia() });

        Assert.True(d.ProdPorcentaje);
        Assert.True(d.RetiroAcH);
        Assert.True(d.GrAveDiaH);

        Assert.False(d.PesoHuevo);
        Assert.False(d.HTotalAa);
        Assert.False(d.Uniformidad);
        Assert.False(d.PesoH);
        Assert.False(d.PesoM);
        Assert.False(d.MortSemH);
        Assert.False(d.MortSemM);
        Assert.False(d.RetiroAcM);
        Assert.False(d.ConsAcH);
        Assert.False(d.ConsAcM);
        Assert.False(d.GrAveDiaM);
    }

    /// <summary>
    /// Basta UNA fila con dato en 100 para que la columna se pinte: la línea Criolla no trae
    /// producción desde la semana 101 (83 de 123 filas con dato, medido), y esa columna sí existe.
    /// </summary>
    [Fact]
    public void Una_sola_fila_con_dato_alcanza_para_marcar_la_metrica()
    {
        var filas = Enumerable.Range(0, 99).Select(_ => FilaGuiaPropia(prodPorcentaje: null)).ToList();
        filas.Add(FilaGuiaPropia(prodPorcentaje: "45,2"));

        Assert.True(GuiaMetricasDisponiblesCalculos.Detectar(filas).ProdPorcentaje);
    }

    [Fact]
    public void Todas_las_filas_sin_dato_deja_la_metrica_fuera()
    {
        var filas = Enumerable.Range(0, 50).Select(_ => FilaGuiaPropia(prodPorcentaje: null)).ToList();

        Assert.False(GuiaMetricasDisponiblesCalculos.Detectar(filas).ProdPorcentaje);
    }

    // ── Guía compartida: todo, sin inspeccionar ─────────────────────────────────────────────────

    [Fact]
    public void GuiaCompartida_informa_todas_aunque_las_filas_esten_incompletas()
    {
        var d = GuiaMetricasDisponiblesCalculos.Resolver(
            guiaEsPropia: false,
            filas: new[] { FilaGuiaPropia() }); // filas pobres a propósito

        Assert.Equal(GuiaMetricasDisponiblesCalculos.Todas, d);
    }

    [Fact]
    public void GuiaCompartida_informa_todas_incluso_sin_filas()
    {
        Assert.Equal(
            GuiaMetricasDisponiblesCalculos.Todas,
            GuiaMetricasDisponiblesCalculos.Resolver(guiaEsPropia: false, filas: null));
    }

    [Fact]
    public void GuiaPropia_completa_marca_todas()
    {
        var d = GuiaMetricasDisponiblesCalculos.Resolver(true, new[] { FilaCompleta() });

        Assert.Equal(GuiaMetricasDisponiblesCalculos.Todas, d);
    }

    // ── Bordes ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Lista_vacia_o_nula_no_marca_ninguna()
    {
        Assert.Equal(GuiaMetricasDisponiblesCalculos.Ninguna,
            GuiaMetricasDisponiblesCalculos.Detectar(Array.Empty<FilaGuiaMetricas>()));
        Assert.Equal(GuiaMetricasDisponiblesCalculos.Ninguna,
            GuiaMetricasDisponiblesCalculos.Detectar(null));
    }

    /// <summary>Un cero de guía ES un dato (mortalidad 0 en la primera semana, por ejemplo).</summary>
    [Theory]
    [InlineData("0", true)]
    [InlineData("0,0", true)]
    [InlineData("  0  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void TieneDato_solo_el_blanco_cuenta_como_ausencia(string? valor, bool esperado)
    {
        Assert.Equal(esperado, GuiaMetricasDisponiblesCalculos.TieneDato(valor));
    }
}
