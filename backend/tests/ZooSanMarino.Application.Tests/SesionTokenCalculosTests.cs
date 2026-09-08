using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// <c>SesionTokenCalculos</c> es el único productor del claim <c>jti</c> del JWT y de la firma de
/// plataforma que depende de él. Antes de este test la invariante ("misma firma que
/// <c>PlatformSecretCalculos.DerivarClaveSesion</c>") sólo la vigilaba un regex sobre el texto de
/// <c>AuthService</c> (criterio 7 de <c>verificar-superficie-produccion.js</c>): ese regex podía dar
/// falso positivo (renombrar una variable lo rompía sin que nada estuviera roto) o falso negativo
/// (miraba la forma del código, no el valor real). Estos tests ejecutan la invariante.
/// </summary>
public class SesionTokenCalculosTests
{
    private const string Clave = "DevOnly#DerivationKey#NOT-FOR-PROD";

    // ── La invariante central: misma fórmula, sin duplicarla ────────────────

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    [InlineData("aabbccdd-eeff-0011-2233-445566778899")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ConstruirDatosDeSesion_platformKey_esExactamenteDerivarClaveSesion(string jtiTexto)
    {
        // Esto es lo que el regex del gate sólo podía SUPONER por la forma del código: que el
        // PlatformKey que sale de SesionTokenCalculos es, byte a byte, el mismo que produce
        // PlatformSecretCalculos.DerivarClaveSesion sobre el mismo insumo. Acá se ejecuta y se mide.
        var jti = Guid.Parse(jtiTexto);

        var datos = SesionTokenCalculos.ConstruirDatosDeSesion(jti, Clave);
        var esperado = PlatformSecretCalculos.DerivarClaveSesion(datos.JtiClaim, Clave);

        Assert.NotNull(esperado);
        Assert.Equal(esperado, datos.PlatformKey);
    }

    [Fact]
    public void ConstruirDatosDeSesion_variosJtiAlAzar_siemprePasaLaInvariante()
    {
        // N jti generados al azar (como los emite AuthService.GenerateResponseAsync con
        // Guid.NewGuid() en cada login real), no solo casos fijos: la invariante tiene que
        // sostenerse para cualquier Guid, no solo para los de ejemplo de arriba.
        for (var i = 0; i < 50; i++)
        {
            var jti = Guid.NewGuid();
            var datos = SesionTokenCalculos.ConstruirDatosDeSesion(jti, Clave);
            var esperado = PlatformSecretCalculos.DerivarClaveSesion(datos.JtiClaim, Clave);

            Assert.Equal(esperado, datos.PlatformKey);
        }
    }

    // ── Ida y vuelta con el middleware ──────────────────────────────────────

    private static string B64Url(string s) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Jwt(string payloadJson) =>
        $"{B64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64Url(payloadJson)}.firmaFalsaNoImporta";

    [Fact]
    public void IdaYVuelta_jtiClaimEnElJwt_elMiddlewareLoLeeYDerivaLaMismaFirma()
    {
        // Este es el camino real: AuthService escribe datos.JtiClaim como claim `jti` del JWT: el
        // middleware lo tiene que poder volver a leer con LeerJtiDeAuthorizationHeader y derivar
        // EXACTAMENTE la misma firma que ya viajó en la respuesta del login como PlatformKey.
        var jti = Guid.NewGuid();
        var datos = SesionTokenCalculos.ConstruirDatosDeSesion(jti, Clave);

        var header = "Bearer " + Jwt($"{{\"sub\":\"x\",\"jti\":\"{datos.JtiClaim}\",\"exp\":123}}");
        var jtiLeidoPorElMiddleware = PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header);

        Assert.Equal(datos.JtiClaim, jtiLeidoPorElMiddleware);

        var firmaQueDerivariaElMiddleware =
            PlatformSecretCalculos.DerivarClaveSesion(jtiLeidoPorElMiddleware, Clave);
        Assert.Equal(datos.PlatformKey, firmaQueDerivariaElMiddleware);
    }

    // ── Camino legacy: sin DerivationKey no se rompe nada ───────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstruirDatosDeSesion_sinDerivationKey_platformKeyNulo_yJtiClaimIntacto(string? derivationKey)
    {
        // Sin PlatformSecret:DerivationKey configurada (hoy o en un ambiente que todavía no la
        // tiene), el login sigue funcionando: PlatformKey sale null y el frontend cae al secreto
        // estático legacy. Lo único que NO puede fallar nunca es el claim del JWT.
        var jti = Guid.NewGuid();

        var datos = SesionTokenCalculos.ConstruirDatosDeSesion(jti, derivationKey);

        Assert.Null(datos.PlatformKey);
        Assert.Equal(jti.ToString(), datos.JtiClaim);
    }

    // ── Formato congelado: es lo que viaja en el token y lo que se relee ───

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("AABBCCDD-EEFF-0011-2233-445566778899")]
    public void ConstruirDatosDeSesion_jtiClaim_esGuidToStringFormatoD(string jtiTexto)
    {
        // JtiClaim tiene que ser byte a byte jti.ToString() (formato "D": minúsculas, con guiones).
        // Es el mismo texto que se escribe en el claim `jti` del JWT y el que LeerJtiDeAuthorizationHeader
        // vuelve a leer en cada request; si el formato cambiara (mayúsculas, sin guiones, "N"), el
        // middleware seguiría funcionando pero PlatformKey emitido y el que se puede re-derivar
        // divergirían en apariencia aunque representen el mismo Guid.
        var jti = Guid.Parse(jtiTexto);

        var datos = SesionTokenCalculos.ConstruirDatosDeSesion(jti, Clave);

        Assert.Equal(jti.ToString(), datos.JtiClaim);
        Assert.Equal(jti.ToString("D"), datos.JtiClaim);
    }
}
