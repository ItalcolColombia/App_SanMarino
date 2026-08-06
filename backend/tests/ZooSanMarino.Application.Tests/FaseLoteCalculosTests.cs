using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La fase indicada manda; sin fase indicada se conserva EXACTAMENTE la derivación previa
/// (≥ 26 semanas ⇒ Producción), que es lo que garantiza que ninguna empresa cambie de
/// comportamiento por este agregado.
/// </summary>
public class FaseLoteCalculosTests
{
    // ── Derivación histórica: no puede cambiar ────────────────────────────────
    [Theory]
    [InlineData(0, "Levante")]
    [InlineData(1, "Levante")]
    [InlineData(25, "Levante")]
    [InlineData(26, "Produccion")]
    [InlineData(27, "Produccion")]
    [InlineData(52, "Produccion")]
    public void DerivarPorEdad_conserva_el_corte_en_26_semanas(int semanas, string esperada)
    {
        Assert.Equal(esperada, FaseLoteCalculos.DerivarPorEdad(semanas));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolver_sin_fase_indicada_deriva_igual_que_antes(string? indicada)
    {
        for (var semanas = 0; semanas <= 60; semanas++)
            Assert.Equal(FaseLoteCalculos.DerivarPorEdad(semanas), FaseLoteCalculos.Resolver(indicada, semanas));
    }

    // ── Fase indicada ─────────────────────────────────────────────────────────
    [Fact]
    public void Resolver_respeta_la_fase_indicada_aunque_la_edad_diga_otra_cosa()
    {
        // El caso que motiva el cambio: histórico encasetado hace 49 semanas que se carga como
        // LEVANTE para que los reportes de levante lo vean.
        Assert.Equal("Levante", FaseLoteCalculos.Resolver("Levante", semanasDesdeEncaset: 49));
        Assert.Equal("Produccion", FaseLoteCalculos.Resolver("Produccion", semanasDesdeEncaset: 3));
    }

    [Theory]
    [InlineData("levante", "Levante")]
    [InlineData("LEVANTE", "Levante")]
    [InlineData("  Levante  ", "Levante")]
    [InlineData("produccion", "Produccion")]
    [InlineData("PRODUCCION", "Produccion")]
    public void NormalizarFaseIndicada_acepta_mayusculas_y_espacios(string entrada, string esperada)
    {
        Assert.Equal(esperada, FaseLoteCalculos.NormalizarFaseIndicada(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizarFaseIndicada_vacia_devuelve_null(string? entrada)
    {
        Assert.Null(FaseLoteCalculos.NormalizarFaseIndicada(entrada));
    }

    [Theory]
    [InlineData("Engorde")]
    [InlineData("Postura")]
    [InlineData("Producción")]   // con tilde: no es el valor que guarda la columna
    [InlineData("x")]
    public void NormalizarFaseIndicada_rechaza_cualquier_otro_valor(string entrada)
    {
        var ex = Assert.Throws<ArgumentException>(() => FaseLoteCalculos.NormalizarFaseIndicada(entrada));
        Assert.Contains("Levante", ex.Message);
        Assert.Contains("Produccion", ex.Message);
    }

    [Fact]
    public void Validas_son_exactamente_los_dos_valores_que_acepta_la_columna()
    {
        Assert.Equal(new[] { "Levante", "Produccion" }, FaseLoteCalculos.Validas);
    }
}
