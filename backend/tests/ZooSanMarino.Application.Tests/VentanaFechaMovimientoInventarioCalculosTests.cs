using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ventana de fechas de los movimientos de inventario cargados A MANO: del 1 del mes en curso hasta
/// hoy. Pedido del usuario (07-ago-2026): «que solo pueda agregar manualmente los alimentos del mes
/// actual, así se evita meter meses antes».
///
/// <para>
/// 🔑 20-ago-2026: la ventana se amplió a <c>MIN(1 del mes, hoy − 15)</c>
/// (<see cref="VentanaFechaRegistroCalculos"/>), que esta clase delega. Los tests con
/// <c>Hoy</c> en el día 7 del mes ya no aíslan «solo el mes en curso»: el piso rodante de 15 días
/// llega más atrás que el 1 del mes. Donde eso cambia el resultado esperado, el test lo dice.
/// </para>
/// </summary>
public class VentanaFechaMovimientoInventarioCalculosTests
{
    private static readonly DateTime Hoy = new(2026, 8, 7);

    [Fact]
    public void PrimerDiaDelMes_Permitido()
        => Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 8, 1), Hoy));

    [Fact]
    public void Hoy_Permitido()
        => Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(Hoy, Hoy));

    [Fact]
    public void AyerDentroDelMismoMes_Permitido()
        => Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 8, 6), Hoy));

    [Fact]
    public void UltimoDiaDelMesAnterior_YaSePermiteConLaVentanaDeQuinceDias()
    {
        // Éste es el caso exacto que motivó el pedido del usuario (20-ago-2026): con Hoy=07/08, el
        // día 1 de agosto no dejaba cargar el 31/07 aunque hubiera pasado apenas una semana. Con el
        // piso rodante de 15 días (23/07 en este caso), el 31/07 ya cae dentro.
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 7, 31), Hoy));
    }

    [Fact]
    public void MasDeQuinceDiasAtras_SigueRechazado()
    {
        // 20/07 está a 18 días de Hoy (07/08): fuera del piso rodante (23/07) y fuera del mes en
        // curso. La ventana es más ancha que antes, pero no ilimitada.
        Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 7, 20), Hoy));
    }

    [Fact]
    public void MesAnteriorCompleto_Rechazado()
        => Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 7, 15), Hoy));

    [Fact]
    public void Manana_Rechazado()
        => Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 8, 8), Hoy));

    [Fact]
    public void FinDelMismoMesPeroFuturo_Rechazado()
        => Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 8, 31), Hoy));

    [Fact]
    public void SinFecha_Permitido()
    {
        // null = «sin fecha explícita»: el servicio le pone la hora actual, que está dentro por construcción.
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(null, Hoy));
    }

    [Fact]
    public void LaHoraNoCuenta_SoloElDia()
    {
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 7, 23, 59, 59), Hoy));
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 1, 0, 0, 0), Hoy));
    }

    [Fact]
    public void CambioDeAnio_ElPisoCruzaElAnioCorrectamente()
    {
        // Hoy=05/01/2027 (día 5): el piso rodante (hoy−15 = 21/12/2026) llega más atrás que el 1 de
        // enero, así que ES el que manda — cruzando el año sin romper la aritmética.
        var primeroDeEnero = new DateTime(2027, 1, 5);
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2027, 1, 1), primeroDeEnero));

        // 31/12/2026 está a 5 días de Hoy: dentro del piso rodante (21/12), aunque sea "diciembre" y
        // el mes en curso sea enero. Es la misma ampliación que UltimoDiaDelMesAnterior de arriba.
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 12, 31), primeroDeEnero));

        // 15/12/2026 está a 21 días: fuera del piso rodante (21/12) y fuera del mes en curso.
        Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 12, 15), primeroDeEnero));
    }

    [Fact]
    public void ElDiaOperativoEsUtcMenos5_NoElDiaUtc()
    {
        // 31/08 a las 20:00 en Ecuador ya es 01/09 en UTC. Sin el offset, la operación no podría
        // cargar un movimiento fechado HOY en las últimas 5 horas del mes.
        var ahora = new DateTimeOffset(2026, 9, 1, 1, 0, 0, TimeSpan.Zero);
        var hoy = VentanaFechaMovimientoInventarioCalculos.DiaOperativo(ahora);

        Assert.Equal(new DateTime(2026, 8, 31), hoy);
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 8, 31), hoy));
    }

    [Fact]
    public void ElMensajeNombraLosDosExtremosDeLaVentana()
    {
        var msg = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentana(Hoy);

        // Piso rodante (Hoy−15 = 23/07), no el 1 del mes: la ventana ampliada manda.
        Assert.Contains("23/07/2026", msg);
        Assert.Contains("07/08/2026", msg);
    }

    [Fact]
    public void ConPermisoRetroactivo_ElMensajeSoloHablaDeFechaFutura()
    {
        var msg = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentana(Hoy, puedeRetroactivar: true);

        Assert.Contains("07/08/2026", msg);
        Assert.DoesNotContain("23/07/2026", msg);
    }

    [Fact]
    public void ConPermisoRetroactivo_CualquierFechaPasadaSePermite()
    {
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(
            new DateTime(2024, 1, 1), Hoy, puedeRetroactivar: true));
    }

    [Fact]
    public void ConPermisoRetroactivo_ElFuturoSigueRechazado()
    {
        Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(
            new DateTime(2026, 8, 8), Hoy, puedeRetroactivar: true));
    }
}
