using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using static ZooSanMarino.Application.Calculos.MenuVisibilidadCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El menú efectivo del usuario tiene que respetar lo que la empresa tiene habilitado
/// (<c>company_menus</c>).
///
/// <para>
/// El defecto que motiva estas pruebas: hasta el 26-ago-2026 el sidebar se armaba desde
/// <c>role_menus</c> y <b>nunca</b> miraba <c>company_menus</c>, así que quitarle un módulo a una
/// empresa no cambiaba nada. Medido en la copia de producción, a ItalcolPanamá se le colaban 7 menús
/// (todo ItalJira, Guía Genética, Bandeja de gestión) que la empresa no tiene asignados.
/// </para>
///
/// <para>
/// Estas pruebas son el contrato que <c>fn_menu_usuario</c> tiene que cumplir: en runtime el menú lo
/// arma la función SQL, acá vive la misma regla en C# para poder fijarla.
/// </para>
/// </summary>
public class MenuVisibilidadCalculosTests
{
    private static readonly string[] SinPermisos = Array.Empty<string>();

    private static MenuPlano M(int id, int orden, int? parent = null, string[]? keys = null) =>
        new(id, $"Menu {id}", null, $"/r{id}", orden, parent, keys ?? SinPermisos);

    /// <summary>
    /// Catálogo de prueba, con la misma forma que el real:
    /// <code>
    ///   1 Configuración (raíz)     10 Reportes (raíz)     20 ItalJira (raíz)
    ///   ├─ 2 Listas maestras       └─ 11 Costos           ├─ 21 Backlog   (exige tickets.admin)
    ///   └─ 3 Guía Genética                                └─ 22 Tablero   (exige tickets.admin)
    /// </code>
    /// </summary>
    private static List<MenuPlano> Catalogo() =>
    [
        M(1, 0),
        M(2, 0, parent: 1),
        M(3, 1, parent: 1),
        M(10, 1),
        M(11, 0, parent: 10),
        M(20, 2),
        M(21, 0, parent: 20, keys: ["tickets.admin"]),
        M(22, 1, parent: 20, keys: ["tickets.admin"])
    ];

    private static int[] Ids(IEnumerable<MenuPlano> planos) => planos.Select(p => p.Id).OrderBy(x => x).ToArray();

    // ── T1: sin configuración de empresa, el resultado es el de siempre ────────
    // D2. Es el caso de una empresa recién creada: CompanyService.CreateAsync NO siembra
    // company_menus, así que fail-closed la dejaría con el menú vacío y sin forma de arreglarlo
    // desde la app.
    [Fact]
    public void EmpresaSinConfigurar_NoFiltra_ResultadoIdenticoAlPrevio()
    {
        var asignados = new[] { 2, 3, 11, 21 };
        var keys = new[] { "tickets.admin" };

        var conGate = ResolverVisibles(Catalogo(), asignados, keys, habilitadosEmpresa: null);

        Assert.Equal(new[] { 1, 2, 3, 10, 11, 20, 21 }, Ids(conGate));
    }

    [Fact]
    public void EmpresaFiltra_SoloSiHayEmpresaYFilas()
    {
        Assert.False(MenuVisibilidadCalculos.EmpresaFiltra(companyId: null, filasEnCompanyMenus: 25));
        Assert.False(MenuVisibilidadCalculos.EmpresaFiltra(companyId: 5, filasEnCompanyMenus: 0));
        Assert.True(MenuVisibilidadCalculos.EmpresaFiltra(companyId: 5, filasEnCompanyMenus: 25));
    }

    // ── D5: el super admin no pasa por el gate de empresa ─────────────────────
    // Sin la marca el resultado tiene que ser idéntico al de siempre; el default `false` del
    // parámetro es lo que garantiza que los tres asserts de arriba sigan midiendo lo mismo.

    [Fact]
    public void SuperAdmin_NoLoFiltraLaEmpresa()
    {
        Assert.False(MenuVisibilidadCalculos.EmpresaFiltra(
            companyId: 5, filasEnCompanyMenus: 25, esSuperAdmin: true));

        // Y el no-super-admin en la misma empresa sí: la excepción es de la persona, no del dato.
        Assert.True(MenuVisibilidadCalculos.EmpresaFiltra(
            companyId: 5, filasEnCompanyMenus: 25, esSuperAdmin: false));
    }

