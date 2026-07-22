// tests/ZooSanMarino.Application.Tests/RateLimitingCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class RateLimitingCalculosTests
{
    // ── Clasificación de rutas y límite aplicable ────────────────────────────

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/register")]
    [InlineData("/auth/login")]
    public void EsRutaAuth_LoginYRegister_True(string path) =>
        Assert.True(RateLimitingCalculos.EsRutaAuth(path));

    [Theory]
    [InlineData("/api/farms")]
    [InlineData("/api/auth/refresh")]  // otras rutas de auth NO llevan el límite estricto
    [InlineData("/swagger/index.html")]
    public void EsRutaAuth_OtrasRutas_False(string path) =>
        Assert.False(RateLimitingCalculos.EsRutaAuth(path));

    [Theory]
    [InlineData("/api/auth/login", 15)]     // auth
    [InlineData("/api/auth/register", 15)]  // auth
    [InlineData("/swagger/index.html", 50)] // swagger
    [InlineData("/swagger-ui/main.js", 50)] // swagger
    [InlineData("/api/lote", 100)]          // general
    public void LimiteParaRuta_SeleccionaElLimiteCorrecto(string path, int esperado) =>
        Assert.Equal(esperado, RateLimitingCalculos.LimiteParaRuta(path, 100, 15, 50));

    // ── Alcance del bloqueo ──────────────────────────────────────────────────

    [Fact]
    public void ClaveBloqueo_RutaAuth_AcotadaAAuth() =>
        Assert.Equal("blocked:auth:10.0.0.1", RateLimitingCalculos.ClaveBloqueo("10.0.0.1", esRutaAuth: true));

    [Fact]
    public void ClaveBloqueo_RutaGeneral_IpCompleta() =>
        Assert.Equal("blocked:10.0.0.1", RateLimitingCalculos.ClaveBloqueo("10.0.0.1", esRutaAuth: false));

    [Fact]
    public void ClavesAVerificar_RutaAuth_RespetaGlobalYAcotado()
    {
        var claves = RateLimitingCalculos.ClavesAVerificar("10.0.0.1", esRutaAuth: true);
        Assert.Equal(new[] { "blocked:10.0.0.1", "blocked:auth:10.0.0.1" }, claves);
    }

    [Fact]
    public void ClavesAVerificar_RutaGeneral_SoloGlobal()
    {
        var claves = RateLimitingCalculos.ClavesAVerificar("10.0.0.1", esRutaAuth: false);
        Assert.Equal(new[] { "blocked:10.0.0.1" }, claves);
    }

    [Fact]
    public void BloqueoAuth_NoAfectaRutaGeneral()
    {
        // La clave que produce una violación en login no está entre las que verifica una ruta general.
        var claveAuth = RateLimitingCalculos.ClaveBloqueo("10.0.0.1", esRutaAuth: true);
        var clavesGeneral = RateLimitingCalculos.ClavesAVerificar("10.0.0.1", esRutaAuth: false);
        Assert.DoesNotContain(claveAuth, clavesGeneral);
    }

    [Fact]
    public void BloqueoGeneral_SiAfectaRutaAuth()
    {
        var claveGlobal = RateLimitingCalculos.ClaveBloqueo("10.0.0.1", esRutaAuth: false);
        var clavesAuth = RateLimitingCalculos.ClavesAVerificar("10.0.0.1", esRutaAuth: true);
        Assert.Contains(claveGlobal, clavesAuth);
    }

    // ── Tiempos ──────────────────────────────────────────────────────────────

    [Fact]
    public void SegundosRestantes_RedondeaHaciaArriba()
    {
        var ahora = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(180, RateLimitingCalculos.SegundosRestantes(ahora, ahora.AddMinutes(3)));
        Assert.Equal(1, RateLimitingCalculos.SegundosRestantes(ahora, ahora.AddMilliseconds(200)));
    }

    [Fact]
    public void SegundosRestantes_BloqueoVencido_Cero()
    {
        var ahora = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, RateLimitingCalculos.SegundosRestantes(ahora, ahora.AddSeconds(-5)));
    }

    // ── Umbral y ventana (misma semántica que el middleware original) ────────

    [Theory]
    [InlineData(15, 15, false)] // en el límite todavía pasa (umbral estricto >)
    [InlineData(16, 15, true)]
    [InlineData(1, 15, false)]
    public void ExcedeLimite_UmbralEstricto(int contador, int limite, bool esperado) =>
        Assert.Equal(esperado, RateLimitingCalculos.ExcedeLimite(contador, limite));

    [Fact]
    public void VentanaExpirada_LimiteExactoDe60s()
    {
        var inicio = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(RateLimitingCalculos.VentanaExpirada(inicio.AddSeconds(59), inicio, 60));
        Assert.True(RateLimitingCalculos.VentanaExpirada(inicio.AddSeconds(60), inicio, 60));
    }
}
