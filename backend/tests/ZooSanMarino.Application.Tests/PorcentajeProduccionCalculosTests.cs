using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// %Producción ave-día del Reporte Técnico — ver
/// <c>fase_de_desarrollo/reporte_tecnico_porcentaje_produccion_plan.md</c>.
/// </summary>
public class PorcentajeProduccionCalculosTests
{
    // ── Diario ───────────────────────────────────────────────────────────────
    [Fact]
    public void Diario_divide_huevos_entre_hembras_vivas()
    {
        Assert.Equal(48.78, PorcentajeProduccionCalculos.Diario(3705, 7596), 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Diario_sin_hembras_es_cero(int hembras)
    {
        Assert.Equal(0d, PorcentajeProduccionCalculos.Diario(120, hembras));
    }

    // ── Hembras al inicio del día ────────────────────────────────────────────
    [Fact]
    public void HembrasInicioDia_es_el_cierre_del_dia_anterior()
    {
        // 10.445 iniciales, 4.190 bajas y 4 de selección acumuladas antes del día.
        Assert.Equal(6251, PorcentajeProduccionCalculos.HembrasInicioDia(10445, 4190, 4));
    }

    [Fact]
    public void HembrasInicioDia_no_baja_de_cero()
    {
        Assert.Equal(0, PorcentajeProduccionCalculos.HembrasInicioDia(100, 90, 30));
    }

    [Fact]
    public void Dia_de_liquidacion_no_pasa_de_100_con_las_hembras_del_inicio()
    {
        // P-K345B, 14-may-2026: arrancó con 6.251 hembras, salieron ~4.600 y cerró con 1.651.
        // Con el cierre daba 2.481 / 1.651 = 150,3 %; con las del inicio del día, 39,7 %.
        const int huevos = 2481, inicio = 6251, cierre = 1651;

        Assert.True(PorcentajeProduccionCalculos.Diario(huevos, cierre) > 100d);
        Assert.Equal(39.69, PorcentajeProduccionCalculos.Diario(huevos, inicio), 2);
    }

    // ── Periodo ──────────────────────────────────────────────────────────────
    [Fact]
    public void Periodo_semana_del_ticket_no_multiplica_por_siete()
    {
        // Semana 2 de P-K345A: 19.213 huevos, saldo 7.595 → 7.586. La Semanal General dividía
        // 19.213 / 7.586 = 253,3 %; con ave-día da el valor comparable con la guía (31,3).
        var dias = new[]
        {
            (2400, 7595), (2600, 7594), (2700, 7592), (2800, 7590),
            (2850, 7589), (2900, 7587), (2963, 7586),
        };

        var porc = PorcentajeProduccionCalculos.Periodo(dias);

        Assert.Equal(36.16, porc, 2);
        Assert.InRange(porc, 0d, 100d);
    }

    [Fact]
    public void Periodo_de_un_dia_es_igual_al_diario()
    {
        Assert.Equal(
            PorcentajeProduccionCalculos.Diario(6500, 7400),
            PorcentajeProduccionCalculos.Periodo(new[] { (6500, 7400) }));
    }

    [Fact]
    public void Periodo_con_aves_constantes_es_el_promedio_del_porcentaje_diario()
    {
        // Equivalencia con la regla anterior del semanal por galpón (promedio del % diario).
        var dias = new[] { (850, 1000), (870, 1000), (880, 1000) };
        var promedioDiario = dias.Average(d => PorcentajeProduccionCalculos.Diario(d.Item1, d.Item2));

        Assert.Equal(promedioDiario, PorcentajeProduccionCalculos.Periodo(dias), 10);
    }

    [Fact]
    public void Periodo_consolidado_pondera_por_aves_de_cada_galpon()
    {
        // Galpón A 1.000 hembras al 90 %, galpón B 3.000 al 70 %: 3.000 huevos / 4.000 aves = 75 %,
        // no el 80 % del promedio simple.
        var dias = new[] { (900, 1000), (2100, 3000) };

        Assert.Equal(75d, PorcentajeProduccionCalculos.Periodo(dias), 10);
    }

    [Fact]
    public void Periodo_ignora_los_dias_sin_hembras()
    {
        var dias = new[] { (500, 1000), (300, 0) };

        Assert.Equal(50d, PorcentajeProduccionCalculos.Periodo(dias), 10);
    }

    [Fact]
    public void Periodo_vacio_o_sin_hembras_es_cero()
    {
        Assert.Equal(0d, PorcentajeProduccionCalculos.Periodo(Array.Empty<(int, int)>()));
        Assert.Equal(0d, PorcentajeProduccionCalculos.Periodo(new[] { (40, 0), (10, 0) }));
    }

    [Fact]
    public void Periodo_nunca_pasa_de_100_si_cada_dia_huevos_no_supera_hembras()
    {
        var rnd = new Random(20260912);
        var dias = Enumerable.Range(0, 7 * 6)
            .Select(_ =>
            {
                var hembras = rnd.Next(1, 12000);
                return (rnd.Next(0, hembras + 1), hembras);
            })
            .ToArray();

        Assert.InRange(PorcentajeProduccionCalculos.Periodo(dias), 0d, 100d);
    }
}