    /// <summary>
    /// El caso concreto que motivó D5: el ítem «Empresas» está en los <c>role_menus</c> del super
    /// admin, pero la empresa activa ya no lo habilita. Con el gate aplicado se quedaría sin la
    /// única pantalla que sirve para volver a habilitarlo.
    /// </summary>
    [Fact]
    public void SuperAdmin_VeLoAsignadoAunqueLaEmpresaLoHayaApagado()
    {
        // El gate resuelto en false (D5) llega a ResolverVisibles como "sin habilitados" = null.
        var filtra = MenuVisibilidadCalculos.EmpresaFiltra(
            companyId: 5, filasEnCompanyMenus: 25, esSuperAdmin: true);

        var visibles = ResolverVisibles(
            Catalogo(),
            asignados: [2, 3, 11, 21, 22],
            keysUsuario: ["tickets.admin"],
            habilitadosEmpresa: filtra ? [1, 2, 3, 10, 11] : null);

        // Ve también ItalJira (20/21/22), que la empresa no habilita.
        Assert.Equal(new[] { 1, 2, 3, 10, 11, 20, 21, 22 }, Ids(visibles));
    }

    /// <summary>
    /// D5 recorta el gate de EMPRESA y nada más: los <c>role_menus</c>, <c>is_active</c> y
    /// <c>menu_permissions</c> le siguen aplicando al super admin como a cualquiera.
    /// </summary>
    [Fact]
    public void SuperAdmin_SigueLimitadoPorSusRolesYSusPermisos()
    {
        var visibles = ResolverVisibles(
            Catalogo(),
            asignados: [2, 3],            // no tiene ItalJira asignado en role_menus
            keysUsuario: [],              // ni la key que ItalJira exige
            habilitadosEmpresa: null);    // gate de empresa levantado por D5

        Assert.DoesNotContain(visibles, m => m.Id is 20 or 21 or 22);
    }

    // ── T2/T3/T4: el gate propiamente dicho ───────────────────────────────────
    // T2 es el caso reportado: el rol de Panamá tiene ItalJira en role_menus y la empresa no lo
    // tiene en company_menus.
    [Fact]
    public void AsignadoAlRolPeroAusenteDeLaEmpresa_NoSeVe()
    {
        var visibles = ResolverVisibles(
            Catalogo(),
            asignados: [2, 3, 11, 21, 22],
            keysUsuario: ["tickets.admin"],
            habilitadosEmpresa: [1, 2, 3, 10, 11]);   // ItalJira (20/21/22) no está

        Assert.Equal(new[] { 1, 2, 3, 10, 11 }, Ids(visibles));
        Assert.DoesNotContain(visibles, m => m.Id is 20 or 21 or 22);
    }

    [Fact]
    public void HabilitadoEnFalse_OcultaIgualQueLaFilaAusente()
    {
        // D1: la pantalla de administración puede dejar la fila y bajar is_enabled. El llamador ya
        // entrega solo los habilitados, así que "deshabilitado" llega como ausencia del id.
        var conFila = ResolverVisibles(Catalogo(), [21], ["tickets.admin"], habilitadosEmpresa: [20, 21]);
        var sinFila = ResolverVisibles(Catalogo(), [21], ["tickets.admin"], habilitadosEmpresa: [20]);

        Assert.Equal(new[] { 20, 21 }, Ids(conFila));
        Assert.Empty(sinFila);
    }

    [Fact]
    public void AsignadoYHabilitado_SeVe()
    {
        var visibles = ResolverVisibles(Catalogo(), [21, 22], ["tickets.admin"], habilitadosEmpresa: [20, 21, 22]);

        Assert.Equal(new[] { 20, 21, 22 }, Ids(visibles));
    }

    // ── T5: D3 — el ancestro entra aunque la empresa no lo tenga ───────────────
    [Fact]
    public void AncestroNoHabilitadoPeroHijoSi_SeMuestranLosDos()
    {
        // Si el gate se aplicara al conjunto final en vez de a la semilla, el padre 20 caería y el
        // hijo 21 quedaría huérfano ⇒ el submenú entero desaparecería.
        var visibles = ResolverVisibles(Catalogo(), [21], ["tickets.admin"], habilitadosEmpresa: [21]);

        Assert.Equal(new[] { 20, 21 }, Ids(visibles));
    }

