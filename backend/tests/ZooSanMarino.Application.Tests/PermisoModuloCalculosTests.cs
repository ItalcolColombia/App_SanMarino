using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.PermisoModuloCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas de los módulos de permisos (<c>permission_modules</c> → <c>company_permissions</c>).
/// <para>
/// Lo que se prueba: que prender un módulo prenda sus permisos, que apagarlo no se lleve puesto un
/// permiso compartido que otro módulo prendido sigue cubriendo (R-M3), que el ajuste fino sobreviva a
/// los cambios que no lo tocan, y que la siembra inicial no le regale a nadie un permiso (R-M5).
/// </para>
/// </summary>
public class PermisoModuloCalculosTests
{
    private const string Postura = "postura";
    private const string Engorde = "pollo_engorde";
    private const string Inventario = "inventario";

    private static readonly string[] Catalogo =
    {
        "carga_masiva_postura",
        "abrir_lote",
        "lote.corregir_aves",
        "editar_registro",
        "tickets.crear"
    };

    /// <summary>Clasificación del plan, recortada: <c>tickets.crear</c> queda sin clasificar a propósito.</summary>
    private static Dictionary<string, IReadOnlyCollection<string>> Clasificacion() => new()
    {
        ["carga_masiva_postura"] = new[] { Postura },
        ["abrir_lote"] = new[] { Engorde },
        ["lote.corregir_aves"] = new[] { Postura, Engorde },
        ["editar_registro"] = new[] { Engorde, Inventario }
    };

    private static EstadoModulosEmpresa Estado(params string[] modulos) => new(Clasificacion(), modulos);

    // ── Prender / apagar módulos ──────────────────────────────────────────────
    [Fact]
    public void PrenderModulo_PrendeTodosSusPermisos()
    {
        var r = ResolverHabilitados(Catalogo, Estado(Postura), Estado(Postura, Engorde),
            new[] { "carga_masiva_postura", "lote.corregir_aves" });

        Assert.Equal(
            new[] { "carga_masiva_postura", "abrir_lote", "lote.corregir_aves", "editar_registro" },
            r);
    }

    [Fact]
    public void ApagarModulo_ApagaLosQueSoloEseModuloCubria()
    {
        var r = ResolverHabilitados(Catalogo, Estado(Postura, Engorde), Estado(Postura),
            new[] { "carga_masiva_postura", "abrir_lote", "lote.corregir_aves", "editar_registro" });

        Assert.DoesNotContain("abrir_lote", r);
        Assert.DoesNotContain("editar_registro", r);
    }

    [Fact]
    public void ApagarModulo_CompartidoConOtroModuloPrendido_SigueHabilitado()
    {
        var r = ResolverHabilitados(Catalogo, Estado(Postura, Engorde), Estado(Postura),
            new[] { "lote.corregir_aves" });

        Assert.Contains("lote.corregir_aves", r);
    }

    [Fact]
    public void ApagarModulo_CompartidoApagadoAMano_SigueApagado()
    {
        // lote.corregir_aves estaba apagado por ajuste fino; apagar Engorde no lo "resucita" por Postura.
        var r = ResolverHabilitados(Catalogo, Estado(Postura, Engorde), Estado(Postura),
            new[] { "carga_masiva_postura" });

        Assert.DoesNotContain("lote.corregir_aves", r);
    }

    [Fact]
    public void ModuloSinCambios_ConservaElAjusteFino()
    {
        // Prender Inventario no debe re-prender abrir_lote, que se apagó a mano dentro de Engorde.
        var r = ResolverHabilitados(Catalogo, Estado(Engorde), Estado(Engorde, Inventario),
            new[] { "lote.corregir_aves" });

        Assert.DoesNotContain("abrir_lote", r);
        Assert.Contains("lote.corregir_aves", r);
    }

    [Fact]
    public void PrenderModulo_CompartidoApagadoAMano_SePrendePorElModuloNuevo()
    {
        // editar_registro estaba apagado dentro de Engorde; prender Inventario (que también lo cubre) lo prende.
        var r = ResolverHabilitados(Catalogo, Estado(Engorde), Estado(Engorde, Inventario),
            Array.Empty<string>());

        Assert.Contains("editar_registro", r);
    }

    [Fact]
    public void SinClasificar_NuncaLoTocanLosModulos()
    {
        var prendido = ResolverHabilitados(Catalogo, Estado(Postura), Estado(), new[] { "tickets.crear" });
        var apagado = ResolverHabilitados(Catalogo, Estado(), Estado(Postura, Engorde, Inventario), Array.Empty<string>());

        Assert.Equal(new[] { "tickets.crear" }, prendido);
        Assert.DoesNotContain("tickets.crear", apagado);
    }

    // ── Cambios de clasificación ──────────────────────────────────────────────
    [Fact]
    public void ClasificarPermisoEnModuloPrendido_LoPrende()
    {
        var antes = new EstadoModulosEmpresa(Clasificacion(), new[] { Postura });
        var clasifNueva = Clasificacion();
        clasifNueva["tickets.crear"] = new[] { Postura };
        var despues = new EstadoModulosEmpresa(clasifNueva, new[] { Postura });

        var r = ResolverHabilitados(Catalogo, antes, despues, new[] { "carga_masiva_postura" });

        Assert.Contains("tickets.crear", r);
    }

