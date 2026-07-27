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

    // ── Día de negocio: el primer día CON REGISTRO es el día 1 ───────────────
    // Caso del reporte: granja DAYLAND, lote "13 - 1", encaset 2026-06-08. La tabla mostraba
    // «Edad 0» el propio 08/06 y el usuario espera «Día 1».
    private static readonly DateTime Dayland = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(8, 1)]    // el día del encaset es el día 1, no el 0
    [InlineData(9, 2)]
    [InlineData(14, 7)]   // cierre de la primera semana
    [InlineData(15, 8)]   // arranque de la segunda
    public void DiaDeNegocio_SinHora_ArrancaEnUno(int diaDelMes, int esperado)
    {
        var fecha = new DateTime(2026, 6, diaDelMes, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(esperado, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, null));
    }

    [Theory]
    [InlineData(8, 0)]    // el día del encaset ya no admite registro ⇒ queda fuera (≤ 0)
    [InlineData(9, 1)]    // el primer día CON REGISTRO es el día 1 igual que en un lote temprano
    [InlineData(15, 7)]   // y su semana 1 también son 7 días
    public void DiaDeNegocio_LlegadaTardia_CorreElDiaUno(int diaDelMes, int esperado)
    {
        var fecha = new DateTime(2026, 6, diaDelMes, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(esperado, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, new TimeOnly(15, 0)));
    }

    [Fact]
    public void DiaDeNegocio_HoraTemprana_NoCorreNada()
    {
        var fecha = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, new TimeOnly(9, 30)));
    }

    [Theory]
    [InlineData(0, 0)]    // anterior al primer registro
    [InlineData(-3, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 1)]    // la semana 1 son los días 1..7
    [InlineData(8, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    public void SemanaDeNegocio_AgrupaDeSieteEnSiete(int dia, int esperado)
    {
        Assert.Equal(esperado, EncasetamientoCalculos.SemanaDeNegocio(dia));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(41)]
    public void SemanaDeNegocio_SinDesplazamiento_CoincideConLaDeLaFnSql_Regresion(int edad)
    {
        // fn_seguimiento_diario_engorde calcula la semana como ceil((edad + 1) / 7). Con la
        // numeración 1-based (dia = edad + 1) el resultado tiene que ser el MISMO número.
        var semanaFn = (int)Math.Ceiling((edad + 1) / 7.0);

        Assert.Equal(semanaFn, EncasetamientoCalculos.SemanaDeNegocio(edad + 1));
    }
}
