using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ventana de fechas de los movimientos de inventario cargados A MANO: del 1 del mes en curso hasta
/// hoy. Pedido del usuario (07-ago-2026): «que solo pueda agregar manualmente los alimentos del mes
/// actual, así se evita meter meses antes».
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
    public void UltimoDiaDelMesAnterior_Rechazado()
        => Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 7, 31), Hoy));

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
    public void CambioDeAnio_ElMesEnCursoEsEneroNoDiciembre()
    {
        var primeroDeEnero = new DateTime(2027, 1, 5);
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2027, 1, 1), primeroDeEnero));
        Assert.False(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida(new DateTime(2026, 12, 31), primeroDeEnero));
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

        Assert.Contains("01/08/2026", msg);
        Assert.Contains("07/08/2026", msg);
    }
}
