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

    // ── Código ERP de la granja (Panamá): avance +1 al cerrar el ciclo ───────
    [Theory]
    [InlineData("4001017", "4001018")]  // base 17 cierra → empieza el 18
    [InlineData("4001018", "4001019")]
    [InlineData("4001099", "4001100")]  // patrón del requerimiento: 99 → 100 (toma 3 dígitos)
    [InlineData("4001100", "4001101")]  // y sigue: 100 → 101
    [InlineData("4001999", "4002000")]  // rollover natural del +1 numérico
    [InlineData("  4001017  ", "4001018")]  // trim
    [InlineData("0099", "0100")]        // conserva ceros a la izquierda (misma longitud)
    [InlineData("9", "10")]             // si el número crece, crece el código
    public void SiguienteCodigoErpGranja_AvanzaMasUno(string actual, string esperado)
    {
        Assert.Equal(esperado, SiguienteCodigoErpGranja(actual));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4001A17")]             // no numérico → no avanzar
    [InlineData("4001-17")]
    [InlineData("40010171234567890123")] // >18 dígitos → no cabe en long con margen
    public void SiguienteCodigoErpGranja_InvalidoDevuelveNull(string? actual)
    {
        Assert.Null(SiguienteCodigoErpGranja(actual));
    }

    [Theory]
    [InlineData(null, true)]            // sin código = granja sin la funcionalidad (válido)
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("4001017", true)]
    [InlineData("0099", true)]
    [InlineData("4001A17", false)]      // letras no
    [InlineData("4001 17", false)]      // espacios internos no
    [InlineData("-400117", false)]      // signo no
    [InlineData("40010171234567890123", false)] // >18 dígitos
    public void EsCodigoErpGranjaValido_SoloDigitosOMaxDieciocho(string? codigo, bool esperado)
    {
        Assert.Equal(esperado, EsCodigoErpGranjaValido(codigo));
    }
}
