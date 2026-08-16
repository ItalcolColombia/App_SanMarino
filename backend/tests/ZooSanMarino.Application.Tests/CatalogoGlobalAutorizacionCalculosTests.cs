using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.CatalogoGlobalAutorizacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Quién puede escribir los catálogos GLOBALES (permisos y menús).
/// <para>
/// Lo que se prueba acá es la frontera que hace útil a la regla: <b>administrador de la aplicación</b>
/// no es lo mismo que <b>administrador de una empresa</b>. En la base conviven media docena de roles
/// cuyo nombre empieza o termina con «Admin», y todos ellos administran su empresa, no el sistema.
/// Una comparación por substring los dejaría entrar al catálogo global — que es el error que esta
/// clase existe para impedir.
/// </para>
/// <para>
/// Espejo del front:
/// <c>frontend/src/app/features/config/role-management/funciones/catalogos-globales.funcion.ts</c>
/// (tests en <c>frontend/src/tests/catalogos-globales.funcion.spec.ts</c>).
/// </para>
/// </summary>
public class CatalogoGlobalAutorizacionCalculosTests
{
    // ─────────────────────────── Admite ───────────────────────────

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Administrador")]
    [InlineData("ADMINISTRADOR")]
    [InlineData("  Admin  ")] // espacios al borde: los recorta antes de comparar
    public void AdminDeLaAplicacion_PuedeEscribir(string rol)
    {
        Assert.True(PuedeEscribirCatalogoGlobal(new[] { rol }));
    }

    [Fact]
    public void AlcanzaConQueUnoDeSusRolesSeaAdmin()
    {
        Assert.True(PuedeEscribirCatalogoGlobal(new[] { "Consulta", "Supervisor", "Admin" }));
    }

    // ─────────────────────── Rechaza (lo importante) ───────────────────────

    /// <summary>
    /// Roles REALES de la base (refresh de producción). Todos administran <b>su empresa</b>: ninguno
    /// puede tocar el catálogo global. Si alguna vez esta prueba se pone en rojo, alguien cambió la
    /// comparación exacta por un «contains» y abrió el catálogo a media plataforma.
    /// </summary>
    [Theory]
    [InlineData("Admin Panama")]
    [InlineData("Admin Demo")]
    [InlineData("Ecuador Administrador")]
    [InlineData("Santa Reyes Administrador")]
    [InlineData("ADMINISTRADOR DE GRANJA")]
    [InlineData("Administrador de Empresa")]
    [InlineData("Colombia Administrativa")]
    [InlineData("Soporte")]
    [InlineData("Lider Funcional")]
    public void AdministradorDeEmpresa_NoPuedeEscribirElCatalogoGlobal(string rol)
    {
        Assert.False(PuedeEscribirCatalogoGlobal(new[] { rol }));
    }

    // ─────────────────────────── Fail-closed ───────────────────────────

    [Fact]
    public void SinRoles_NoPuedeEscribir()
    {
        Assert.False(PuedeEscribirCatalogoGlobal(null));
        Assert.False(PuedeEscribirCatalogoGlobal(Array.Empty<string>()));
    }

    [Fact]
    public void RolesVaciosOEnBlanco_NoPuedeEscribir()
    {
        Assert.False(PuedeEscribirCatalogoGlobal(new string?[] { null, "", "   " }));
    }

    // ─────────────────────────── Catálogo de roles ───────────────────────────

    [Fact]
    public void ElCatalogoDeRolesAdmin_EsExactamenteDos_YCompareIgnorandoMayusculas()
    {
        Assert.Equal(2, RolesAdminAplicacion.Count);
        Assert.Contains("ADMIN", RolesAdminAplicacion);
        Assert.Contains("Administrador", RolesAdminAplicacion);
    }
}
