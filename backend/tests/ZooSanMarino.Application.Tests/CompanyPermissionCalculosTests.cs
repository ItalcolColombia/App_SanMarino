using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.CompanyPermissionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas del eje permiso↔empresa (<c>company_permissions</c>).
/// <para>
/// Lo que se prueba acá es que apagar un permiso en una empresa lo apague DE VERDAD: ni se ofrece al
/// armar un rol (R1/R2) ni sobrevive al login (R3). Y que apagarlo no borre nada: lo ya asignado se
/// reporta como huérfano en vez de desaparecer sin dejar rastro (R5).
/// </para>
/// </summary>
public class CompanyPermissionCalculosTests
{
    private const int Ecuador = 3;
    private const int Panama = 5;
    private const int SinConfigurar = 99;

    private static readonly string[] Catalogo =
    {
        "carga_masiva_postura",
        "editar_registro",
        "lote_base_pollo_engorde.ver",
        "sincronizacion_panama.ejecutar",
        "tickets.crear"
    };

    private static Dictionary<int, IReadOnlyCollection<string>> Habilitadas() => new()
    {
        [Ecuador] = new[] { "editar_registro", "lote_base_pollo_engorde.ver", "tickets.crear" },
        [Panama] = new[] { "editar_registro", "sincronizacion_panama.ejecutar", "tickets.crear" }
    };

