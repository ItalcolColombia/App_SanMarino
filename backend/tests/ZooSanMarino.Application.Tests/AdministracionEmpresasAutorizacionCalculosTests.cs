using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.AdministracionEmpresasAutorizacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Quién puede ESCRIBIR sobre las empresas: crearlas, editarlas, borrarlas y decidir sus menús y
/// permisos (<c>company_menus</c> / <c>company_permissions</c>).
/// <para>
/// La frontera que se prueba acá es la misma que en
/// <see cref="CatalogoGlobalAutorizacionCalculosTests"/> y por la misma razón, pero las
/// consecuencias son peores: quien pase este gate puede <b>reasignarse módulos a sí mismo</b> y
/// tocar los de otro país. Media docena de roles reales empiezan o terminan con «Admin» y son
/// administradores <b>de su empresa</b> — un <c>contains</c> les daría la llave de las demás.
/// </para>
/// <para>
/// El segundo eje es el <b>dato</b> <c>users.is_super_admin</c>, que viaja como claim. Es el eje
/// correcto a futuro; el nombre de rol se conserva porque es el que hoy sostiene la pantalla.
/// </para>
/// </summary>
public class AdministracionEmpresasAutorizacionCalculosTests
{
    // ─────────────────────────── Admite ───────────────────────────

    /// <summary>
    /// El super admin entra por el DATO, sin depender de cómo se llame su rol. Es el caso que
    /// sobrevive a que alguien renombre el rol <c>Admin</c>.
    /// </summary>
    [Fact]
    public void SuperAdmin_PuedeAunqueNingunRolSeaAdmin()
    {
        Assert.True(PuedeAdministrarEmpresas(esSuperAdmin: true, new[] { "Consulta", "Supervisor" }));
    }

    [Fact]
    public void SuperAdmin_PuedeAunqueNoTengaNingunRol()
    {
        Assert.True(PuedeAdministrarEmpresas(esSuperAdmin: true, Array.Empty<string?>()));
        Assert.True(PuedeAdministrarEmpresas(esSuperAdmin: true, null));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Administrador")]
    [InlineData("  Admin  ")] // espacios al borde: los recorta antes de comparar
    public void AdminDeLaAplicacion_PuedeSinSerSuperAdmin(string rol)
    {
        Assert.True(PuedeAdministrarEmpresas(esSuperAdmin: false, new[] { rol }));
    }

    [Fact]
    public void AlcanzaConQueUnoDeSusRolesSeaAdmin()
    {
        Assert.True(PuedeAdministrarEmpresas(
            esSuperAdmin: false, new[] { "Consulta", "Supervisor", "Admin" }));
    }

    // ─────────────────────── Rechaza (lo importante) ───────────────────────

    /// <summary>
    /// Los seis nombres de la base que un <c>contains</c> dejaría pasar. Si alguien afloja la
    /// comparación, estos se ponen en rojo.
    /// </summary>
    [Theory]
    [InlineData("Admin Panama")]
    [InlineData("Admin Demo")]
    [InlineData("Ecuador Administrador")]
    [InlineData("Santa Reyes Administrador")]
    [InlineData("ADMINISTRADOR DE GRANJA")]
    [InlineData("Administrador de Empresa")]
    public void AdministradorDeUnaEmpresa_NoPuedeAdministrarEmpresas(string rol)
    {
        Assert.False(PuedeAdministrarEmpresas(esSuperAdmin: false, new[] { rol }));
    }

    /// <summary>
    /// El rol que motivó todo esto: el encargado de soporte de una sola empresa. Ve el módulo de
    /// usuarios y roles de la suya, y no toca ninguna empresa.
    /// </summary>
    [Fact]
    public void SoporteDeUnaEmpresa_NoPuede()
    {
        Assert.False(PuedeAdministrarEmpresas(
            esSuperAdmin: false, new[] { "Soporte Sanmarino", "Consulta" }));
    }

    [Fact]
    public void FailClosed_SinMarcaYSinRoles()
    {
        Assert.False(PuedeAdministrarEmpresas(esSuperAdmin: false, null));
        Assert.False(PuedeAdministrarEmpresas(esSuperAdmin: false, Array.Empty<string?>()));
        Assert.False(PuedeAdministrarEmpresas(esSuperAdmin: false, new string?[] { null, "", "   " }));
    }

    // ─────────────────── Lectura del claim is_super_admin ───────────────────

    /// <summary>
    /// El claim viaja como cadena. La comparación es exactamente la que hace
    /// <c>AuthController</c> al poblar <c>GET /auth/profile</c>: cualquier cosa que no sea
    /// «true» es <c>false</c>, incluido el <c>"false"</c> explícito que emite <c>AuthService</c>
    /// para todos los demás usuarios.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("1", false)]
    [InlineData("yes", false)]
    [InlineData(null, false)]
    public void LeerMarcaSuperAdmin_FailClosed(string? valor, bool esperado)
    {
        Assert.Equal(esperado, LeerMarcaSuperAdmin(valor));
    }
}
