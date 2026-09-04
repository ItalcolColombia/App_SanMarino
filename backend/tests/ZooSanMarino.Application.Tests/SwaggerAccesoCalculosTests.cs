using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del gate de acceso a Swagger. Cubre lo que antes no tenía una sola prueba: qué se
/// protege, qué se exenta, cuándo la contraseña vale y cuándo la sesión ya venció.
/// </summary>
public class SwaggerAccesoCalculosTests
{
    private const string Password = "Swagger2024!SanMarino#API";
    private static readonly DateTime Ahora = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    // ── Rutas protegidas ───────────────────────────────────────────────

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/download")]
    [InlineData("/swagger-ui/dark.css")]
    [InlineData("/SWAGGER/INDEX.HTML")] // el gate normaliza a minúsculas
    public void RutasDeSwagger_estanProtegidas(string path) =>
        Assert.True(SwaggerAccesoCalculos.EsRutaProtegida(path));

    [Theory]
    [InlineData("/api/company")]
    [InlineData("/api/Auth/login")]
    [InlineData("/health")]
    [InlineData("/hc")]
    [InlineData("/")]
    [InlineData("")]
    [InlineData(null)]
    public void ElResto_delApi_noLoToca(string? path) =>
        Assert.False(SwaggerAccesoCalculos.EsRutaProtegida(path));

    // ── Exención del propio formulario ─────────────────────────────────

    [Fact]
    public void ElPostDelLogin_estaExento_sinoNadiePodriaEntrarNunca() =>
        Assert.True(SwaggerAccesoCalculos.EsRutaExenta("POST", "/swagger/login"));

    [Fact]
    public void ElMetodoSeComparaSinImportarMayusculas() =>
        Assert.True(SwaggerAccesoCalculos.EsRutaExenta("post", "/swagger/login"));

    [Theory]
    [InlineData("GET", "/swagger/login")]   // el formulario en sí sigue protegido
    [InlineData("POST", "/swagger")]
    [InlineData("POST", "/swagger/v1/swagger.json")]
    [InlineData("POST", null)]
    public void NadaMas_estaExento(string? metodo, string? path) =>
        Assert.False(SwaggerAccesoCalculos.EsRutaExenta(metodo, path));

    // ── Contraseña ─────────────────────────────────────────────────────

    [Fact]
    public void LaContrasenaCorrecta_entra() =>
        Assert.True(SwaggerAccesoCalculos.PasswordCorrecta(Password, Password));

    [Theory]
    [InlineData("swagger2024!sanmarino#api")] // distinta capitalización
    [InlineData("Swagger2024!SanMarino#AP")]  // prefijo correcto, incompleta
    [InlineData("Swagger2024!SanMarino#APIX")]// prefijo correcto, con cola
    [InlineData(" Swagger2024!SanMarino#API")]// con espacio: no se recorta
    [InlineData("")]
    [InlineData(null)]
    public void CualquierOtraCosa_noEntra(string? recibida) =>
        Assert.False(SwaggerAccesoCalculos.PasswordCorrecta(recibida, Password));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SinContrasenaConfigurada_noEntraNadie_niConLaDelRepositorio(string? esperada)
    {
        // Fail-closed. Antes había una constante hardcodeada de respaldo en dos archivos:
        // vaciar la configuración no cerraba la puerta, la abría con una contraseña pública.
        Assert.False(SwaggerAccesoCalculos.PasswordCorrecta(Password, esperada));
        Assert.False(SwaggerAccesoCalculos.PasswordCorrecta("cualquiera", esperada));
    }

    // ── Cookie de sesión ───────────────────────────────────────────────

    [Fact]
    public void LaCookieReciénEmitida_esValida()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);
        Assert.True(SwaggerAccesoCalculos.CookieVigente(cookie, Password, "10.0.0.5", Ahora));
    }

    [Fact]
    public void AlMinuto5_59_sigueViva_alos6_01_yaNo()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);

        Assert.True(SwaggerAccesoCalculos.CookieVigente(
            cookie, Password, "10.0.0.5", Ahora.AddMinutes(5).AddSeconds(59)));
        Assert.False(SwaggerAccesoCalculos.CookieVigente(
            cookie, Password, "10.0.0.5", Ahora.AddMinutes(6).AddSeconds(1)));
    }

    [Fact]
    public void ElVencimientoVaFirmado_noSePuedeEstirarDesdeElCliente()
    {
        // Ésta es la razón de ser del formato: antes la vigencia vivía en una segunda cookie que
        // el cliente reescribía a gusto, así que el timeout de 6 minutos no vencía nunca.
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);
        var firma = cookie.Split('.')[1];

        var estirada = $"{Ahora.AddYears(1).Ticks}.{firma}";

        Assert.False(SwaggerAccesoCalculos.CookieVigente(estirada, Password, "10.0.0.5", Ahora));
    }

    [Fact]
    public void LaCookieDeOtraIp_noSirve()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);
        Assert.False(SwaggerAccesoCalculos.CookieVigente(cookie, Password, "10.0.0.9", Ahora));
    }

    [Fact]
    public void CambiarLaContrasenaDelAmbiente_invalidaLasSesionesAbiertas()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);
        Assert.False(SwaggerAccesoCalculos.CookieVigente(cookie, "OtraDistinta", "10.0.0.5", Ahora));
    }

    [Fact]
    public void SinIp_noRevienta()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, null, Ahora);
        Assert.True(SwaggerAccesoCalculos.CookieVigente(cookie, Password, null, Ahora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-separador")]
    [InlineData("no-es-un-numero.firma")]
    [InlineData("123.demasiadas.partes")]
    [InlineData(".")]
    public void CualquierCookieMalFormada_seRechaza(string? valor) =>
        Assert.False(SwaggerAccesoCalculos.CookieVigente(valor, Password, "10.0.0.5", Ahora));

    [Fact]
    public void SinContrasenaConfigurada_ningunaCookieValida()
    {
        var cookie = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);
        Assert.False(SwaggerAccesoCalculos.CookieVigente(cookie, null, "10.0.0.5", Ahora));
        Assert.False(SwaggerAccesoCalculos.CookieVigente(cookie, "", "10.0.0.5", Ahora));
    }

    [Fact]
    public void LaSesionEsDeslizante_cadaPeticionCorreElVencimiento()
    {
        var primera = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", Ahora);

        // A los 5 minutos sigue viva y se reemite: la nueva vence 6 minutos MÁS TARDE.
        var alos5 = Ahora.AddMinutes(5);
        Assert.True(SwaggerAccesoCalculos.CookieVigente(primera, Password, "10.0.0.5", alos5));

        var renovada = SwaggerAccesoCalculos.EmitirCookie(Password, "10.0.0.5", alos5);
        Assert.True(SwaggerAccesoCalculos.CookieVigente(renovada, Password, "10.0.0.5", Ahora.AddMinutes(10)));
        Assert.False(SwaggerAccesoCalculos.CookieVigente(primera, Password, "10.0.0.5", Ahora.AddMinutes(10)));
    }
}
