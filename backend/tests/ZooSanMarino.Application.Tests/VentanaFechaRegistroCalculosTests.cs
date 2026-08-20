using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ventana base de fechas para los registros cargados A MANO (movimientos de inventario, de aves, de
/// pollo engorde, traslados de aves y de huevos, gastos de inventario) y el permiso que la destraba.
///
/// <para>
/// Pedido del usuario (20-ago-2026): con la regla anterior —solo el mes en curso— el día 1 de cada
/// mes nadie podía registrar lo que había llegado el día anterior, porque pertenecía al mes ya
/// cerrado. La ventana se amplía a <c>MIN(1 del mes, hoy − 15)</c>, y el permiso
/// <see cref="VentanaFechaRegistroCalculos.PermisoFechaRetroactiva"/> abre todo el pasado (nunca el
/// futuro, ni con el permiso).
/// </para>
/// </summary>
public class VentanaFechaRegistroCalculosTests
{
    // ─── El caso reportado: día 1 del mes ────────────────────────────────────────

    [Fact]
    public void Dia1DelMes_ElDiaAnterior_AhoraSePermite()
    {
        // Éste es el bug reportado: con la regla vieja, el 01/08 no dejaba cargar el 31/07 aunque
        // hubiera pasado apenas un día.
        var hoy = new DateTime(2026, 8, 1);
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 7, 31), hoy));
    }

    [Fact]
    public void Dia1DelMes_LosUltimosQuinceDiasSePermiten()
    {
        var hoy = new DateTime(2026, 8, 1);
        // hoy − 15 = 17/07: el piso rodante, inclusive.
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 7, 17), hoy));
    }

    [Fact]
    public void Dia1DelMes_DieciseisDiasAtras_Rechazado()
    {
        var hoy = new DateTime(2026, 8, 1);
        Assert.False(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 7, 16), hoy));
    }

    // ─── Día 16 en adelante: el mes en curso es más ancho que 15 días ────────────

    [Fact]
    public void Dia16DelMes_ElPisoEsElUnoDelMes_NoElRodante()
    {
        // hoy − 15 = 01/08, exactamente el 1 del mes: los dos coinciden.
        var hoy = new DateTime(2026, 8, 16);
        Assert.Equal(new DateTime(2026, 8, 1), VentanaFechaRegistroCalculos.PrimerDiaAdmitido(hoy));
    }

    [Fact]
    public void Dia20DelMes_ElMesEsMasAnchoQueElPisoRodante()
    {
        // hoy − 15 = 05/08, pero el 1 del mes (01/08) llega más atrás y es el que manda.
        var hoy = new DateTime(2026, 8, 20);
        Assert.Equal(new DateTime(2026, 8, 1), VentanaFechaRegistroCalculos.PrimerDiaAdmitido(hoy));
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 8, 2), hoy));
    }

    // ─── Es una ampliación estricta: nada que se aceptaba antes se rechaza ahora ─

    [Fact]
    public void ElDia20_PrimerDiaDelMes_SigueSiendoValido()
        => Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 1), new DateTime(2026, 8, 20)));

    [Fact]
    public void MesAnteriorMasAllaDeLosQuinceDias_SigueRechazado()
        => Assert.False(VentanaFechaRegistroCalculos.EsFechaPermitida(
            new DateTime(2026, 6, 30), new DateTime(2026, 8, 1)));

    // ─── El futuro no lo abre nadie ───────────────────────────────────────────────

    [Fact]
    public void Manana_Rechazado()
        => Assert.False(VentanaFechaRegistroCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 21), new DateTime(2026, 8, 20)));

    [Fact]
    public void Hoy_Permitido()
    {
        var hoy = new DateTime(2026, 8, 20);
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(hoy, hoy));
    }

    [Fact]
    public void SinFecha_SiempreValido()
        => Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(null, new DateTime(2026, 8, 20)));

    // ─── El permiso de fecha retroactiva ──────────────────────────────────────────

    [Fact]
    public void ConPermiso_HaceDosAnios_SePermite()
        => Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(
            new DateTime(2024, 1, 1), new DateTime(2026, 8, 20), puedeRetroactivar: true));

    [Fact]
    public void ConPermiso_ElFuturoSigueCerrado()
        => Assert.False(VentanaFechaRegistroCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 21), new DateTime(2026, 8, 20), puedeRetroactivar: true));

    [Fact]
    public void ConPermiso_ElMinimoOfrecidoEsNull()
    {
        var (min, max) = VentanaFechaRegistroCalculos.ExtremosVentana(new DateTime(2026, 8, 20), puedeRetroactivar: true);
        Assert.Null(min);
        Assert.Equal(new DateTime(2026, 8, 20), max);
    }

    [Fact]
    public void SinPermiso_ElMinimoOfrecidoEsElPisoDeLaVentana()
    {
        var hoy = new DateTime(2026, 8, 1);
        var (min, max) = VentanaFechaRegistroCalculos.ExtremosVentana(hoy);
        Assert.Equal(new DateTime(2026, 7, 17), min);
        Assert.Equal(hoy, max);
    }

    // ─── Equivalencia: sin el flag, a mitad de mes el resultado es idéntico a la
    // regla anterior (byte a byte, mensajes incluidos) ────────────────────────────

    [Fact]
    public void SinPermiso_ADia20_SeComportaIgualQueLaReglaDelMesEnCurso()
    {
        var hoy = new DateTime(2026, 8, 20);

        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 8, 1), hoy));
        Assert.False(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 7, 31), hoy));

        var msg = VentanaFechaRegistroCalculos.MensajeFueraDeVentana(hoy);
        Assert.Contains("01/08/2026", msg);
        Assert.Contains("20/08/2026", msg);
    }

    // ─── Permiso resuelto desde la lista de claims de la sesión ──────────────────

    [Theory]
    [InlineData("registros.fecha_retroactiva")]
    [InlineData("REGISTROS.FECHA_RETROACTIVA")]
    [InlineData("Registros.Fecha_Retroactiva")]
    public void TienePermisoRetroactivo_EsCaseInsensitive(string key)
        => Assert.True(VentanaFechaRegistroCalculos.TienePermisoRetroactivo(new[] { key }));

    [Fact]
    public void TienePermisoRetroactivo_SinLaKey_False()
        => Assert.False(VentanaFechaRegistroCalculos.TienePermisoRetroactivo(new[] { "otro.permiso" }));

    [Fact]
    public void TienePermisoRetroactivo_ListaVacia_False()
        => Assert.False(VentanaFechaRegistroCalculos.TienePermisoRetroactivo(Array.Empty<string>()));

    [Fact]
    public void TienePermisoRetroactivo_ListaNull_False()
        => Assert.False(VentanaFechaRegistroCalculos.TienePermisoRetroactivo(null));

    // ─── Día operativo UTC−5 ───────────────────────────────────────────────────────

    [Fact]
    public void DiaOperativo_UtcMenos5_CruzaElMesCorrectamente()
    {
        // 01/09 a las 03:00 UTC son las 22:00 del 31/08 en Colombia/Ecuador/Panamá.
        var ahora = new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTime(2026, 8, 31), VentanaFechaRegistroCalculos.DiaOperativo(ahora));
    }

    // ─── La hora no cuenta, solo el día ─────────────────────────────────────────

    [Fact]
    public void LaHoraNoCuenta_SoloElDia()
    {
        var hoy = new DateTime(2026, 8, 20);
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 8, 1, 0, 0, 1), hoy));
        Assert.True(VentanaFechaRegistroCalculos.EsFechaPermitida(new DateTime(2026, 8, 20, 23, 59, 59), hoy));
    }
}
