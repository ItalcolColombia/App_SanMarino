using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SiloCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Decisiones puras del catálogo de silos (Santa Reyes, Fase A):
///  - el tipo se normaliza a <c>Silo</c> | <c>Bodega</c>, y el legacy <c>Insumos</c> se lee como
///    <c>Bodega</c> (es la misma ubicación, que ahora también guarda alimento);
///  - un <c>Silo</c> sale SIEMPRE de la lista maestra (para que "Silo 4" signifique lo mismo en toda
///    la empresa) y una <c>Bodega</c> se nombra a mano;
///  - generar el rango 1..100 es idempotente: los números que ya existen no se duplican.
/// </summary>
public class SiloCalculosTests
{
    // ── Normalización de tipo ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Silo")]
    [InlineData("silo")]
    [InlineData("  SILO  ")]
    public void NormalizarTipo_Silo(string entrada) => Assert.Equal(TipoSilo, NormalizarTipo(entrada));

    [Theory]
    [InlineData("Bodega")]
    [InlineData("bodega")]
    [InlineData("  BODEGA ")]
    public void NormalizarTipo_Bodega(string entrada) => Assert.Equal(TipoBodega, NormalizarTipo(entrada));

    [Theory]
    [InlineData("Insumos")]
    [InlineData("insumos")]
    public void NormalizarTipo_InsumosLegacy_EsBodega(string entrada)
    {
        // La carga inicial de Santa Reyes creó la bodega con tipo 'Insumos'. Ahora esa ubicación
        // también guarda alimento, así que es una Bodega a todos los efectos.
        Assert.Equal(TipoBodega, NormalizarTipo(entrada));
        Assert.True(EsBodega(entrada));
    }

    [Theory]
    [InlineData("Galpon")]
    [InlineData("")]
    [InlineData(null)]
    public void NormalizarTipo_Desconocido_EsNull(string? entrada) => Assert.Null(NormalizarTipo(entrada));

    [Fact]
    public void EsBodega_SoloParaBodegaEInsumos()
    {
        Assert.True(EsBodega("Bodega"));
        Assert.True(EsBodega("Insumos"));
        Assert.False(EsBodega("Silo"));
        Assert.False(EsBodega("cualquier cosa"));
    }

    // ── Nombre desde el catálogo ─────────────────────────────────────────────────

    [Fact]
    public void NombreDeCatalogo_UsaPatronPorDefecto() => Assert.Equal("Silo 4", NombreDeCatalogo(4));

    [Fact]
    public void NombreDeCatalogo_PatronPersonalizado() => Assert.Equal("SILO-12", NombreDeCatalogo(12, "SILO-{n}"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NombreDeCatalogo_PatronVacio_CaeAlDefault(string? patron)
        => Assert.Equal("Silo 7", NombreDeCatalogo(7, patron));

    // ── Validación de número ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(999)]
    public void ValidarNumero_EnRango_EsValido(int n) => Assert.Null(ValidarNumero(n));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1000)]
    public void ValidarNumero_FueraDeRango_DaError(int n) => Assert.NotNull(ValidarNumero(n));

    // ── Expansión del rango (la lista del 1 al 100) ──────────────────────────────

    [Fact]
    public void ExpandirRango_SinExistentes_DevuelveTodo()
    {
        var nuevos = ExpandirRango(1, 100, Array.Empty<int>(), out var error);
        Assert.Null(error);
        Assert.Equal(100, nuevos.Count);
        Assert.Equal(1, nuevos.First());
        Assert.Equal(100, nuevos.Last());
    }

    [Fact]
    public void ExpandirRango_OmiteLosQueYaExisten()
    {
        // Idempotencia: volver a pedir 1..10 con 1..5 ya creados solo agrega 6..10.
        var nuevos = ExpandirRango(1, 10, new[] { 1, 2, 3, 4, 5 }, out var error);
        Assert.Null(error);
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, nuevos);
    }

    [Fact]
    public void ExpandirRango_TodoExistente_DevuelveVacioSinError()
    {
        var nuevos = ExpandirRango(1, 3, new[] { 1, 2, 3 }, out var error);
        Assert.Null(error);
        Assert.Empty(nuevos);
    }

    [Fact]
    public void ExpandirRango_DesdeMayorQueHasta_DaError()
    {
        var nuevos = ExpandirRango(10, 5, Array.Empty<int>(), out var error);
        Assert.NotNull(error);
        Assert.Empty(nuevos);
    }

    [Fact]
    public void ExpandirRango_NumeroInvalido_DaError()
    {
        Assert.NotNull(Ejecutar(0, 10));
        Assert.NotNull(Ejecutar(1, 1000));

        static string? Ejecutar(int desde, int hasta)
        {
            ExpandirRango(desde, hasta, Array.Empty<int>(), out var error);
            return error;
        }
    }

    [Fact]
    public void ExpandirRango_MasDelMaximo_DaError()
    {
        // 1..999 son 999 silos: por encima del tope de 500 por llamada.
        var nuevos = ExpandirRango(1, 999, Array.Empty<int>(), out var error);
        Assert.NotNull(error);
        Assert.Empty(nuevos);
    }

    // ── Alta de un silo/bodega de granja ─────────────────────────────────────────

    [Fact]
    public void ValidarAlta_Silo_ConCatalogo_EsValido()
        => Assert.Null(ValidarAltaFarmSilo("Silo", siloCatalogoId: 3, nombre: null));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarAlta_Silo_SinCatalogo_DaError(int? catalogoId)
    {
        // Un silo sin catálogo rompería que "Silo 4" signifique lo mismo en toda la empresa.
        var error = ValidarAltaFarmSilo("Silo", catalogoId, nombre: "Silo suelto");
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidarAlta_Bodega_ConNombre_EsValido()
        => Assert.Null(ValidarAltaFarmSilo("Bodega", siloCatalogoId: null, nombre: "Bodega principal"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidarAlta_Bodega_SinNombre_DaError(string? nombre)
        => Assert.NotNull(ValidarAltaFarmSilo("Bodega", siloCatalogoId: null, nombre: nombre));

    [Fact]
    public void ValidarAlta_Bodega_ConCatalogo_DaError()
        => Assert.NotNull(ValidarAltaFarmSilo("Bodega", siloCatalogoId: 5, nombre: "Bodega"));

    [Fact]
    public void ValidarAlta_TipoInvalido_DaError()
        => Assert.NotNull(ValidarAltaFarmSilo("Galpon", siloCatalogoId: 1, nombre: "X"));

    [Fact]
    public void ValidarAlta_InsumosLegacy_SeAceptaComoBodega()
        => Assert.Null(ValidarAltaFarmSilo("Insumos", siloCatalogoId: null, nombre: "Insumos"));

    // ── Orden de presentación ────────────────────────────────────────────────────

    [Fact]
    public void ClaveOrden_BodegasPrimeroLuegoSilosPorNumero()
    {
        var items = new[]
        {
            ("Silo", (int?)10, "Silo 10"),
            ("Silo", (int?)2,  "Silo 2"),
            ("Bodega", (int?)null, "Bodega"),
            ("Silo", (int?)null, "Silo sin numero"),
        };

        var ordenados = items
            .OrderBy(x => ClaveOrden(x.Item1, x.Item2, x.Item3).Grupo)
            .ThenBy(x => ClaveOrden(x.Item1, x.Item2, x.Item3).Numero)
            .ThenBy(x => x.Item3)
            .Select(x => x.Item3)
            .ToArray();

        Assert.Equal(new[] { "Bodega", "Silo 2", "Silo 10", "Silo sin numero" }, ordenados);
    }
}
