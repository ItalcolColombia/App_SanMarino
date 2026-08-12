namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Qué permisos puede llevar un rol, dada la configuración por empresa.
/// </summary>
/// <param name="Asignables">
/// Keys que la UI puede ofrecer (ya intersectadas entre todas las empresas del rol).
/// </param>
/// <param name="Huerfanas">
/// Keys que el rol YA tiene asignadas pero que alguna de sus empresas no habilita. No se pierden en
/// silencio: la UI las muestra para que el admin decida quitarlas (regla R5, no destructiva).
/// </param>
/// <param name="EmpresasSinConfigurar">
/// Empresas del rol que no tienen ninguna fila en <c>company_permissions</c>. Con fail-closed no
/// aportan nada, así que la UI tiene que avisar en vez de mostrar una lista vacía sin explicación.
/// </param>
public sealed record PermisosAsignablesResultado(
    IReadOnlyList<string> Asignables,
    IReadOnlyList<string> Huerfanas,
    IReadOnlyList<int> EmpresasSinConfigurar
);

/// <summary>
/// Reglas del eje permiso↔empresa. Lógica PURA: sin EF, sin estado, sin servicios — los services
/// resuelven los datos y delegan acá.
///
/// <para>Las reglas (ver <c>fase_de_desarrollo/permisos_por_empresa_plan.md</c>):</para>
/// <list type="bullet">
///   <item><b>R1 fail-closed</b> — un permiso es asignable solo si la empresa lo habilita.</item>
///   <item><b>R2 intersección</b> — rol compartido entre empresas ⇒ solo lo que TODAS habilitan.</item>
///   <item><b>R3 runtime por par (rol, empresa)</b> — un permiso sobrevive al login solo si viene de
///         un rol cuya empresa lo tiene prendido.</item>
///   <item><b>R5 no destructivo</b> — lo ya asignado que quedó fuera se reporta, no se borra.</item>
///   <item><b>R6 case-insensitive</b> — el front baja las keys a minúscula.</item>
/// </list>
/// </summary>
public static class CompanyPermissionCalculos
{
    /// <summary>Las keys de permiso se comparan sin distinguir mayúsculas (R6).</summary>
    public static readonly StringComparer ComparadorKeys = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Permisos que la UI puede ofrecer al editar un rol, más lo que quedó huérfano (R1, R2, R5, R6).
    /// </summary>
    /// <param name="catalogo">Catálogo global de keys (fuente de la capitalización devuelta).</param>
    /// <param name="habilitadasPorEmpresa">
    /// Keys habilitadas por empresa. Una empresa AUSENTE del diccionario está sin configurar y, por
    /// fail-closed, no aporta ninguna key.
    /// </param>
    /// <param name="empresasDelRol">Empresas a las que está vinculado el rol.</param>
    /// <param name="yaAsignadas">Keys que el rol ya tiene en <c>role_permissions</c>.</param>
    public static PermisosAsignablesResultado ResolverAsignables(
        IEnumerable<string> catalogo,
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> habilitadasPorEmpresa,
        IEnumerable<int> empresasDelRol,
        IEnumerable<string> yaAsignadas)
    {
        var catalogoLimpio = NormalizarCatalogo(catalogo);
        var empresas = (empresasDelRol ?? Array.Empty<int>()).Distinct().ToList();

        var sinConfigurar = empresas
            .Where(id => !habilitadasPorEmpresa.ContainsKey(id))
            .ToList();

        // Sin empresas seleccionadas no hay contra qué contrastar: fail-closed, no se ofrece nada.
        // R2: intersección — solo lo que TODAS las empresas del rol habilitan. Una empresa sin
        // configurar aporta el conjunto vacío, así que la intersección se vacía (R1).
        var permitidas = new HashSet<string>(ComparadorKeys);
        var primera = true;
        foreach (var companyId in empresas)
        {
            var deEmpresa = habilitadasPorEmpresa.TryGetValue(companyId, out var keys)
                ? new HashSet<string>(keys ?? Array.Empty<string>(), ComparadorKeys)
                : new HashSet<string>(ComparadorKeys);

            if (primera) { permitidas = deEmpresa; primera = false; }
            else permitidas.IntersectWith(deEmpresa);

            if (permitidas.Count == 0) break;
        }

        // Se devuelve la capitalización del catálogo, no la que trajo la configuración.
        var asignables = catalogoLimpio
            .Where(k => permitidas.Contains(k))
            .ToList();

        var asignablesSet = new HashSet<string>(asignables, ComparadorKeys);
        var huerfanas = NormalizarCatalogo(yaAsignadas)
            .Where(k => !asignablesSet.Contains(k))
            .ToList();

        return new PermisosAsignablesResultado(asignables, huerfanas, sinConfigurar);
    }

