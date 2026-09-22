using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Options;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Rotación de la clave JWT en cada deploy: se firma con la actual y se valida con la actual y la
/// anterior. Si la anterior se pierde, cada deploy deja afuera a todos los que tienen sesión abierta;
/// si una anterior rara tumbara el arranque, la API quedaría caída sin que nadie pueda arreglarla
/// (el equipo no administra AWS). Por eso una anterior que no sirve se IGNORA.
/// </summary>
public class JwtRotacionClaveCalculosTests
{
    private const string Actual = "clave-actual-0123456789-0123456789-0123456789";
    private const string Anterior = "clave-anterior-0123456789-0123456789-01234567";

    // ───────────────────────── ClavesDeValidacion ─────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Con_clave_anterior_valida_con_las_dos_y_la_actual_primero(bool esProduccion)
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, Anterior, esProduccion);

        Assert.Equal([Actual, Anterior], claves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_clave_anterior_valida_solo_con_la_actual(string? anterior)
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, anterior, esProduccion: true);

        Assert.Equal([Actual], claves);
    }

    [Fact]
    public void Anterior_igual_a_la_actual_no_se_duplica()
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, Actual, esProduccion: true);

        Assert.Equal([Actual], claves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Anterior_de_menos_de_32_bytes_se_ignora(bool esProduccion)
    {
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, new string('a', 31), esProduccion);

        Assert.Equal([Actual], claves);
    }

    [Fact]
    public void Anterior_de_32_bytes_se_acepta()
    {
        var anterior = new string('a', 32);

        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, anterior, esProduccion: true);

        Assert.Equal([Actual, anterior], claves);
    }

    [Fact]
    public void En_produccion_una_anterior_del_repo_se_ignora()
    {
        // El primer deploy con rotación mueve a PreviousKey lo que hubiera en la TaskDef. Si fuera la
        // clave de desarrollo, aceptarla dejaría firmar tokens con una clave pública del repo.
        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(
            Actual, "ZooSanMarino_SecretKey_For_Development_Only_0123456789", esProduccion: true);

        Assert.Equal([Actual], claves);
    }

    [Fact]
    public void Fuera_de_produccion_una_anterior_del_repo_se_acepta()
    {
        const string anteriorDev = "ZooSanMarino_SecretKey_For_Development_Only_0123456789";

        var claves = JwtRotacionClaveCalculos.ClavesDeValidacion(Actual, anteriorDev, esProduccion: false);

        Assert.Equal([Actual, anteriorDev], claves);
    }

    // ───────────────────────── JwtOptions.EnsureValid ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("corta")]
    [InlineData("clave-anterior-0123456789-0123456789-01234567")]
    public void EnsureValid_nunca_falla_por_la_clave_anterior(string? anterior)
    {
        Opciones(anterior).EnsureValid();
    }

    // ───────────────────────── contrato con el deploy ─────────────────────────

    [Fact]
    public void El_deploy_rota_las_mismas_variables_que_lee_la_API()
    {
        // La API lee la sección "JwtSettings"; en ECS eso llega como JwtSettings__<Propiedad>.
        Assert.NotNull(typeof(JwtOptions).GetProperty(nameof(JwtOptions.Key)));
        Assert.NotNull(typeof(JwtOptions).GetProperty(nameof(JwtOptions.PreviousKey)));

        var script = File.ReadAllText(RutaDelRepo("backend", "scripts", "rotar-clave-jwt-taskdef.js"));
        Assert.Contains($"'JwtSettings__{nameof(JwtOptions.Key)}'", script);
        Assert.Contains($"'JwtSettings__{nameof(JwtOptions.PreviousKey)}'", script);

        var workflow = File.ReadAllText(RutaDelRepo(".github", "workflows", "deploy-production.yml"));
        Assert.Contains("node backend/scripts/rotar-clave-jwt-taskdef.js /tmp/task-def-backend.json", workflow);
        Assert.DoesNotContain("secretsmanager", workflow);
    }

    private static JwtOptions Opciones(string? anterior) => new()
    {
        Key = Actual,
        PreviousKey = anterior,
        Issuer = "ZooSanMarino.API",
        Audience = "ZooSanMarino.Client",
        DurationInMinutes = 60,
    };

    /// <summary>Sube desde el assembly hasta la raíz del repo (la que tiene <c>backend/src</c>).</summary>
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
