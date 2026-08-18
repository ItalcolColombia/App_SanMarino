using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// §2.4 de la auditoría de cierre: la sección BULTO del Reporte Contable muestra los movimientos de
/// alimento de la GRANJA, y cuando la granja tiene varios lotes padres los reportes de todos muestran
/// los mismos kilos. Sumarlos multiplica el alimento (la granja 20 medida: 4 × 2.907 = 11.628 bultos
/// que no existen). No se puede atribuir al lote —no hay dato—, así que el reporte lo DICE.
/// </summary>
public class ReporteContableBultosAlcanceTests
{
    [Fact]   // T1 — el padre es el único de su granja: el kardex sí es suyo
    public void T1_UnSoloLotePadre_NoAvisa()
        => Assert.Null(ReporteContableBultosCalculos.AdvertenciaAlcance(1, "NIZA III"));

    [Fact]   // T2 — el caso real de la auditoría
    public void T2_CuatroLotesPadres_AvisaNombrandoGranjaYCantidad()
    {
        var aviso = ReporteContableBultosCalculos.AdvertenciaAlcance(4, "LA ESMERALDA");

        Assert.NotNull(aviso);
        Assert.Contains("LA ESMERALDA", aviso);
        Assert.Contains("4 lotes padres", aviso);
        Assert.Contains("los otros 3", aviso);
        Assert.Contains("NO sumar", aviso);
    }

    [Fact]   // T3 — con dos padres el aviso habla en singular del otro
    public void T3_DosLotesPadres_HablaDelOtroEnSingular()
    {
        var aviso = ReporteContableBultosCalculos.AdvertenciaAlcance(2, "MIRALINDO");

        Assert.NotNull(aviso);
        Assert.Contains("el otro", aviso);
        Assert.DoesNotContain("los otros", aviso);
    }

    [Theory] // T4 — sin dato no se inventa un aviso
    [InlineData(0)]
    [InlineData(-1)]
    public void T4_SinDato_NoAvisa(int lotesPadre)
        => Assert.Null(ReporteContableBultosCalculos.AdvertenciaAlcance(lotesPadre, "MANGOS"));

    [Theory] // T5 — sin nombre de granja el aviso sigue siendo legible
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void T5_SinNombreDeGranja_AvisaGenerico(string? granja)
    {
        var aviso = ReporteContableBultosCalculos.AdvertenciaAlcance(3, granja);

        Assert.NotNull(aviso);
        Assert.Contains("esta granja", aviso);
        Assert.DoesNotContain("«»", aviso);
    }
}
