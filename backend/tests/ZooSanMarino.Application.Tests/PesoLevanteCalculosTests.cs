using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El reporte técnico de levante convertía a kilos SÓLO la guía y comparaba gramos contra kilos:
/// «%Dif Peso H» daba 104.037,93 % en S369A semana 1 (151 g reales vs 145 g de guía) y
/// 109.555,17 % en K345A. Acá se fija que real y guía viven en la misma unidad.
/// </summary>
public class PesoLevanteCalculosTests
{
    // ── Conversión de unidad ──────────────────────────────────────────────────
    [Theory]
    [InlineData(151, 0.151)]
    [InlineData(3029.17, 3.02917)]
    [InlineData(0.5, 0.0005)]
    public void AKilos_divide_por_mil(double gramos, double esperado)
    {
        Assert.Equal(esperado, PesoLevanteCalculos.AKilos(gramos)!.Value, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AKilos_sin_pesaje_devuelve_null(double gramos)
    {
        // Mismo guard `peso > 0` que ya tenía el service: sin pesaje la celda va vacía, no en cero.
        Assert.Null(PesoLevanteCalculos.AKilos(gramos));
    }

    // ── Diferencia contra la guía ─────────────────────────────────────────────
    [Fact]
    public void PorcDiferencia_los_casos_que_estaban_rotos_en_pantalla()
    {
        // S369A semana 1: 151 g reales contra 145 g de guía ⇒ +4,14 % (antes 104.037,93 %).
        Assert.Equal(4.14, PesoLevanteCalculos.PorcDiferencia(151, 145)!.Value, 2);
        // S369A semana 24: 3.029,17 g contra 2.915 g ⇒ +3,92 % (antes 103.816,52 %).
        Assert.Equal(3.92, PesoLevanteCalculos.PorcDiferencia(3029.17, 2915)!.Value, 2);
        // K345A semana 1: 159 g contra 145 g ⇒ +9,66 % (antes 109.555,17 %).
        Assert.Equal(9.66, PesoLevanteCalculos.PorcDiferencia(159, 145)!.Value, 2);
    }

    [Fact]
    public void PorcDiferencia_peso_bajo_la_guia_da_negativo()
    {
        // El semáforo del front pinta rojo con negativo: no puede quedar en valor absoluto.
        Assert.True(PesoLevanteCalculos.PorcDiferencia(2800, 2915) < 0);
        Assert.Equal(-3.95, PesoLevanteCalculos.PorcDiferencia(2800, 2915)!.Value, 2);
    }

    [Fact]
    public void PorcDiferencia_es_invariante_a_la_unidad()
    {
        // La raíz del bug: mezclar unidades. Con las dos en la MISMA, gramos o kilos da igual.
        foreach (var (real, guia) in new[] { (151d, 145d), (3029.17, 2915d), (2800d, 2915d) })
        {
            var enGramos = PesoLevanteCalculos.PorcDiferencia(real, guia)!.Value;
            var enKilos  = PesoLevanteCalculos.PorcDiferencia(real / 1000, guia / 1000)!.Value;
            Assert.Equal(enGramos, enKilos, 9);
        }
    }

    [Theory]
    [InlineData(151, 0)]     // sin guía para esa edad
    [InlineData(0, 145)]     // semana sin pesaje
    [InlineData(0, 0)]
    [InlineData(-1, 145)]
    public void PorcDiferencia_sin_alguno_de_los_dos_devuelve_null(double real, double guia)
    {
        Assert.Null(PesoLevanteCalculos.PorcDiferencia(real, guia));
    }

    // ── Overload redondeada ───────────────────────────────────────────────────
    [Fact]
    public void PorcDiferencia_redondeada_conserva_el_estilo_de_2_decimales()
    {
        Assert.Equal(3.92, PesoLevanteCalculos.PorcDiferencia(3029.17, 2915, 2)!.Value);
        Assert.Null(PesoLevanteCalculos.PorcDiferencia(0, 2915, 2));
    }

    [Fact]
    public void GramosPorKilo_es_el_factor_que_usa_tambien_la_guia()
    {
        Assert.Equal(1000d, PesoLevanteCalculos.GramosPorKilo);
    }
}