    // ── T1: empresa configurada ⇒ solo sus permisos ───────────────────────────
    [Fact]
    public void ResolverAsignables_SoloLosHabilitadosDeLaEmpresa()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { Ecuador }, Array.Empty<string>());

        Assert.Equal(
            new[] { "editar_registro", "lote_base_pollo_engorde.ver", "tickets.crear" },
            r.Asignables);
        Assert.Empty(r.EmpresasSinConfigurar);
        // Los de Panamá y los de Sanmarino Colombia no se ofrecen en Ecuador.
        Assert.DoesNotContain("sincronizacion_panama.ejecutar", r.Asignables);
        Assert.DoesNotContain("carga_masiva_postura", r.Asignables);
    }

    // ── T2: empresa sin configurar ⇒ fail-closed + bandera ────────────────────
    [Fact]
    public void ResolverAsignables_EmpresaSinConfigurar_FailClosedYAvisa()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { SinConfigurar }, Array.Empty<string>());

        Assert.Empty(r.Asignables);
        Assert.Equal(new[] { SinConfigurar }, r.EmpresasSinConfigurar);
    }

    [Fact]
    public void ResolverAsignables_SinEmpresasSeleccionadas_NoOfreceNada()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), Array.Empty<int>(), Array.Empty<string>());

        Assert.Empty(r.Asignables);
        Assert.Empty(r.EmpresasSinConfigurar);
    }

    // ── T3: rol multi-empresa ⇒ intersección ──────────────────────────────────
    [Fact]
    public void ResolverAsignables_RolCompartido_Intersecta()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { Ecuador, Panama }, Array.Empty<string>());

        Assert.Equal(new[] { "editar_registro", "tickets.crear" }, r.Asignables);
    }

    [Fact]
    public void ResolverAsignables_UnaEmpresaSinConfigurar_VaciaLaInterseccion()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { Ecuador, SinConfigurar }, Array.Empty<string>());

        Assert.Empty(r.Asignables);
        Assert.Equal(new[] { SinConfigurar }, r.EmpresasSinConfigurar);
    }

    // ── T6: comparación case-insensitive ──────────────────────────────────────
    [Fact]
    public void ResolverAsignables_KeysCaseInsensitive()
    {
        var habilitadas = new Dictionary<int, IReadOnlyCollection<string>>
        {
            [Ecuador] = new[] { "EDITAR_REGISTRO", "Tickets.Crear" }
        };

        var r = ResolverAsignables(Catalogo, habilitadas, new[] { Ecuador }, Array.Empty<string>());

        // Matchea sin distinguir mayúsculas y devuelve la capitalización del CATÁLOGO.
        Assert.Equal(new[] { "editar_registro", "tickets.crear" }, r.Asignables);
    }

    // ── T7: lo ya asignado que quedó fuera se reporta, no se pierde ───────────
    [Fact]
    public void ResolverAsignables_AsignadoNoHabilitado_SaleComoHuerfano()
    {
        var yaAsignadas = new[] { "editar_registro", "sincronizacion_panama.ejecutar" };

        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { Ecuador }, yaAsignadas);

        Assert.Contains("editar_registro", r.Asignables);
        Assert.Equal(new[] { "sincronizacion_panama.ejecutar" }, r.Huerfanas);
    }

    [Fact]
    public void ResolverAsignables_SinHuerfanosCuandoTodoEstaHabilitado()
    {
        var r = ResolverAsignables(Catalogo, Habilitadas(), new[] { Ecuador }, new[] { "tickets.crear" });

        Assert.Empty(r.Huerfanas);
    }

    // ── T8: bordes ────────────────────────────────────────────────────────────
    [Fact]
    public void ResolverAsignables_CatalogoVacio_NoExplota()
    {
        var r = ResolverAsignables(Array.Empty<string>(), Habilitadas(), new[] { Ecuador }, Array.Empty<string>());

        Assert.Empty(r.Asignables);
        Assert.Empty(r.Huerfanas);
    }

    [Fact]
    public void ResolverAsignables_IgnoraKeysVaciasYDuplicadas()
    {
        var catalogo = new[] { "editar_registro", "  ", null!, "editar_registro", "tickets.crear" };

        var r = ResolverAsignables(catalogo, Habilitadas(), new[] { Ecuador }, Array.Empty<string>());

        Assert.Equal(new[] { "editar_registro", "tickets.crear" }, r.Asignables);
    }

    // ── T4/T5: runtime — el par (rol, empresa) es lo que decide ───────────────
    [Fact]
    public void ResolverEfectivos_PermisoNoHabilitadoEnSuEmpresa_NoViaja()
    {
        // Rol de Ecuador que tiene asignado un permiso que Ecuador NO habilita.
        var pares = new (int, IReadOnlyCollection<string>)[]
        {
            (Ecuador, new[] { "editar_registro", "sincronizacion_panama.ejecutar" })
        };

        var efectivos = ResolverEfectivos(pares, Habilitadas());

        Assert.Equal(new[] { "editar_registro" }, efectivos);
    }

    [Fact]
    public void ResolverEfectivos_MismoPermisoPorDosEmpresas_SobreviveUnaSolaVez()
    {
        // Ecuador no habilita sincronizacion_panama; Panamá sí. El usuario tiene un rol en cada una.
        var pares = new (int, IReadOnlyCollection<string>)[]
        {
            (Ecuador, new[] { "sincronizacion_panama.ejecutar", "tickets.crear" }),
            (Panama,  new[] { "sincronizacion_panama.ejecutar", "tickets.crear" })
        };

        var efectivos = ResolverEfectivos(pares, Habilitadas());

        Assert.Equal(new[] { "tickets.crear", "sincronizacion_panama.ejecutar" }, efectivos);
        Assert.Single(efectivos, k => k == "sincronizacion_panama.ejecutar");
    }

    [Fact]
    public void ResolverEfectivos_EmpresaSinConfigurar_NoAportaNada()
    {
        var pares = new (int, IReadOnlyCollection<string>)[]
        {
            (SinConfigurar, new[] { "editar_registro", "tickets.crear" })
        };

        Assert.Empty(ResolverEfectivos(pares, Habilitadas()));
    }

    [Fact]
    public void ResolverEfectivos_ConfiguracionEspejoDelUso_NoCambiaNada()
    {
        // Invariante del seed: si la empresa tiene habilitado exactamente lo que sus roles usan, los
        // permisos efectivos son idénticos a los de antes del gate.
        var delRol = new[] { "editar_registro", "tickets.crear", "lote_base_pollo_engorde.ver" };
        var habilitadas = new Dictionary<int, IReadOnlyCollection<string>>
        {
            [Ecuador] = delRol
        };

        var efectivos = ResolverEfectivos(
            new (int, IReadOnlyCollection<string>)[] { (Ecuador, delRol) },
            habilitadas);

        Assert.Equal(delRol, efectivos);
    }

    [Fact]
    public void ResolverEfectivos_SinPares_DevuelveVacio()
    {
        Assert.Empty(ResolverEfectivos(
            Array.Empty<(int, IReadOnlyCollection<string>)>(),
            Habilitadas()));
    }
}
