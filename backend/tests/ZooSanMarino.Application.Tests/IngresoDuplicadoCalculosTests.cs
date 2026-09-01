using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fija cuándo se avisa que un ingreso de alimento repite una remisión ya cargada.
/// El caso real: dos usuarios distintos cargaron el mismo remito y el galpón quedó con kilos que
/// nunca entraron. Ninguna guarda de front lo atrapa; solo el servidor.
/// </summary>
public class IngresoDuplicadoCalculosTests
{
    [Fact]
    public void ConRemisionYCantidad_SeChequea()
    {
        Assert.True(IngresoDuplicadoCalculos.AmeritaChequeo("190900", 8469m, confirmadoPorElUsuario: false));
    }

    [Fact]
    public void SinRemision_NoSeChequeaNunca()
    {
        // Dos camiones del mismo alimento el mismo día son dos ingresos reales: sin remisión no hay
        // con qué afirmar que uno es copia del otro.
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo(null, 8469m, false));
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo("", 8469m, false));
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo("   ", 8469m, false));
    }

    [Fact]
    public void SiElUsuarioYaConfirmo_NoSeVuelveAAvisar()
    {
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo("190900", 8469m, confirmadoPorElUsuario: true));
    }

    [Fact]
    public void CantidadNoPositiva_NoSeChequea()
    {
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo("190900", 0m, false));
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo("190900", -5m, false));
    }

    [Theory]
    [InlineData("Seguimiento reproductora #812 (devolución por eliminación)")]
    [InlineData("Seguimiento aves engorde #12680 2026-08-21 (validado)")]
    [InlineData("Devolución por quitar la validación del seguimiento")]
    public void LasReferenciasDelSISTEMA_NoAvisan(string referencia)
    {
        // Repiten clave a propósito: son las devoluciones automáticas. Avisarlas sería ruido, y un
        // índice único directamente las rompería.
        Assert.True(IngresoDuplicadoCalculos.EsReferenciaDeSistema(referencia));
        Assert.False(IngresoDuplicadoCalculos.AmeritaChequeo(referencia, 100m, false));
    }

    [Fact]
    public void UnaRemisionNormal_NoEsReferenciaDeSistema()
    {
        Assert.False(IngresoDuplicadoCalculos.EsReferenciaDeSistema("190900"));
        Assert.False(IngresoDuplicadoCalculos.EsReferenciaDeSistema("RQN 12789"));
    }

    [Fact]
    public void ElMensaje_NombraLaRemisionYElMovimientoExistente()
    {
        var msg = IngresoDuplicadoCalculos.MensajeDuplicado("190900", 8469m, "kg", 10560);

        Assert.Contains("190900", msg);
        Assert.Contains("8469", msg);
        Assert.Contains("10560", msg);
        Assert.Contains("confirmá", msg);
    }

    [Fact]
    public void ElMensaje_CaeAKilosSiNoVieneUnidad()
    {
        Assert.Contains("kg", IngresoDuplicadoCalculos.MensajeDuplicado("A-1", 10m, null, 5));
    }
}
