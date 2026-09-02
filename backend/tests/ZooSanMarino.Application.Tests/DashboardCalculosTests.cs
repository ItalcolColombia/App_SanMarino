using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del gating del dashboard. Espejan caso por caso a
/// <c>features/dashboard/funciones/resolver-paneles-visibles.spec.ts</c>: la regla de match vive en
/// los dos lados (el front decide qué dibuja, el backend corta igual) y los dos tienen que
/// contestar lo mismo ante la misma entrada.
///
/// Regla de oro: <b>fail-closed</b>. Sin menú, sin módulo declarado o con la route equivocada, la
/// respuesta es «no», nunca «todo».
/// </summary>
public class DashboardCalculosTests
{
    // ─────────────────────────────────────────────────────────── NormalizarRoute

    [Theory]
    [InlineData("/daily-log/seguimiento", "/daily-log/seguimiento")]
    [InlineData("/Daily-Log/Seguimiento", "/daily-log/seguimiento")]   // mayúsculas
    [InlineData("/daily-log/seguimiento/", "/daily-log/seguimiento")]  // barra final
    [InlineData("/daily-log/seguimiento///", "/daily-log/seguimiento")] // varias barras
    [InlineData("  /daily-log/seguimiento  ", "/daily-log/seguimiento")] // espacios
    [InlineData("daily-log/seguimiento", "/daily-log/seguimiento")]    // sin barra inicial
    [InlineData("/", "/")]                                             // la raíz se conserva
    public void NormalizarRoute_deja_la_forma_canonica(string entrada, string esperado)
        => Assert.Equal(esperado, DashboardCalculos.NormalizarRoute(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizarRoute_sin_contenido_devuelve_null(string? entrada)
        => Assert.Null(DashboardCalculos.NormalizarRoute(entrada));

    // ─────────────────────────────────────────────────────────── Cubre

    [Fact]
    public void Cubre_la_misma_route()
        => Assert.True(DashboardCalculos.Cubre("/vacunacion", "/vacunacion"));

    [Fact]
    public void Cubre_un_descendiente_al_modulo_padre()
    {
        Assert.True(DashboardCalculos.Cubre("/vacunacion/cronograma", "/vacunacion"));
        Assert.True(DashboardCalculos.Cubre("/vacunacion/reportes/detalle", "/vacunacion"));
    }

    [Fact]
    public void No_cubre_al_reves_el_padre_no_alcanza_al_hijo()
        => Assert.False(DashboardCalculos.Cubre("/vacunacion", "/vacunacion/reportes"));

    [Fact]
    public void No_cubre_cuando_solo_comparte_prefijo_de_texto()
    {
        // Sin la barra en el prefijo, estos dos pasarían y son módulos distintos.
        Assert.False(DashboardCalculos.Cubre("/vacunacion-historica", "/vacunacion"));
        Assert.False(DashboardCalculos.Cubre("/gestion-inventarios-viejo", "/gestion-inventario"));
    }

    [Fact]
    public void Cubre_es_fail_closed_ante_nulos()
    {
        Assert.False(DashboardCalculos.Cubre(null, "/vacunacion"));
        Assert.False(DashboardCalculos.Cubre("/vacunacion", null));
        Assert.False(DashboardCalculos.Cubre("", ""));
    }

    // ─────────────────────────────────────────────────────────── TieneAlgunModulo

    [Fact]
    public void TieneAlgunModulo_con_una_sola_route_del_panel_alcanza()
    {
        var menu = new[] { "/daily-log/produccion" };
        Assert.True(DashboardCalculos.TieneAlgunModulo(menu, DashboardCalculos.ModulosPanel.Postura));
    }

    [Fact]
    public void TieneAlgunModulo_sin_ninguna_route_del_panel_dice_que_no()
    {
        var menu = new[] { "/config/users", "/config/companies" };
        Assert.False(DashboardCalculos.TieneAlgunModulo(menu, DashboardCalculos.ModulosPanel.Postura));
    }

    [Fact]
    public void TieneAlgunModulo_menu_vacio_o_nulo_es_fail_closed()
    {
        Assert.False(DashboardCalculos.TieneAlgunModulo(Array.Empty<string>(), DashboardCalculos.ModulosPanel.Postura));
        Assert.False(DashboardCalculos.TieneAlgunModulo(null, DashboardCalculos.ModulosPanel.Postura));
    }

    [Fact]
    public void TieneAlgunModulo_sin_modulos_pedidos_tambien_es_fail_closed()
    {
        // Un endpoint que se olvide de declarar su módulo debe quedarse sin datos, no ver toda la
        // empresa. Mismo criterio con el que los p_scope_* de vacunación se hicieron obligatorios.
        var menu = new[] { "/daily-log/seguimiento" };
        Assert.False(DashboardCalculos.TieneAlgunModulo(menu, Array.Empty<string>()));
        Assert.False(DashboardCalculos.TieneAlgunModulo(menu, null));
    }

    [Fact]
    public void TieneAlgunModulo_ignora_routes_basura_del_menu()
    {
        // Los nodos de agrupación (Configuración, Reportes) no tienen route: llegan como null.
        var menu = new string?[] { null, "", "   ", "/daily-log/aves-engorde" };
        Assert.True(DashboardCalculos.TieneAlgunModulo(menu, DashboardCalculos.ModulosPanel.Engorde));

        var soloBasura = new string?[] { null, "", "   " };
        Assert.False(DashboardCalculos.TieneAlgunModulo(soloBasura, DashboardCalculos.ModulosPanel.Engorde));
    }

    [Fact]
    public void TieneAlgunModulo_normaliza_las_dos_puntas()
    {
        var menu = new[] { "/Daily-Log/Seguimiento/" };
        Assert.True(DashboardCalculos.TieneAlgunModulo(menu, new[] { "daily-log/seguimiento" }));
    }

    [Theory]
    [InlineData("/vacunacion/cronograma")]
    [InlineData("/cuadres-offline")]
    [InlineData("/implementacion/mis-tareas")]
    public void TieneAlgunModulo_cumplimiento_lo_abre_cualquiera_de_sus_tres_modulos(string route)
        => Assert.True(DashboardCalculos.TieneAlgunModulo(
            new[] { route }, DashboardCalculos.ModulosPanel.Cumplimiento));

    // ─────────────────────────────────────────────────────────── TieneAlgunPermiso

    [Fact]
    public void TieneAlgunPermiso_sin_permisos_pedidos_pasa()
    {
        Assert.True(DashboardCalculos.TieneAlgunPermiso(Array.Empty<string>(), null));
        Assert.True(DashboardCalculos.TieneAlgunPermiso(null, Array.Empty<string>()));
    }

    [Fact]
    public void TieneAlgunPermiso_alcanza_con_uno_de_los_pedidos()
    {
        var propios = new[] { "seguimiento_produccion.desvalidar" };
        var pedidos = new[] { "seguimiento_produccion.validar", "seguimiento_produccion.desvalidar" };
        Assert.True(DashboardCalculos.TieneAlgunPermiso(propios, pedidos));
    }

    [Fact]
    public void TieneAlgunPermiso_no_distingue_mayusculas()
    {
        var propios = new[] { "SEGUIMIENTO_PRODUCCION.VALIDAR" };
        Assert.True(DashboardCalculos.TieneAlgunPermiso(propios, new[] { "seguimiento_produccion.validar" }));
    }

    [Fact]
    public void TieneAlgunPermiso_con_otro_permiso_distinto_dice_que_no()
    {
        var propios = new[] { "editar_registro" };
        Assert.False(DashboardCalculos.TieneAlgunPermiso(propios, new[] { "seguimiento_produccion.validar" }));
    }

    [Fact]
    public void TieneAlgunPermiso_sin_permisos_propios_y_con_pedidos_dice_que_no()
    {
        Assert.False(DashboardCalculos.TieneAlgunPermiso(null, new[] { "seguimiento_produccion.validar" }));
        Assert.False(DashboardCalculos.TieneAlgunPermiso(Array.Empty<string>(), new[] { "seguimiento_produccion.validar" }));
    }

    // ─────────────────────────────────────────────────────────── el catálogo de módulos

    [Fact]
    public void Los_modulos_de_cada_panel_estan_declarados_y_normalizados()
    {
        var todos = new[]
        {
            DashboardCalculos.ModulosPanel.Postura,
            DashboardCalculos.ModulosPanel.Engorde,
            DashboardCalculos.ModulosPanel.AlimentoInventario,
            DashboardCalculos.ModulosPanel.Cumplimiento,
        };

        foreach (var panel in todos)
        {
            Assert.NotEmpty(panel);
            foreach (var route in panel)
            {
                // Si una route del catálogo no está en forma canónica, el match sigue andando
                // (Cubre normaliza), pero el catálogo miente sobre lo que dice. Se exige la forma.
                Assert.Equal(route, DashboardCalculos.NormalizarRoute(route));
            }
        }
    }
}
