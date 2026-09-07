using System.Security.Cryptography;
using System.Text;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la firma de plataforma por sesión (derivada del <c>jti</c>). Cubre la derivación
/// determinista, la comparación en tiempo constante y el parseo tolerante del <c>jti</c> desde el
/// header <c>Authorization</c> sin validar la firma del JWT.
/// </summary>
public class PlatformSecretCalculosTests
{
    private const string Clave = "DevOnly#DerivationKey#NOT-FOR-PROD";
    private const string Jti = "11111111-1111-1111-1111-111111111111";

    // ── DerivarClaveSesion ────────────────────────────────────────────────

    [Fact]
    public void DerivarClaveSesion_esDeterminista_yPorJti()
    {
        var a1 = PlatformSecretCalculos.DerivarClaveSesion(Jti, Clave);
        var a2 = PlatformSecretCalculos.DerivarClaveSesion(Jti, Clave);
        var b = PlatformSecretCalculos.DerivarClaveSesion("22222222-2222-2222-2222-222222222222", Clave);

        Assert.NotNull(a1);
        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);
    }

    [Fact]
    public void DerivarClaveSesion_coincideConUnHmacDeReferencia()
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Clave));
        var esperado = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(Jti)));

        Assert.Equal(esperado, PlatformSecretCalculos.DerivarClaveSesion(Jti, Clave));
    }

    [Theory]
    [InlineData(null, Clave)]
    [InlineData("", Clave)]
    [InlineData("   ", Clave)]
    [InlineData(Jti, null)]
    [InlineData(Jti, "")]
    public void DerivarClaveSesion_sinInsumos_esNull(string? jti, string? clave) =>
        Assert.Null(PlatformSecretCalculos.DerivarClaveSesion(jti, clave));

    // ── FirmasCoinciden ──────────────────────────────────────────────────

    [Fact]
    public void FirmasCoinciden_igualdad()
    {
        var firma = PlatformSecretCalculos.DerivarClaveSesion(Jti, Clave)!;
        Assert.True(PlatformSecretCalculos.FirmasCoinciden(firma, firma));
    }

    [Theory]
    [InlineData("abc", "abd")]
    [InlineData("abc", "abcd")]        // largos distintos, sin excepción
    [InlineData(null, "abc")]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    [InlineData("", "abc")]
    public void FirmasCoinciden_distintasONulas_esFalse(string? a, string? b) =>
        Assert.False(PlatformSecretCalculos.FirmasCoinciden(a, b));

    // ── LeerJtiDeAuthorizationHeader ─────────────────────────────────────

    private static string B64Url(string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Jwt(string payloadJson) =>
        $"{B64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64Url(payloadJson)}.firmaFalsaNoImporta";

    [Fact]
    public void LeerJti_deUnBearerValido()
    {
        var header = "Bearer " + Jwt($"{{\"sub\":\"x\",\"jti\":\"{Jti}\",\"exp\":123}}");
        Assert.Equal(Jti, PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header));
    }

    [Fact]
    public void LeerJti_toleraEsquemaEnMinusculaYEspacios()
    {
        var header = "  bearer   " + Jwt($"{{\"jti\":\"{Jti}\"}}");
        Assert.Equal(Jti, PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer ")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer sk_live_algun_pat_de_servicio")]
    [InlineData("Bearer no-es-un-jwt")]
    [InlineData("Bearer a.b")]                    // 2 segmentos
    [InlineData("Bearer a.b.c.d")]                // 4 segmentos
    [InlineData("Bearer aaa.@@@no-base64@@@.ccc")]
    public void LeerJti_entradasInvalidas_null(string? header) =>
        Assert.Null(PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header));

    [Fact]
    public void LeerJti_payloadSinJti_null()
    {
        var header = "Bearer " + Jwt("{\"sub\":\"x\",\"exp\":123}");
        Assert.Null(PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header));
    }

    [Fact]
    public void LeerJti_payloadJtiVacio_null()
    {
        var header = "Bearer " + Jwt("{\"jti\":\"\"}");
        Assert.Null(PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header));
    }

    [Fact]
    public void IdaYVuelta_jtiDelHeader_derivaLaMismaFirma()
    {
        var header = "Bearer " + Jwt($"{{\"jti\":\"{Jti}\"}}");
        var jtiLeido = PlatformSecretCalculos.LeerJtiDeAuthorizationHeader(header);

        var firmaDesdeHeader = PlatformSecretCalculos.DerivarClaveSesion(jtiLeido, Clave);
        var firmaDirecta = PlatformSecretCalculos.DerivarClaveSesion(Jti, Clave);

        Assert.Equal(firmaDirecta, firmaDesdeHeader);
    }
}
