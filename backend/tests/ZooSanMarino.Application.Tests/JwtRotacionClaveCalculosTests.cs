using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Options;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Rotación de la clave JWT en cada deploy: se firma con la actual y se valida con la actual y la
/// anterior. Si la anterior se pierde, cada deploy deja afuera a todos los que tienen sesión abierta.
/// </summary>
public class JwtRotacionClaveCalculosTests
{
    private const string Actual = "clave-actual-0123456789-0123456789-0123456789";
    private const string Anterior = "clave-anterior-0123456789-0123456789-01234567";

    // ───────────────────────── ClavesDeValidacion ─────────────────────────

    [Fact]
    public void Con_clave_anterior_valida_con_las_dos_y_la_actual_primero()
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, Anterior);

        Assert.Equal([Actual, Anterior], claves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_clave_anterior_valida_solo_con_la_actual(string? anterior)
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, anterior);

        Assert.Equal([Actual], claves);
    }

    [Fact]
    public void Anterior_igual_a_la_actual_no_se_duplica()
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, Actual);

        Assert.Equal([Actual], claves);
    }

    // ───────────────────────── JwtOptions.EnsureValid ─────────────────────────

    [Fact]
    public void EnsureValid_acepta_sin_clave_anterior()
    {
        Opciones(anterior: null).EnsureValid();
    }

    [Fact]
    public void EnsureValid_acepta_clave_anterior_de_32_bytes()
    {
        Opciones(anterior: new string('a', 32)).EnsureValid();
    }

    [Fact]
    public void EnsureValid_rechaza_clave_anterior_corta()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Opciones(anterior: new string('a', 31)).EnsureValid());

        Assert.Contains("PreviousKey", ex.Message);
    }

    private static JwtOptions Opciones(string? anterior) => new()
    {
        Key = Actual,
        PreviousKey = anterior,
        Issuer = "ZooSanMarino.API",
        Audience = "ZooSanMarino.Client",
        DurationInMinutes = 60,
    };
}
