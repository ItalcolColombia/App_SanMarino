using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Equivalencia de la discriminación semana/fase Levante-Producción y etapa de producción,
/// extraída de MovimientoAvesService a MovimientoAvesCalculos: división entera por 7 + 1,
/// umbral de semana 26 y tramos de etapa 26–33 / 34–50 / resto. Misma aritmética que vivía inline.
/// </summary>
public class MovimientoAvesCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 1, 1);

    [Theory]
    [InlineData(0, 1)]   // mismo día → 0 días transcurridos → semana 1
    [InlineData(1, 1)]
    [InlineData(6, 1)]   // último día de la semana 1
    [InlineData(7, 2)]   // primer día de la semana 2
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    public void SemanaDesdeEncaset_DivisionEnteraPor7MasUno(int diasTranscurridos, int semanaEsperada)
    {
        var fecha = Encaset.AddDays(diasTranscurridos);

        var semana = MovimientoAvesCalculos.SemanaDesdeEncaset(fecha, Encaset);

        Assert.Equal(semanaEsperada, semana);
    }

    [Fact]
    public void SemanaDesdeEncaset_TruncaHoraAmbasFechas()
    {
        var fecha = Encaset.AddDays(7).AddHours(23);
        var encasetConHora = Encaset.AddHours(12);

        var semana = MovimientoAvesCalculos.SemanaDesdeEncaset(fecha, encasetConHora);

        Assert.Equal(2, semana);
    }

    [Fact]
    public void SemanaDesdeEncaset_FechaAnteriorAlEncaset_DiasNegativos()
    {
        var fecha = Encaset.AddDays(-8);

        // (-8 / 7) en C# trunca hacia cero → -1; -1 + 1 = 0.
        var semana = MovimientoAvesCalculos.SemanaDesdeEncaset(fecha, Encaset);

        Assert.Equal(0, semana);
    }

    [Theory]
    [InlineData(0, 0)]   // mismo día → 0 días → devuelve 0 (no 1)
    [InlineData(1, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(14, 3)]
    public void SemanaDesdeEncasetOCero_CeroSoloCuandoNoHanTranscurridoDias(int diasTranscurridos, int semanaEsperada)
    {
        var fecha = Encaset.AddDays(diasTranscurridos);

        var semana = MovimientoAvesCalculos.SemanaDesdeEncasetOCero(fecha, Encaset);

        Assert.Equal(semanaEsperada, semana);
    }

    [Fact]
    public void SemanaDesdeEncasetOCero_FechaAnteriorAlEncaset_DevuelveCero()
    {
        var fecha = Encaset.AddDays(-1);

        var semana = MovimientoAvesCalculos.SemanaDesdeEncasetOCero(fecha, Encaset);

        Assert.Equal(0, semana);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(25, true)]
    [InlineData(26, false)]
    [InlineData(27, false)]
    public void EstaEnLevante_MenorASemana26(int semana, bool esperado)
    {
        Assert.Equal(esperado, MovimientoAvesCalculos.EstaEnLevante(semana));
    }

    [Theory]
    [InlineData(25, false)]
    [InlineData(26, true)]
    [InlineData(27, true)]
    public void EstaEnProduccion_DesdeSemana26(int semana, bool esperado)
    {
        Assert.Equal(esperado, MovimientoAvesCalculos.EstaEnProduccion(semana));
    }

    [Fact]
    public void EstaEnLevante_Y_EstaEnProduccion_SonMutuamenteExcluyentesYExhaustivas()
    {
        for (var semana = 0; semana <= 60; semana++)
        {
            Assert.NotEqual(
                MovimientoAvesCalculos.EstaEnLevante(semana),
                MovimientoAvesCalculos.EstaEnProduccion(semana));
        }
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(25, 3)]
    [InlineData(26, 1)]
    [InlineData(30, 1)]
    [InlineData(33, 1)]
    [InlineData(34, 2)]
    [InlineData(40, 2)]
    [InlineData(50, 2)]
    [InlineData(51, 3)]
    [InlineData(100, 3)]
    public void EtapaProduccion_Tramos26a33_34a50_Resto(int semana, int etapaEsperada)
    {
        Assert.Equal(etapaEsperada, MovimientoAvesCalculos.EtapaProduccion(semana));
    }
}
