using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Guarda de arranque de producción: la API no puede firmar JWT con una clave que vive en el repo.
///
/// <para>
/// El caso real que la motiva: <c>appsettings.json</c> viaja dentro de la imagen con la clave de
/// desarrollo, y si la TaskDef de ECS no define <c>JwtSettings__Key</c> .NET cae en ella sin avisar.
/// Por eso los tests leen las claves <b>reales</b> de los <c>appsettings*.json</c> versionados: si
/// alguien cambia la clave de desarrollo por una sin marca, este test lo corta.
/// </para>
/// </summary>
public class JwtClaveProduccionCalculosTests
{
    /// <summary>
    /// El secreto de Secrets Manager que rota el pipeline. Tiene que ser el mismo en el workflow y en
    /// el ejemplo de la TaskDef: si divergen, el deploy rota un secreto que la API no lee.
    /// </summary>
    private const string SecretoJwt = "sanmarino/produccion/jwt-key";

    // ───────────────────────── claves del repo ─────────────────────────

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.json.example")]
    [InlineData("appsettings.Development.json.example")]
    public void Clave_versionada_en_el_repo_se_rechaza(string archivo)
    {
        var clave = ClaveJwtDe(archivo);

        Assert.NotNull(JwtClaveProduccionCalculos.MotivoRechazo(clave));
    }

    [Fact]
    public void El_workflow_y_el_ejemplo_de_TaskDef_usan_el_mismo_secreto()
    {
        var workflow = File.ReadAllText(RutaDelRepo(".github", "workflows", "deploy-production.yml"));
        var ejemplo = File.ReadAllText(RutaDelRepo("backend", "deploy", "jwt-produccion.example.md"));

        Assert.Contains($"JWT_SECRET_ID: {SecretoJwt}", workflow);
        Assert.Contains($"secret:{SecretoJwt}-", ejemplo);
        Assert.Contains("JwtSettings__PreviousKey", workflow);
        Assert.Contains("JwtSettings__PreviousKey", ejemplo);
    }

    [Fact]
    public void El_motivo_nombra_la_clave_que_se_evaluo()
    {
        var motivo = JwtClaveProduccionCalculos.MotivoRechazo("zzzz_Development_zzzz", "JwtSettings:PreviousKey");

        Assert.NotNull(motivo);
        Assert.Contains("JwtSettings:PreviousKey", motivo);
        Assert.Contains("JwtSettings__PreviousKey", motivo);
    }

    // ───────────────────────── marcas y vacíos ─────────────────────────

    [Theory]
    [InlineData("YOUR_JWT_SECRET_KEY_HERE_0123456789")]
    [InlineData("CHANGE_ME_CHANGE_ME_CHANGE_ME_CHANGE_ME")]
    [InlineData("zzzz-development-zzzz-zzzz-zzzz-zzzz-zzzz")]
    [InlineData("ZZZZ_DEVELOPMENT_ONLY_ZZZZ_ZZZZ_ZZZZ_ZZZZ")]
    [InlineData("reemplazar_con_clave_generada_de_64_bytes")]
    public void Marca_no_productiva_se_rechaza_sin_importar_mayusculas(string clave)
    {
        Assert.NotNull(JwtClaveProduccionCalculos.MotivoRechazo(clave));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clave_ausente_se_rechaza(string? clave)
    {
        Assert.NotNull(JwtClaveProduccionCalculos.MotivoRechazo(clave));
    }

    [Fact]
    public void El_motivo_nunca_incluye_la_clave()
    {
        var clave = ClaveJwtDe("appsettings.json");

        var motivo = JwtClaveProduccionCalculos.MotivoRechazo(clave);

        Assert.NotNull(motivo);
        Assert.DoesNotContain(clave, motivo);
    }

    // ───────────────────────── claves aleatorias ─────────────────────────

    [Fact]
    public void Clave_aleatoria_de_64_bytes_en_base64_se_acepta()
    {
        // Semilla fija: el test es determinista. 500 claves como las que genera el ejemplo
        // (64 bytes aleatorios en base64 estándar = 88 caracteres).
        var rnd = new Random(20260922);
        var bytes = new byte[64];

        for (var i = 0; i < 500; i++)
        {
            rnd.NextBytes(bytes);
            var clave = Convert.ToBase64String(bytes);

            Assert.Null(JwtClaveProduccionCalculos.MotivoRechazo(clave));
        }
    }

    // ───────────────────────── helpers ─────────────────────────

    private static string ClaveJwtDe(string archivo)
    {
        var json = File.ReadAllText(RutaDelRepo("backend", "src", "ZooSanMarino.API", archivo));
        using var doc = JsonDocument.Parse(json.TrimStart('﻿'));
        return doc.RootElement.GetProperty("JwtSettings").GetProperty("Key").GetString() ?? "";
    }

    /// <summary>
    /// Sube desde el assembly hasta la raíz del repo (la que tiene <c>backend/src</c>). El binario
    /// cambia de carpeta según configuración y TFM, así que no se usa una ruta relativa fija.
    /// </summary>
    private static string RutaDelRepo(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "backend", "src")))
                return Path.Combine([dir.FullName, .. partes]);
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontró la raíz del repo subiendo desde " + AppContext.BaseDirectory + ".");
    }
}
