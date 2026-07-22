using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.GestionLotesEngordeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas puras de la numeración de "corrida" (solo Panamá) del Lote de pollo engorde:
///  - siguiente número = MAX actual (por lote base + galpón) + 1, arrancando en 1,
///  - nombre a mostrar = "{lote base} - {numero}".
/// </summary>
public class GestionLotesEngordeCalculosTests
{
    // ── Siguiente número de corrida ──────────────────────────────────────────
    [Theory]
    [InlineData(null, 1)]  // sin lotes previos de esa base en ese galpón → arranca en 1
    [InlineData(1, 2)]     // ya existe "96 - 1" → siguiente 2
    [InlineData(2, 3)]     // ya existe "96 - 2" → siguiente 3
    [InlineData(9, 10)]
    public void SiguienteNumeroCorrida_EsMaxMasUno(int? maxActual, int esperado)
    {
        Assert.Equal(esperado, SiguienteNumeroCorrida(maxActual));
    }

    // ── Armado del nombre ────────────────────────────────────────────────────
    [Theory]
    [InlineData("96", 1, "96 - 1")]
    [InlineData("94", 2, "94 - 2")]
    [InlineData("A-100", 3, "A-100 - 3")]
    public void ConstruirNombreCorrida_ConcatenaBaseYNumero(string baseNombre, int numero, string esperado)
    {
        Assert.Equal(esperado, ConstruirNombreCorrida(baseNombre, numero));
    }

    [Fact]
    public void ConstruirNombreCorrida_RecortaEspaciosDelBase()
    {
        Assert.Equal("96 - 1", ConstruirNombreCorrida("  96  ", 1));
    }

    [Fact]
    public void ConstruirNombreCorrida_BaseNuloNoRompe()
    {
        // Base nulo/vacío no debería ocurrir (siempre se resuelve), pero no debe lanzar NRE.
        Assert.Equal(" - 1", ConstruirNombreCorrida(null!, 1));
    }
}