    // ── T6: padre habilitado sin hijos visibles ───────────────────────────────
    [Fact]
    public void PadreSinHijosVisibles_QuedaComoGrupoVacio()
    {
        // Es lo que hace hoy: en producción, ItalcolEcuador tiene el grupo «Movimientos» asignado a
        // un rol sin ninguno de sus hijos. No se inventa un comportamiento nuevo.
        var visibles = ResolverVisibles(Catalogo(), [20], ["tickets.admin"], habilitadosEmpresa: [20]);
        var arbol = ConstruirArbol(visibles);

        Assert.Equal(new[] { 20 }, Ids(visibles));
        Assert.Single(arbol);
        Assert.Empty(arbol[0].Children);
    }

    // ── T7: el gate de permisos no se afloja ──────────────────────────────────
    [Fact]
    public void SinElPermisoQueExige_NoSeVeAunqueLaEmpresaLoHabilite()
    {
        var visibles = ResolverVisibles(Catalogo(), [21, 22], keysUsuario: [], habilitadosEmpresa: [20, 21, 22]);

        // 21 y 22 exigen tickets.admin; el 20 entró como ancestro y también se filtra por permisos
        // en la rama asignada, pero no exige ninguno, así que sobrevive vacío.
        Assert.Equal(new[] { 20 }, Ids(visibles));
    }

    [Fact]
    public void KeysComparanSinDistinguirMayusculas()
    {
        var visibles = ResolverVisibles(Catalogo(), [21], ["TICKETS.ADMIN"], habilitadosEmpresa: [20, 21]);

        Assert.Equal(new[] { 20, 21 }, Ids(visibles));
    }

    // ── T8: rama fallback (usuario sin role_menus) ────────────────────────────
    [Fact]
    public void SinRoleMenus_ElFiltroPorEmpresaTambienAplica()
    {
        var visibles = ResolverVisibles(
            Catalogo(),
            asignados: [],
            keysUsuario: ["tickets.admin"],
            habilitadosEmpresa: [1, 2, 3, 10, 11]);

        Assert.Equal(new[] { 1, 2, 3, 10, 11 }, Ids(visibles));
    }

    [Fact]
    public void SinRoleMenusYSinEmpresa_ElCatalogoPermitidoEntero()
    {
        var visibles = ResolverVisibles(Catalogo(), asignados: [], keysUsuario: [], habilitadosEmpresa: null);

        // 21 y 22 exigen un permiso que no tiene; el resto entra.
        Assert.Equal(new[] { 1, 2, 3, 10, 11, 20 }, Ids(visibles));
    }

    // ── T9: cadena rota por un ancestro inactivo ──────────────────────────────
    [Fact]
    public void PadreInactivo_ElHijoSeDescarta()
    {
        // El catálogo sólo trae los is_active = true, así que un padre inactivo simplemente no está.
        var catalogoSinPadre = Catalogo().Where(m => m.Id != 20).ToList();

        var visibles = ResolverVisibles(catalogoSinPadre, [21], ["tickets.admin"], habilitadosEmpresa: null);

        Assert.Empty(visibles);
    }

    // ── T10: empate de order ⇒ desempata el id ────────────────────────────────
    [Fact]
    public void EmpateDeOrden_DesempatanIds()
    {
        // En producción hay cuatro empates reales; el más visible es Carga Masiva (66) e ItalJira
        // (75), los dos con order 901. Antes el orden lo decidía el motor.
        var catalogo = new List<MenuPlano> { M(75, 901), M(66, 901), M(61, 902), M(79, 902) };

        var arbol = ConstruirArbol(ResolverVisibles(catalogo, [66, 75, 61, 79], [], habilitadosEmpresa: null));

        Assert.Equal(new[] { 66, 75, 61, 79 }, arbol.Select(n => n.Id).ToArray());
    }

