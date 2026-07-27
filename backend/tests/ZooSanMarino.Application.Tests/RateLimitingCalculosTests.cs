// tests/ZooSanMarino.Application.Tests/RateLimitingCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class RateLimitingCalculosTests
{
    private const string Ip = "10.0.0.1";

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
    [InlineData("/api/sync")]
    [InlineData("/api/sync/push")]
    [InlineData("/api/sync/pull")]
    [InlineData("/api/sync/telemetria")]
    public void EsRutaSync_RutasDeSincronizacion_True(string path) =>
        Assert.True(RateLimitingCalculos.EsRutaSync(path));

    [Theory]
    [InlineData("/api/synchro")]        // prefijo parecido, NO es sync
    [InlineData("/api/lote")]
    [InlineData("/api/auth/login")]
    public void EsRutaSync_OtrasRutas_False(string path) =>
        Assert.False(RateLimitingCalculos.EsRutaSync(path));

    [Theory]
    [InlineData("/api/auth/login", 15)]     // auth
    [InlineData("/api/auth/register", 15)]  // auth
    [InlineData("/swagger/index.html", 50)] // swagger
    [InlineData("/swagger-ui/main.js", 50)] // swagger
    [InlineData("/api/lote", 100)]          // general
    [InlineData("/api/sync/push", 300)]     // sync
    public void LimiteParaRuta_SeleccionaElLimiteCorrecto(string path, int esperado) =>
        Assert.Equal(esperado, RateLimitingCalculos.LimiteParaRuta(path, 100, 15, 50, 300));

    [Theory]
    [InlineData("/api/auth/login", AlcanceRateLimit.Auth)]
    [InlineData("/api/sync/push", AlcanceRateLimit.Sync)]
    [InlineData("/api/lote", AlcanceRateLimit.General)]
    [InlineData("/swagger/index.html", AlcanceRateLimit.General)]
    public void AlcanceDeRuta_Clasifica(string path, AlcanceRateLimit esperado) =>
        Assert.Equal(esperado, RateLimitingCalculos.AlcanceDeRuta(path));

    // ── Identidad del cliente ────────────────────────────────────────────────

    [Fact]
    public void IdentidadCliente_Sync_UsaElDispositivo() =>
        Assert.Equal("dev:tablet-7", RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-7"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IdentidadCliente_SyncSinDispositivo_CaeALaIp(string? deviceId) =>
        Assert.Equal(Ip, RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, deviceId));

    [Theory]
    [InlineData(AlcanceRateLimit.General)]
    [InlineData(AlcanceRateLimit.Auth)]
    public void IdentidadCliente_NoSync_SiempreLaIpAunqueMandeDispositivo(AlcanceRateLimit alcance) =>
        Assert.Equal(Ip, RateLimitingCalculos.IdentidadCliente(alcance, Ip, "tablet-7"));

    // ── Alcance del bloqueo ──────────────────────────────────────────────────

    [Fact]
    public void ClaveBloqueo_RutaAuth_AcotadaAAuth() =>
        Assert.Equal("blocked:auth:10.0.0.1", RateLimitingCalculos.ClaveBloqueo(Ip, AlcanceRateLimit.Auth));

    [Fact]
    public void ClaveBloqueo_RutaGeneral_IpCompleta() =>
        Assert.Equal("blocked:10.0.0.1", RateLimitingCalculos.ClaveBloqueo(Ip, AlcanceRateLimit.General));

    [Fact]
    public void ClaveBloqueo_RutaSync_AcotadaAlDispositivo() =>
        Assert.Equal("blocked:sync:dev:tablet-7", RateLimitingCalculos.ClaveBloqueo("dev:tablet-7", AlcanceRateLimit.Sync));

    [Fact]
    public void ClavesAVerificar_RutaAuth_RespetaGlobalYAcotado()
    {
        var claves = RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.Auth, Ip);
        Assert.Equal(new[] { "blocked:10.0.0.1", "blocked:auth:10.0.0.1" }, claves);
    }

    [Fact]
    public void ClavesAVerificar_RutaGeneral_SoloGlobal()
    {
        var claves = RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.General, Ip);
        Assert.Equal(new[] { "blocked:10.0.0.1" }, claves);
    }

    [Fact]
    public void BloqueoAuth_NoAfectaRutaGeneral()
    {
        // La clave que produce una violación en login no está entre las que verifica una ruta general.
        var claveAuth = RateLimitingCalculos.ClaveBloqueo(Ip, AlcanceRateLimit.Auth);
        var clavesGeneral = RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.General, Ip);
        Assert.DoesNotContain(claveAuth, clavesGeneral);
    }

    [Fact]
    public void BloqueoGeneral_SiAfectaRutaAuth()
    {
        var claveGlobal = RateLimitingCalculos.ClaveBloqueo(Ip, AlcanceRateLimit.General);
        var clavesAuth = RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.Auth, Ip);
        Assert.Contains(claveGlobal, clavesAuth);
    }

    // ── El escenario que motiva el alcance Sync ──────────────────────────────
    // Cinco tablets de la misma granja, mismo módem (misma IP pública), drenando su cola.

    [Fact]
    public void Sync_CadaDispositivoTieneSuPropioContador()
    {
        var tabletA = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-A");
        var tabletB = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-B");

        Assert.NotEqual(tabletA, tabletB);
        Assert.NotEqual(
            RateLimitingCalculos.ClaveBloqueo(tabletA, AlcanceRateLimit.Sync),
            RateLimitingCalculos.ClaveBloqueo(tabletB, AlcanceRateLimit.Sync));
    }

    [Fact]
    public void Sync_UnDispositivoSaturado_NoBloqueaALosDemasNiALaIp()
    {
        var tabletA = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-A");
        var tabletB = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-B");

        var claveDeA = RateLimitingCalculos.ClaveBloqueo(tabletA, AlcanceRateLimit.Sync);

        // Otro dispositivo sincronizando desde la MISMA IP no mira la clave de A...
        Assert.DoesNotContain(claveDeA, RateLimitingCalculos.ClavesAVerificar(tabletB, AlcanceRateLimit.Sync, Ip));
        // ...y el login de cualquiera desde esa IP tampoco.
        Assert.DoesNotContain(claveDeA, RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.Auth, Ip));
        Assert.DoesNotContain(claveDeA, RateLimitingCalculos.ClavesAVerificar(Ip, AlcanceRateLimit.General, Ip));
    }

    [Fact]
    public void Sync_NoQuedaAtrapadaPorUnBloqueoGeneralDeLaIp()
    {
        // Aislamiento deliberado: una ráfaga cualquiera desde el módem de la granja no puede
        // impedir que las tablets suban capturas de campo, que es el dato que no se recupera.
        var claveGlobal = RateLimitingCalculos.ClaveBloqueo(Ip, AlcanceRateLimit.General);
        var identidadSync = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, "tablet-A");

        Assert.DoesNotContain(claveGlobal, RateLimitingCalculos.ClavesAVerificar(identidadSync, AlcanceRateLimit.Sync, Ip));
    }

    [Fact]
    public void Sync_SinCabeceraDeDispositivo_SigueLimitadoPorIp()
    {
        // Fail-safe: peor precisión, nunca ausencia de límite.
        var identidad = RateLimitingCalculos.IdentidadCliente(AlcanceRateLimit.Sync, Ip, null);
        Assert.Equal(new[] { "blocked:sync:10.0.0.1" },
            RateLimitingCalculos.ClavesAVerificar(identidad, AlcanceRateLimit.Sync, Ip));
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
