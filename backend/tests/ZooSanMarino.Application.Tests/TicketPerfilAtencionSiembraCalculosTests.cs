using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la siembra del perfil de atención de tickets de una empresa nueva.
/// </summary>
/// <remarks>
/// Lo que estos tests sostienen es la frontera del nombre de rol: en la base hay seis roles cuyo
/// nombre contiene «Admin» pero que administran SU empresa. Si alguien cambia la comparación exacta
/// por un <c>Contains</c>, la empresa nueva nacería asignándose a sí misma los casos de desarrollo y
/// estos tests se ponen en rojo.
/// </remarks>
public class TicketPerfilAtencionSiembraCalculosTests
{
    // Los seis roles reales de la base cuyo nombre contiene «Admin» sin ser el equipo de desarrollo.
    private static readonly (int Id, string? Nombre)[] RolesDeEmpresa =
    {
        (22, "Admin Panama"),
        (23, "Admin Demo"),
        (10, "Ecuador Administrador"),
        (30, "Santa Reyes Administrador"),
        (18, "ADMINISTRADOR DE GRANJA"),
        (36, "Administrador de Empresa"),
    };

    [Fact]
    public void Empresa_vacia_solo_siembra_el_rol_global_y_sus_cuatro_tipos()
    {
        var roles = RolesDeEmpresa.Append((1, "Admin")).ToArray();

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes: null);

        Assert.Equal(4, filas.Count);
        Assert.All(filas, f => Assert.Equal(1, f.RoleId));
        Assert.Equal(
            new[] { TicketTipos.Soporte, TicketTipos.Dudas, TicketTipos.Desarrollo, TicketTipos.Requerimiento },
            filas.Select(f => f.Tipo));
    }

    [Fact]
    public void Sin_rol_global_no_se_inventa_resolutor()
    {
        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(RolesDeEmpresa, existentes: null);

        Assert.Empty(filas);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("  Admin  ")]
    [InlineData("Administrador")]
    [InlineData("administrador")]
    public void El_nombre_global_se_reconoce_exacto_sin_mayusculas_y_con_trim(string nombre)
        => Assert.True(TicketPerfilAtencionSiembraCalculos.EsResolutorGlobal(nombre));

    [Theory]
    [InlineData("Admin Panama")]
    [InlineData("Santa Reyes Administrador")]
    [InlineData("ADMINISTRADOR DE GRANJA")]
    [InlineData("Implementador Sanmarino Colombia")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Un_administrador_de_empresa_no_es_el_resolutor_global(string? nombre)
        => Assert.False(TicketPerfilAtencionSiembraCalculos.EsResolutorGlobal(nombre));

    [Fact]
    public void Lo_ya_configurado_no_se_vuelve_a_proponer()
    {
        var roles = new (int, string?)[] { (1, "Admin") };
        var existentes = new[] { (1, TicketTipos.Desarrollo) };

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes);

        Assert.Equal(3, filas.Count);
        Assert.DoesNotContain(filas, f => f.Tipo == TicketTipos.Desarrollo);
    }

    [Fact]
    public void Empresa_ya_completa_no_genera_nada()
    {
        var roles = new (int, string?)[] { (1, "Admin") };
        var existentes = TicketPerfilAtencionSiembraCalculos.TiposEmpresaNueva
            .Select(t => (1, t))
            .ToArray();

        Assert.Empty(TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes));
    }

    [Fact]
    public void El_tipo_ya_configurado_se_compara_normalizado()
    {
        var roles = new (int, string?)[] { (1, "Admin") };
        // Tal como podría venir de una fila escrita a mano: minúsculas y con espacios.
        var existentes = new[] { (1, " desarrollo "), (1, "soporte") };

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes);

        Assert.Equal(new[] { TicketTipos.Dudas, TicketTipos.Requerimiento }, filas.Select(f => f.Tipo));
    }

    [Fact]
    public void Dos_roles_globales_reciben_cada_uno_sus_cuatro_tipos_sin_duplicar()
    {
        var roles = new (int, string?)[] { (7, "administrador"), (1, "Admin"), (1, "Admin") };

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes: null);

        Assert.Equal(8, filas.Count);
        Assert.Equal(filas.Count, filas.Distinct().Count());
        // Orden determinista: por rol y, dentro del rol, por el orden declarado de los tipos.
        Assert.Equal(new[] { 1, 1, 1, 1, 7, 7, 7, 7 }, filas.Select(f => f.RoleId));
    }

    [Fact]
    public void Entradas_nulas_o_en_blanco_no_lanzan()
    {
        Assert.Empty(TicketPerfilAtencionSiembraCalculos.FilasFaltantes(null, null));
        Assert.Empty(TicketPerfilAtencionSiembraCalculos.FilasFaltantes(Array.Empty<(int, string?)>(), null));

        var roles = new (int, string?)[] { (1, "Admin"), (2, null), (3, "  ") };
        var existentes = new[] { (1, ""), (1, "   ") };

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(roles, existentes);

        Assert.Equal(4, filas.Count);
        Assert.All(filas, f => Assert.Equal(1, f.RoleId));
    }

    [Fact]
    public void Los_tipos_sembrados_son_exactamente_el_catalogo_del_dominio()
    {
        Assert.Equal(
            TicketTipos.Todos.OrderBy(t => t, StringComparer.Ordinal),
            TicketPerfilAtencionSiembraCalculos.TiposEmpresaNueva.OrderBy(t => t, StringComparer.Ordinal));
    }
}
