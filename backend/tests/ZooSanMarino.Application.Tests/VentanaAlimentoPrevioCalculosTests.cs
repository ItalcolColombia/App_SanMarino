using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ventana de alimento previo al encasetamiento: desde qué fecha cuenta el alimento del galpón para
/// el saldo del reporte diario de engorde.
/// <para>
/// Cortar exactamente en la fecha de encaset (FIX #12) evitaba heredar el sobrante del ciclo anterior
/// pero descartaba alimento propio del lote: el preiniciador llega antes que los pollitos. Caso
/// testigo — galpón 6 de DAYLAND: 12.129,638 kg recibidos 4 días antes del encaset, justo la
/// diferencia entre el saldo del reporte y el inventario real.
/// </para>
/// </summary>
public class VentanaAlimentoPrevioCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 6, 8);

    [Fact]
    public void DiasPorDefecto_SonDiez() =>
        // Por debajo del vacío sanitario típico (10-14 días): la ventana no puede alcanzar el cierre
        // del lote anterior del mismo galpón.
        Assert.Equal(10, VentanaAlimentoPrevioCalculos.DiasPreviosPorDefecto);

    [Theory]
    [InlineData(null, 10)]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    public void NormalizarDias_ConservaLoConfigurado(int? configurado, int esperado) =>
        Assert.Equal(esperado, VentanaAlimentoPrevioCalculos.NormalizarDias(configurado));

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NormalizarDias_Negativo_CaeAlDefault(int configurado) =>
        Assert.Equal(10, VentanaAlimentoPrevioCalculos.NormalizarDias(configurado));

    [Theory]
    [InlineData(31)]
    [InlineData(365)]
    public void NormalizarDias_PorEncimaDelMaximo_SeRecorta(int configurado) =>
        // Fail-safe: una ventana enorme volvería a heredar el alimento del ciclo anterior.
        Assert.Equal(30, VentanaAlimentoPrevioCalculos.NormalizarDias(configurado));

    [Fact]
    public void FechaCorte_SinEncaset_EsNull() =>
        // Sin encaset el lote no tiene referencia temporal: filtrar sería arbitrario.
        Assert.Null(VentanaAlimentoPrevioCalculos.FechaCorte(null, 10));

    [Fact]
    public void FechaCorte_ConDefault_RestaDiezDias() =>
        Assert.Equal(new DateTime(2026, 5, 29), VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, null));

    [Fact]
    public void FechaCorte_ConCero_EsElEncaset() =>
        // 0 = comportamiento previo al fix (corte seco en el encaset).
        Assert.Equal(Encaset, VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, 0));

    [Fact]
    public void FechaCorte_IgnoraLaHora() =>
        Assert.Equal(new DateTime(2026, 5, 29),
            VentanaAlimentoPrevioCalculos.FechaCorte(new DateTime(2026, 6, 8, 17, 30, 0), null));

    [Fact]
    public void FechaCorte_CasoGalpon6_IncluyeElIngresoDelPreiniciador()
    {
        // Encaset 08/06, preiniciador recibido el 04/06: con el default (10 días) el corte queda en
        // 29/05 y ese ingreso entra al saldo. Con 0 (el comportamiento viejo) quedaba afuera.
        var ingresoPreiniciador = new DateTime(2026, 6, 4);

        var conVentana = VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, null)!.Value;
        var sinVentana = VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, 0)!.Value;

        Assert.True(ingresoPreiniciador >= conVentana);
        Assert.True(ingresoPreiniciador < sinVentana);
    }

    [Fact]
    public void FechaCorte_ElCicloAnteriorQuedaFuera()
    {
        // Un lote de engorde dura ~42 días; con el vacío sanitario, el cierre del ciclo previo cae
        // bastante antes que la ventana de 10 días.
        var cierreCicloAnterior = Encaset.AddDays(-25);
        var corte = VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, null)!.Value;

        Assert.True(cierreCicloAnterior < corte);
    }

    [Fact]
    public void FechaCorte_ConElMaximo_TodaviaExcluyeUnCicloCompleto()
    {
        var corte = VentanaAlimentoPrevioCalculos.FechaCorte(Encaset, 999)!.Value; // recortado a 30
        Assert.Equal(Encaset.AddDays(-30), corte);
        Assert.True(Encaset.AddDays(-42) < corte); // el encaset del lote anterior queda fuera
    }
}
