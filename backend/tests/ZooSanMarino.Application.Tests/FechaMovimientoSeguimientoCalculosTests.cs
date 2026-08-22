using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la fecha con que nace el movimiento de inventario de un seguimiento diario.
///
/// <para>
/// Estos tests no verifican una cuenta: <b>fijan una decisión</b>. La fecha del kardex es la del
/// formulario (el día del galpón) y no la del sync ni la que declara el teléfono. Los parámetros
/// <c>capturadoAtDispositivo</c> y <c>ahoraServidorUtc</c> están en la firma sin usarse a propósito,
/// así que lo que se afirma acá es justamente que <b>no</b> influyen. Si alguien los "conecta",
/// estos casos se ponen rojos y el cambio deja de ser silencioso.
/// </para>
/// </summary>
public class FechaMovimientoSeguimientoCalculosTests
{
    private static readonly DateTime FechaRegistro = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Unspecified);

    // Un push que llegó cinco días tarde: el galponero cargó el 17 y hubo señal el 22.
    private static readonly DateTime AhoraSync = new(2026, 8, 22, 14, 35, 12, DateTimeKind.Utc);

    [Fact]
    public void DevuelveElDiaDelFormulario_NoElDelSync()
    {
        var r = FechaMovimientoSeguimientoCalculos.Resolver(FechaRegistro, null, AhoraSync);

        Assert.Equal(new DateTime(2026, 8, 17), r);
    }

    /// <summary>
    /// El kardex es por día: la tabla diaria agrupa por <c>DATE(fecha_operacion)</c>. Que el
    /// formulario mande medianoche, mediodía o el último segundo del día no puede cambiar el
    /// movimiento.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12, 0, 0)]
    [InlineData(23, 59, 59)]
    public void DescartaLaHoraDelRegistro(int h, int m, int s)
    {
        var conHora = new DateTime(2026, 8, 17, h, m, s, DateTimeKind.Unspecified);

        var r = FechaMovimientoSeguimientoCalculos.Resolver(conHora, null, AhoraSync);

        Assert.Equal(new DateTime(2026, 8, 17), r);
        Assert.Equal(TimeSpan.Zero, r.TimeOfDay);
    }

    /// <summary>
    /// El reloj del teléfono es del usuario: lo cambia a mano o tiene la zona mal. Se registra en
    /// <c>sync_operaciones</c> para auditoría y no decide nada. Se cubren los tres casos que un
    /// dispositivo real manda: sin valor, atrasado y adelantado.
    /// </summary>
    [Fact]
    public void ElRelojDelDispositivoNoCambiaNada()
    {
        var esperado = new DateTime(2026, 8, 17);

        // Sin declarar (app vieja o campo omitido).
        Assert.Equal(esperado,
            FechaMovimientoSeguimientoCalculos.Resolver(FechaRegistro, null, AhoraSync));

        // Reloj atrasado: el teléfono cree que es un mes antes.
        Assert.Equal(esperado,
            FechaMovimientoSeguimientoCalculos.Resolver(
                FechaRegistro, new DateTime(2026, 7, 15, 6, 30, 0, DateTimeKind.Utc), AhoraSync));

        // Reloj adelantado a lo bestia: si mandara, se descontarían kilos en un día que no pasó.
        Assert.Equal(esperado,
            FechaMovimientoSeguimientoCalculos.Resolver(
                FechaRegistro, new DateTime(2031, 1, 1, 3, 0, 0, DateTimeKind.Utc), AhoraSync));

        // Año roto (el caso que aparece cuando el teléfono arranca sin batería y pierde la hora).
        Assert.Equal(esperado,
            FechaMovimientoSeguimientoCalculos.Resolver(
                FechaRegistro, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), AhoraSync));
    }

    /// <summary>
    /// El reloj del servidor describe el sync, no la operación del galpón. Es la fuente del defecto
    /// que este cálculo existe para evitar (hoy varios caminos caen en <c>UtcNow</c>), así que se fija
    /// que ni un servidor a 500 días de distancia mueve la fecha.
    /// </summary>
    [Theory]
    [InlineData(-30)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(500)]
    public void ElRelojDelServidorNoCambiaNada(int diasDeDesfase)
    {
        var ahora = FechaRegistro.AddDays(diasDeDesfase).AddHours(9);

        var r = FechaMovimientoSeguimientoCalculos.Resolver(FechaRegistro, null, ahora);

        Assert.Equal(new DateTime(2026, 8, 17), r);
    }

    /// <summary>
    /// Los dos parámetros ignorados, combinados y contradiciéndose entre sí, siguen sin mover el
    /// resultado: el día del formulario es la única entrada que importa.
    /// </summary>
    [Fact]
    public void ConDispositivoYServidorEnDiasDistintos_SigueMandandoElFormulario()
    {
        var r = FechaMovimientoSeguimientoCalculos.Resolver(
            new DateTime(2026, 8, 17, 18, 42, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            new DateTime(2025, 1, 2, 0, 0, 1, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 17), r);
    }

    /// <summary>
    /// No se hace <c>ToUniversalTime()</c>: una fecha local de la tarde convertida a UTC se va al día
    /// siguiente, que es exactamente el corrimiento que se quiere evitar. El <c>Kind</c> del valor
    /// recibido se conserva porque el consumidor (<c>ResolveMovimientoCreatedAt</c>) sólo lee
    /// año/mes/día para anclar el instante.
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void NoConvierteDeZona_ConservaElDiaYElKind(DateTimeKind kind)
    {
        var tardeDelDia = new DateTime(2026, 8, 17, 20, 15, 0, kind);

        var r = FechaMovimientoSeguimientoCalculos.Resolver(tardeDelDia, null, AhoraSync);

        Assert.Equal(17, r.Day);
        Assert.Equal(8, r.Month);
        Assert.Equal(2026, r.Year);
        Assert.Equal(kind, r.Kind);
    }
}