    [Fact]
    public void DesclasificarPermiso_ConservaSuEstado()
    {
        var antes = new EstadoModulosEmpresa(Clasificacion(), new[] { Engorde });
        var clasifNueva = Clasificacion();
        clasifNueva.Remove("abrir_lote");
        var despues = new EstadoModulosEmpresa(clasifNueva, new[] { Engorde });

        var r = ResolverHabilitados(Catalogo, antes, despues, new[] { "abrir_lote" });

        Assert.Contains("abrir_lote", r);
    }

    [Fact]
    public void ResolverHabilitados_KeysSinDistinguirMayusculas_YOrdenDelCatalogo()
    {
        var r = ResolverHabilitados(Catalogo, Estado(Postura), Estado(Postura),
            new[] { "LOTE.CORREGIR_AVES", "Carga_Masiva_Postura", "", null! });

        Assert.Equal(new[] { "carga_masiva_postura", "lote.corregir_aves" }, r);
    }

    // ── Gate del ajuste fino (R-M4) ───────────────────────────────────────────
    [Fact]
    public void Gate_PrenderPermisoFueraDeModulo_SeRechaza()
    {
        var r = ResolverNoPermitidos(
            new[] { "carga_masiva_postura", "abrir_lote" },
            new[] { "carga_masiva_postura" },
            Clasificacion(),
            new[] { Postura });

        Assert.Equal(new[] { "abrir_lote" }, r);
    }

    [Fact]
    public void Gate_CompartidoCubiertoPorUnModuloPrendido_SePermite()
    {
        var r = ResolverNoPermitidos(new[] { "lote.corregir_aves" }, Array.Empty<string>(),
            Clasificacion(), new[] { Postura });

        Assert.Empty(r);
    }

    [Fact]
    public void Gate_SinClasificar_SePermite()
    {
        var r = ResolverNoPermitidos(new[] { "tickets.crear" }, Array.Empty<string>(),
            Clasificacion(), Array.Empty<string>());

        Assert.Empty(r);
    }

    [Fact]
    public void Gate_ConservarLoQueYaEstaba_NuncaFalla()
    {
        // abrir_lote quedó prendido de antes aunque la empresa ya no tenga Engorde: guardar no lo rechaza.
        var r = ResolverNoPermitidos(new[] { "abrir_lote" }, new[] { "abrir_lote" },
            Clasificacion(), new[] { Postura });

        Assert.Empty(r);
    }

    [Fact]
    public void Gate_EmpresaSinConfiguracionDeModulos_NoRechazaNada()
    {
        var r = ResolverNoPermitidos(new[] { "abrir_lote" }, Array.Empty<string>(), Clasificacion(), null);

        Assert.Empty(r);
    }

    // ── Siembra inicial (R-M5) ────────────────────────────────────────────────
    [Fact]
    public void Siembra_FueraDeModulosPrendidos_SeApaga()
    {
        var r = ResolverSiembra(Catalogo, Estado(Postura),
            new Dictionary<string, bool> { ["abrir_lote"] = true },
            new[] { "abrir_lote" });

        Assert.False(r["abrir_lote"]);
        Assert.False(r["editar_registro"]);
    }

    [Fact]
    public void Siembra_CubiertoConFila_RespetaLaFila()
    {
        var r = ResolverSiembra(Catalogo, Estado(Engorde),
            new Dictionary<string, bool> { ["abrir_lote"] = false, ["editar_registro"] = true },
            Array.Empty<string>());

        Assert.False(r["abrir_lote"]);
        Assert.True(r["editar_registro"]);
    }

    [Fact]
    public void Siembra_CubiertoSinFilaNiRoles_SePrende()
    {
        var r = ResolverSiembra(Catalogo, Estado(Postura), new Dictionary<string, bool>(), Array.Empty<string>());

        Assert.True(r["carga_masiva_postura"]);
    }

    [Fact]
    public void Siembra_CubiertoSinFilaPeroAsignadoARoles_QuedaApagado_NadieGana()
    {
        // El caso medido: seguimiento_*.validar asignado a roles de postura sin fila en company_permissions.
        var r = ResolverSiembra(Catalogo, Estado(Postura), new Dictionary<string, bool>(),
            new[] { "CARGA_MASIVA_POSTURA" });

        Assert.False(r["carga_masiva_postura"]);
    }

    [Fact]
    public void Siembra_SinClasificar_NoApareceEnElResultado()
    {
        var r = ResolverSiembra(Catalogo, Estado(Postura, Engorde, Inventario),
            new Dictionary<string, bool> { ["tickets.crear"] = true }, Array.Empty<string>());

        Assert.False(r.ContainsKey("tickets.crear"));
    }

    [Fact]
    public void EntradasNulas_NoRevientan()
    {
        Assert.Empty(ResolverHabilitados(null!, null!, null!, null!));
        Assert.Empty(ResolverNoPermitidos(null!, null!, null!, Array.Empty<string>()));
        Assert.Empty(ResolverSiembra(null!, null!, null!, null!));
    }
}
