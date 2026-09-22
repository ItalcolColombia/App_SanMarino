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
    /// <summary>El placeholder de <c>backend/deploy/jwt-produccion.example.md</c>.</summary>
    private const string PlaceholderDelEjemplo = "REEMPLAZAR_CON_CLAVE_GENERADA_DE_64_BYTES";

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
    public void Placeholder_del_ejemplo_de_TaskDef_se_rechaza()
    {
        Assert.NotNull(JwtClaveProduccionCalculos.MotivoRechazo(PlaceholderDelEjemplo));
    }

    [Fact]
    public void El_ejemplo_de_TaskDef_usa_el_placeholder_que_la_regla_rechaza()
    {
        var ejemplo = File.ReadAllText(RutaDelRepo("backend", "deploy", "jwt-produccion.example.md"));

        Assert.Contains(PlaceholderDelEjemplo, ejemplo);
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
