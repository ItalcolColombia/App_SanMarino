using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Hora de llegada de las aves → primer día con registro. Corte 13:00 INCLUSIVE; sin hora, el
/// comportamiento debe quedar idéntico al previo (los lotes existentes no tienen hora).
/// </summary>
public class EncasetamientoCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinHora_ElPrimerDiaEsElEncaset_Regresion()
    {
        Assert.False(EncasetamientoCalculos.LlegadaTardia(null));
        Assert.Equal(Encaset, EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, null));
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(null));
        Assert.Null(EncasetamientoCalculos.MotivoDesplazamiento(null));
    }

    [Theory]
    [InlineData(0, 0)]    // 00:00
    [InlineData(6, 0)]    // 06:00
    [InlineData(11, 59)]  // justo antes del mediodía
    [InlineData(12, 0)]   // mediodía: NO es tardío (el corte es 13:00)
    [InlineData(12, 59)]  // último minuto temprano
    public void LlegadaTemprana_ElPrimerConsumoVaElMismoDia(int hora, int minuto)
    {
        var h = new TimeOnly(hora, minuto);

        Assert.False(EncasetamientoCalculos.LlegadaTardia(h));
        Assert.Equal(new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc),
                     EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, h));
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(h));
    }

    [Theory]
    [InlineData(13, 0)]   // corte INCLUSIVE
    [InlineData(13, 1)]
    [InlineData(18, 30)]
    [InlineData(23, 59)]
    public void LlegadaTardia_ElPrimerConsumoVaAlDiaSiguiente(int hora, int minuto)
    {
        var h = new TimeOnly(hora, minuto);

        Assert.True(EncasetamientoCalculos.LlegadaTardia(h));
        Assert.Equal(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
                     EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, h));
        Assert.Equal(1, EncasetamientoCalculos.EdadMinimaConRegistro(h));
    }

    [Fact]
    public void PrimerDiaConRegistro_ConservaKindYHoraDelEncaset()
    {
        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, new TimeOnly(15, 0));

        Assert.Equal(DateTimeKind.Utc, primer.Kind);
        Assert.Equal(12, primer.Hour); // sigue anclado a mediodía UTC, como las fechas puras del sistema
    }

    [Fact]
    public void LlegadaTardia_CruzaFinDeMes()
    {
        var encaset = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(encaset, new TimeOnly(18, 0));

        Assert.Equal(new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc), primer);
    }

    [Fact]
    public void LlegadaTardia_CruzaAlDia29EnAnioBisiesto()
    {
        var encaset = new DateTime(2028, 2, 28, 12, 0, 0, DateTimeKind.Utc);

        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(encaset, new TimeOnly(14, 0));

        Assert.Equal(new DateTime(2028, 2, 29, 12, 0, 0, DateTimeKind.Utc), primer);
    }

    [Fact]
    public void MotivoDesplazamiento_ExplicaLaHoraAlUsuario()
    {
        var motivo = EncasetamientoCalculos.MotivoDesplazamiento(new TimeOnly(15, 30));

        Assert.NotNull(motivo);
        Assert.Contains("15:30", motivo);
        Assert.Contains("13:00", motivo);
    }

    // ── Ventana de captura de la reproductora ────────────────────────────────
    [Fact]
    public void EdadSeguimiento_LoteTemprano_AceptaDesdeLaEdadCero_Regresion()
    {
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(0));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(7));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(-1));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(8));
    }

    [Fact]
    public void EdadSeguimiento_LoteTardio_RechazaElDiaDelEncaset()
    {
        const int edadMinima = 1;

        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(0, edadMinima: edadMinima));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(1, edadMinima: edadMinima));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(7, edadMinima: edadMinima));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(8, edadMinima: edadMinima));
    }
}
