using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El aviso de «esta salida deja el día en rojo». Nace del 18-may-2026: en tres galpones de Ecuador
/// se cargó un ingreso sin remisión fechado ese mismo día y después las salidas fechadas hacia atrás,
/// al 15 y 16 de mayo. El stock lo aceptó —en el instante del guardado los kilos ya estaban— y la
/// tabla diaria, que ordena por fecha, cerró en −3.920, −3.220 y −600 kg.
/// </summary>
public class SalidaEnRojoCalculosTests
{
    // ─── A quién se le pregunta a la fn ───────────────────────────────────────

    [Fact]
    public void ConGalponYCantidad_SeChequea()
        => Assert.True(SalidaEnRojoCalculos.AmeritaChequeo("G0055", 3600m, confirmadoPorElUsuario: false));

    [Fact]
    public void YaConfirmadoPorElUsuario_NO_SeVuelveAChequear()
    {
        // El aviso es confirmable: si se volviera a preguntar, el usuario no podría registrar nunca
        // el movimiento que ya decidió registrar.
        Assert.False(SalidaEnRojoCalculos.AmeritaChequeo("G0055", 3600m, confirmadoPorElUsuario: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinGalponOrigen_NO_SeChequea(string? galpon)
    {
        // La tabla diaria de engorde se arma POR GALPÓN: una salida de bodega de granja no puede
        // dejar ningún día en rojo. Mismo criterio que SaldoAlimentoEngordeAplicador, que también se
        // va sin hacer nada cuando no hay galpón.
        Assert.False(SalidaEnRojoCalculos.AmeritaChequeo(galpon, 3600m, confirmadoPorElUsuario: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CantidadNoPositiva_NO_SeChequea(decimal cantidad)
        => Assert.False(SalidaEnRojoCalculos.AmeritaChequeo("G0055", cantidad, confirmadoPorElUsuario: false));

    // ─── La comparación ───────────────────────────────────────────────────────

    [Fact]
    public void ElCasoDeG0055_AvisaConLosNumerosReales()
    {
        // El día 16-may tenía 4.200 kg y la salida retiraba 8.120: quedó en −3.920.
        Assert.True(SalidaEnRojoCalculos.DejaDiaEnRojo(4200m, 8120m));
    }

    [Fact]
    public void SiAlcanzaJusto_NO_Avisa()
    {
        // Quedar en cero es el cierre normal de un ciclo: sacar todo lo que sobró.
        Assert.False(SalidaEnRojoCalculos.DejaDiaEnRojo(8120m, 8120m));
    }

    [Fact]
    public void SiSobra_NO_Avisa()
        => Assert.False(SalidaEnRojoCalculos.DejaDiaEnRojo(11720m, 8120m));

    [Fact]
    public void SinTablaDesdeEsaFecha_NO_Avisa()
    {
        // Galpón sin ningún día cargado desde la fecha del movimiento: no hay tabla que poner en rojo.
        Assert.False(SalidaEnRojoCalculos.DejaDiaEnRojo(null, 8120m));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void PorDebajoDeLaTolerancia_NO_Avisa(double excesoKg)
    {
        // Es la tolerancia del cuadre: por debajo de un kilo no es un día en rojo, es punto flotante.
        // Sin esto, un galpón que cierra en −1e-11 avisaría en cada salida.
        Assert.False(SalidaEnRojoCalculos.DejaDiaEnRojo(1000m, 1000m + (decimal)excesoKg));
    }

    [Fact]
    public void ApenasPorEncimaDeLaTolerancia_SI_Avisa()
        => Assert.True(SalidaEnRojoCalculos.DejaDiaEnRojo(1000m, 1001.5m));

    [Fact]
    public void ElMinimoEsElQueMandaAunqueElDiaDelMovimientoAguante()
    {
        // La salida baja por igual TODOS los días siguientes, así que el mínimo desde la fecha es el
        // que decide: mirar solo el saldo del día del movimiento dejaría pasar el que revienta después.
        Assert.True(SalidaEnRojoCalculos.DejaDiaEnRojo(saldoMinimoDesdeLaFecha: 500m, cantidad: 3600m));
    }

    // ─── El mensaje ───────────────────────────────────────────────────────────

    [Fact]
    public void ElMensajeTraeLosTresNumerosQueHacenFaltaParaDecidir()
    {
        var msg = SalidaEnRojoCalculos.Mensaje(
            "2602", new DateOnly(2026, 5, 16), 4200m, 8120m, "kg");

        Assert.Contains("16/05/2026", msg);
        Assert.Contains("2602", msg);
        Assert.Contains("4.200", msg.Replace(",", "."));      // lo que hay
        Assert.Contains("8.120", msg.Replace(",", "."));      // lo que sale
        Assert.Contains("-3.920", msg.Replace(",", "."));     // con cuánto queda
    }

    [Fact]
    public void SinLoteNiUnidad_ElMensajeSigueSiendoLegible()
    {
        var msg = SalidaEnRojoCalculos.Mensaje(null, new DateOnly(2026, 5, 16), 4200m, 8120m, null);

        Assert.DoesNotContain("del lote", msg);
        Assert.Contains("kg", msg);
    }
}