    // ── T11: sin empresa (endpoint de administración por usuario) ─────────────
    [Fact]
    public void SinEmpresa_NoSeFiltraPorEmpresa()
    {
        var visibles = ResolverVisibles(Catalogo(), [21, 22], ["tickets.admin"], habilitadosEmpresa: null);

        Assert.Equal(new[] { 20, 21, 22 }, Ids(visibles));
    }

    // ── Árbol: forma y orden ──────────────────────────────────────────────────
    [Fact]
    public void ConstruirArbol_AnidaYOrdenaPorOrden()
    {
        var visibles = ResolverVisibles(
            Catalogo(),
            asignados: [2, 3, 11],
            keysUsuario: [],
            habilitadosEmpresa: [1, 2, 3, 10, 11]);

        var arbol = ConstruirArbol(visibles);

        Assert.Equal(new[] { 1, 10 }, arbol.Select(n => n.Id).ToArray());
        Assert.Equal(new[] { 2, 3 }, arbol[0].Children.Select(n => n.Id).ToArray());
        Assert.Equal(new[] { 11 }, arbol[1].Children.Select(n => n.Id).ToArray());
    }

    // ── Contrato del JSON que devuelve fn_menu_usuario ────────────────────────
    // El backend ya no arma el árbol: recibe este jsonb y lo deserializa. Si la función cambiara el
    // nombre o la forma de una clave, el menú llegaría vacío o sin rutas y NADA lo avisaría —
    // `Deserialize` no falla ante propiedades que no matchean, deja los valores en default.
    // La muestra es salida REAL de la función para el administrador de ItalcolPanamá.
    private const string JsonRealDeLaFuncion = """
    [
        { "id": 4, "icon": "building", "label": "Gestion de Granjas", "order": 1,
          "route": "/config/farm-management", "children": [] },
        { "id": 40, "icon": "layer-group", "label": "Lote", "order": 2, "route": null, "children": [
            { "id": 41, "icon": "layer-group", "label": "Lote Engorde", "order": 2,
              "route": "/config/lote-engorde", "children": [] },
            { "id": 42, "icon": "layer-group", "label": "Lote Reproductora Engorde", "order": 3,
              "route": "/config/lote-reproductora-ave-engorde", "children": [] } ] },
        { "id": 66, "icon": "file-import", "label": "Carga Masiva", "order": 901, "route": null,
          "children": [
            { "id": 65, "icon": "cloud-download-alt", "label": "Integración Panamá", "order": 2,
              "route": "/migraciones/sincronizacion-panama", "children": [] } ] }
    ]
    """;

    [Fact]
    public void JsonDeLaFuncion_DeserializaAlContratoDelFront()
    {
        var arbol = System.Text.Json.JsonSerializer.Deserialize<MenuItemDto[]>(
            JsonRealDeLaFuncion, MenuVisibilidadCalculos.OpcionesJson);

        Assert.NotNull(arbol);
        Assert.Equal(3, arbol!.Length);

        var granjas = arbol[0];
        Assert.Equal(4, granjas.Id);
        Assert.Equal("Gestion de Granjas", granjas.Label);
        Assert.Equal("building", granjas.Icon);
        Assert.Equal("/config/farm-management", granjas.Route);
        Assert.Equal(1, granjas.Order);
        Assert.Empty(granjas.Children);

        // Un grupo sin ruta llega con Route en null, no en string vacío.
        var lote = arbol[1];
        Assert.Null(lote.Route);
        Assert.Equal(new[] { 41, 42 }, lote.Children.Select(c => c.Id).ToArray());
        Assert.Equal("/config/lote-engorde", lote.Children[0].Route);

        // Y el acento sobrevive el viaje jsonb → texto → C#.
        Assert.Equal("Integración Panamá", arbol[2].Children.Single().Label);
    }

    [Fact]
    public void JsonVacioDeLaFuncion_DeserializaAArregloVacio()
    {
        var arbol = System.Text.Json.JsonSerializer.Deserialize<MenuItemDto[]>(
            "[]", MenuVisibilidadCalculos.OpcionesJson);

        Assert.NotNull(arbol);
        Assert.Empty(arbol!);
    }

    [Fact]
    public void CatalogoVacio_DevuelveVacio()
    {
        Assert.Empty(ResolverVisibles([], [1, 2], ["x"], habilitadosEmpresa: null));
        Assert.Empty(ConstruirArbol([]));
    }
}
