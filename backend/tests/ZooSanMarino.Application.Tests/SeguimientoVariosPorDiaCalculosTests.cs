using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del flag <c>permite_multiples_seguimientos_diarios</c>: apagado, cada decisión es la de
/// siempre (un registro por lote y por día); encendido, el segundo registro del día pasa.
/// </summary>
public class SeguimientoVariosPorDiaCalculosTests
{
    // ── Alta de producción ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SinNadaEseDia_SeInserta(bool permiteMultiples)
    {
        Assert.Equal(AltaSeguimientoDelDia.Insertar,
            SeguimientoVariosPorDiaCalculos.ResolverAltaProduccion(false, false, permiteMultiples));
    }

    /// <summary>La fila del arrastre de huevos se mergea igual que antes, con o sin flag.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilaDeArrastre_SeMergea(bool permiteMultiples)
    {
        Assert.Equal(AltaSeguimientoDelDia.MergearSobreArrastre,
            SeguimientoVariosPorDiaCalculos.ResolverAltaProduccion(true, true, permiteMultiples));
    }

    [Fact]
    public void FlagApagado_SegundoRegistroDelDia_SeRechaza()
    {
        Assert.Equal(AltaSeguimientoDelDia.Rechazar,
            SeguimientoVariosPorDiaCalculos.ResolverAltaProduccion(true, false, false));
    }

    [Fact]
    public void FlagEncendido_SegundoRegistroDelDia_SeInserta()
    {
        Assert.Equal(AltaSeguimientoDelDia.Insertar,
            SeguimientoVariosPorDiaCalculos.ResolverAltaProduccion(true, false, true));
    }

    // ── Edición de producción ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void EdicionProduccion(bool hayOtro, bool permiteMultiples, bool esperado)
    {
        Assert.Equal(esperado,
            SeguimientoVariosPorDiaCalculos.RechazaEdicionProduccion(hayOtro, permiteMultiples));
    }

    // ── Tabla de levante ───────────────────────────────────────────────────────────────────────

    /// <summary>Flag apagado: todos los tipos siguen con un registro por día.</summary>
    [Theory]
    [InlineData("levante")]
    [InlineData("reproductora")]
    [InlineData("produccion")]
    [InlineData(null)]
    public void FlagApagado_TodoTipoMantieneUnoPorDia(string? tipo)
    {
        Assert.True(SeguimientoVariosPorDiaCalculos.AplicaUnicoPorDiaLevante(tipo, false));
    }

    [Fact]
    public void FlagEncendido_LevanteQuedaLibre()
    {
        Assert.False(SeguimientoVariosPorDiaCalculos.AplicaUnicoPorDiaLevante("levante", true));
    }

    /// <summary>Reproductora y el tipo legacy producción no cambian aunque la empresa tenga el flag.</summary>
    [Theory]
    [InlineData("reproductora")]
    [InlineData("produccion")]
    [InlineData("Levante")]
    [InlineData(null)]
    public void FlagEncendido_OtrosTiposMantienenUnoPorDia(string? tipo)
    {
        Assert.True(SeguimientoVariosPorDiaCalculos.AplicaUnicoPorDiaLevante(tipo, true));
    }
}