    /// <summary>
    /// Keys que hay que RECHAZAR al escribir los permisos de un rol (gate de escritura, R1/R2).
    ///
    /// <para>
    /// Solo se juzga lo que se AGREGA: <paramref name="keysSolicitadas"/> menos
    /// <paramref name="yaAsignadas"/>. Conservar lo que el rol ya tenía nunca falla — si no, apagar un
    /// permiso en una empresa volvería INEDITABLE a todo rol que lo tuviera, porque el formulario
    /// manda siempre la lista completa y el propio guardado que viene a limpiarlo sería rechazado.
    /// Quitar tampoco se valida (ver <c>Roles_RemovePermissionsAsync</c>): limpiar huérfanos tiene que
    /// funcionar siempre.
    /// </para>
    /// </summary>
    /// <returns>Las keys agregadas que ninguna/alguna empresa del rol habilita; vacío = todo OK.</returns>
    public static IReadOnlyList<string> ResolverNoPermitidas(
        IEnumerable<string> keysSolicitadas,
        IEnumerable<string> yaAsignadas,
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> habilitadasPorEmpresa,
        IEnumerable<int> empresasDelRol)
    {
        var solicitadas = NormalizarCatalogo(keysSolicitadas);
        if (solicitadas.Count == 0) return Array.Empty<string>();

        var conservadas = new HashSet<string>(NormalizarCatalogo(yaAsignadas), ComparadorKeys);
        var agregadas = solicitadas.Where(k => !conservadas.Contains(k)).ToList();
        if (agregadas.Count == 0) return Array.Empty<string>();

        // Se contrasta contra el catálogo de lo agregado: lo que sobreviva es lo permitido.
        var permitidas = new HashSet<string>(
            ResolverAsignables(agregadas, habilitadasPorEmpresa, empresasDelRol, Array.Empty<string>()).Asignables,
            ComparadorKeys);

        return agregadas.Where(k => !permitidas.Contains(k)).ToList();
    }

    /// <summary>
    /// Permisos efectivos de un usuario en el login (R3): unión, sobre cada par (rol, empresa), de
    /// los permisos del rol intersectados con los que esa empresa habilita.
    /// <para>
    /// No depende de "empresa activa": el mismo permiso puede llegar por un rol de una empresa que lo
    /// habilita y por otro de una que no — sobrevive por el primero, una sola vez.
    /// </para>
    /// </summary>
    /// <param name="permisosPorRolYEmpresa">
    /// Un elemento por par (rol, empresa) del usuario, con las keys que ese rol otorga.
    /// </param>
    /// <param name="habilitadasPorEmpresa">Keys habilitadas por empresa (ausente = sin configurar).</param>
    public static IReadOnlyList<string> ResolverEfectivos(
        IEnumerable<(int CompanyId, IReadOnlyCollection<string> Keys)> permisosPorRolYEmpresa,
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> habilitadasPorEmpresa)
    {
        var efectivas = new List<string>();
        var vistas = new HashSet<string>(ComparadorKeys);

        foreach (var (companyId, keys) in permisosPorRolYEmpresa ?? Array.Empty<(int, IReadOnlyCollection<string>)>())
        {
            if (keys is null || keys.Count == 0) continue;

            // R1: empresa sin configurar no habilita nada.
            if (!habilitadasPorEmpresa.TryGetValue(companyId, out var habilitadas) || habilitadas is null)
                continue;

            var permitidas = new HashSet<string>(habilitadas, ComparadorKeys);

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var limpia = key.Trim();
                if (!permitidas.Contains(limpia)) continue;
                if (vistas.Add(limpia)) efectivas.Add(limpia);
            }
        }

        return efectivas;
    }

    /// <summary>Quita nulos/vacíos, recorta y deduplica conservando el orden de entrada.</summary>
    private static List<string> NormalizarCatalogo(IEnumerable<string>? keys)
    {
        var vistas = new HashSet<string>(ComparadorKeys);
        var salida = new List<string>();
        foreach (var key in keys ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var limpia = key.Trim();
            if (vistas.Add(limpia)) salida.Add(limpia);
        }
        return salida;
    }
}
