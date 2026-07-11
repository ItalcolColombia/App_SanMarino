using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la lógica PURA de tokens de servicio (PAT): generación, hashing y verificación.
/// Sin EF: valida el contrato que el ServiceTokenService y el ServiceTokenAuthHandler reutilizan.
/// </summary>
public class ServiceTokenHasherTests
{
    [Fact]
    public void Hash_EsDeterministico()
    {
        const string plano = "sk_abc123";
        Assert.Equal(ServiceTokenHasher.Hash(plano), ServiceTokenHasher.Hash(plano));
    }

    [Fact]
    public void Hash_DiferentesEntradas_DanDiferentesHashes()
    {
        Assert.NotEqual(ServiceTokenHasher.Hash("sk_uno"), ServiceTokenHasher.Hash("sk_dos"));
    }

    [Fact]
    public void Hash_EsHexMinuscula_De64Chars()
    {
        var hash = ServiceTokenHasher.Hash("sk_algo");
        Assert.Equal(64, hash.Length); // SHA-256 → 32 bytes → 64 hex chars
        Assert.All(hash, c => Assert.True("0123456789abcdef".Contains(c)));
    }

    [Fact]
    public void Verify_TokenCorrecto_DevuelveTrue()
    {
        var plano = ServiceTokenHasher.GenerateToken();
        var hash = ServiceTokenHasher.Hash(plano);
        Assert.True(ServiceTokenHasher.Verify(plano, hash));
    }

    [Fact]
    public void Verify_HashDistinto_DevuelveFalse()
    {
        var plano = ServiceTokenHasher.GenerateToken();
        var hashDeOtro = ServiceTokenHasher.Hash(ServiceTokenHasher.GenerateToken());
        Assert.False(ServiceTokenHasher.Verify(plano, hashDeOtro));
    }

    [Theory]
    [InlineData(null, "abc")]
    [InlineData("sk_x", null)]
    [InlineData("", "abc")]
    [InlineData("sk_x", "")]
    public void Verify_NullsOVacios_DevuelveFalse(string? plano, string? hash)
    {
        Assert.False(ServiceTokenHasher.Verify(plano, hash));
    }

    [Fact]
    public void GenerateToken_EmpiezaConPrefijo_YLongitudEsperada()
    {
        var token = ServiceTokenHasher.GenerateToken();
        Assert.StartsWith("sk_", token);
        Assert.Equal(ServiceTokenHasher.Prefix, "sk_");
        // "sk_" (3) + Base64Url de 32 bytes sin padding (43) = 46 chars.
        Assert.Equal(46, token.Length);
        // Base64Url: sin '+', '/', ni padding '='.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void GenerateToken_DosLlamadas_ProducenTokensDistintos()
    {
        Assert.NotEqual(ServiceTokenHasher.GenerateToken(), ServiceTokenHasher.GenerateToken());
    }
}
