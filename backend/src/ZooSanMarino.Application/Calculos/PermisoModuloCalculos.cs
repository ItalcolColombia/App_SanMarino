namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// El eje módulo↔permiso visto desde una empresa en un instante.
/// </summary>
/// <param name="ModulosPorPermiso">
/// Clasificación M:N: key de permiso → keys de los módulos que lo contienen. Un permiso ausente (o con
/// colección vacía) está SIN CLASIFICAR.
/// </param>
/// <param name="ModulosDeEmpresa">Keys de los módulos que la empresa tiene prendidos.</param>
public sealed record EstadoModulosEmpresa(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> ModulosPorPermiso,
    IReadOnlyCollection<string> ModulosDeEmpresa
);

/// <summary>
/// Reglas de los módulos de permisos. Lógica PURA: sin EF, sin estado — los services resuelven datos y
/// delegan acá, y la migración de siembra es el espejo SQL de <see cref="ResolverSiembra"/>.
///
/// <para>Plan: <c>fase_de_desarrollo/modulos_permisos_por_empresa_plan.md</c>.</para>
/// <list type="bullet">
///   <item><b>R-M2</b> — apagar un módulo apaga permisos de la EMPRESA; nunca toca <c>role_permissions</c>.</item>
///   <item><b>R-M3</b> — un permiso compartido sobrevive mientras cualquier módulo que lo contiene siga prendido.</item>
///   <item><b>R-M4</b> — ajuste fino: sólo se puede prender un permiso cubierto por un módulo prendido; sin clasificar ⇒ libre.</item>
///   <item><b>R-M5</b> — la siembra inicial no le da a nadie un permiso que hoy no tiene.</item>
///   <item>Keys sin distinguir mayúsculas (igual que <see cref="CompanyPermissionCalculos"/>).</item>
/// </list>
/// </summary>
public static class PermisoModuloCalculos
{
    public static readonly StringComparer Comparador = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Permisos habilitados de la empresa DESPUÉS de un cambio de módulos o de clasificación.
    /// <para>Por cada permiso del catálogo:</para>
    /// <list type="number">
    ///   <item>Sin clasificar (después) ⇒ conserva su estado anterior.</item>
    ///   <item>Ningún módulo prendido lo cubre ⇒ <b>apagado</b>.</item>
    ///   <item>Lo cubre un módulo que ANTES no lo cubría (se prendió el módulo, o se clasificó el
    ///         permiso en un módulo prendido) ⇒ <b>prendido</b>.</item>
    ///   <item>Si no ⇒ conserva su estado anterior (el ajuste fino sobrevive).</item>
    /// </list>
    /// </summary>
    /// <returns>Keys habilitadas, en el orden del catálogo y con su capitalización.</returns>
    public static IReadOnlyList<string> ResolverHabilitados(
        IEnumerable<string> catalogo,
        EstadoModulosEmpresa antes,
        EstadoModulosEmpresa despues,
        IEnumerable<string> habilitadosAntes)
    {
        var habAntes = ASet(habilitadosAntes);
        var clasifAntes = Indexar(antes?.ModulosPorPermiso);
        var clasifDespues = Indexar(despues?.ModulosPorPermiso);
        var empAntes = ASet(antes?.ModulosDeEmpresa);
        var empDespues = ASet(despues?.ModulosDeEmpresa);

        var salida = new List<string>();
        foreach (var key in Normalizar(catalogo))
        {
            if (!clasifDespues.TryGetValue(key, out var modsDespues) || modsDespues.Count == 0)
            {
                if (habAntes.Contains(key)) salida.Add(key);
                continue;
            }

            var cubren = modsDespues.Where(empDespues.Contains).ToList();
            if (cubren.Count == 0) continue;

            clasifAntes.TryGetValue(key, out var modsAntes);
            var cubreUnoNuevo = cubren.Any(m =>
                !(empAntes.Contains(m) && modsAntes is not null && modsAntes.Contains(m)));

            if (cubreUnoNuevo || habAntes.Contains(key)) salida.Add(key);
        }
        return salida;
    }

    /// <summary>
    /// Gate del ajuste fino (R-M4): keys que se intentan PRENDER y que ningún módulo prendido de la
    /// empresa cubre. Sólo se juzga lo que se agrega — conservar lo que ya estaba prendido nunca falla
    /// (mismo criterio que <see cref="CompanyPermissionCalculos.ResolverNoPermitidas"/>).
    /// </summary>
    /// <param name="modulosDeEmpresa">
    /// <c>null</c> = la empresa todavía no tiene configuración de módulos ⇒ no se rechaza nada.
    /// </param>
    public static IReadOnlyList<string> ResolverNoPermitidos(
        IEnumerable<string> solicitados,
        IEnumerable<string> habilitadosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> modulosPorPermiso,
        IReadOnlyCollection<string>? modulosDeEmpresa)
    {
        if (modulosDeEmpresa is null) return Array.Empty<string>();

        var antes = ASet(habilitadosAntes);
        var empresa = ASet(modulosDeEmpresa);
        var clasif = Indexar(modulosPorPermiso);

        return Normalizar(solicitados)
            .Where(k => !antes.Contains(k))
            .Where(k => clasif.TryGetValue(k, out var mods) && mods.Count > 0 && !mods.Any(empresa.Contains))
            .ToList();
    }

    /// <summary>
    /// Siembra inicial (R-M5): estado final de cada permiso CLASIFICADO de una empresa que ya tenía
    /// <c>company_permissions</c> configurado. Los sin clasificar no aparecen en el resultado ⇒ intactos.
    /// <list type="bullet">
    ///   <item>Ningún módulo prendido lo cubre ⇒ <c>false</c>.</item>
    ///   <item>Cubierto y con fila ⇒ se respeta la fila (un apagado explícito es ajuste fino que ya existía).</item>
    ///   <item>Cubierto y SIN fila ⇒ <c>true</c> sólo si ningún rol de la empresa lo tiene asignado; si lo
    ///         tiene, <c>false</c> — prenderlo resucitaría esa asignación huérfana en el login.</item>
    /// </list>
    /// </summary>
    /// <param name="filasActuales">key → <c>is_enabled</c> de las filas existentes en <c>company_permissions</c>.</param>
    /// <param name="asignadasARolesDeLaEmpresa">Keys presentes en <c>role_permissions</c> de roles de la empresa.</param>
    public static IReadOnlyDictionary<string, bool> ResolverSiembra(
        IEnumerable<string> catalogo,
        EstadoModulosEmpresa estado,
        IReadOnlyDictionary<string, bool> filasActuales,
        IEnumerable<string> asignadasARolesDeLaEmpresa)
    {
        var clasif = Indexar(estado?.ModulosPorPermiso);
        var empresa = ASet(estado?.ModulosDeEmpresa);
        var asignadas = ASet(asignadasARolesDeLaEmpresa);
        var filas = new Dictionary<string, bool>(Comparador);
        foreach (var (k, v) in filasActuales ?? new Dictionary<string, bool>())
        {
            if (!string.IsNullOrWhiteSpace(k)) filas[k.Trim()] = v;
        }

        var salida = new Dictionary<string, bool>(Comparador);
        foreach (var key in Normalizar(catalogo))
        {
            if (!clasif.TryGetValue(key, out var mods) || mods.Count == 0) continue;

            if (!mods.Any(empresa.Contains)) { salida[key] = false; continue; }

            salida[key] = filas.TryGetValue(key, out var habilitado)
                ? habilitado
                : !asignadas.Contains(key);
        }
        return salida;
    }

    /// <summary>Quita nulos/vacíos, recorta y deduplica conservando el orden de entrada.</summary>
    private static List<string> Normalizar(IEnumerable<string>? keys)
    {
        var vistas = new HashSet<string>(Comparador);
        var salida = new List<string>();
        foreach (var key in keys ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var limpia = key.Trim();
            if (vistas.Add(limpia)) salida.Add(limpia);
        }
        return salida;
    }

    private static HashSet<string> ASet(IEnumerable<string>? keys) => new(Normalizar(keys), Comparador);

    private static Dictionary<string, HashSet<string>> Indexar(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? modulosPorPermiso)
    {
        var idx = new Dictionary<string, HashSet<string>>(Comparador);
        foreach (var (permiso, modulos) in modulosPorPermiso ?? new Dictionary<string, IReadOnlyCollection<string>>())
        {
            if (string.IsNullOrWhiteSpace(permiso)) continue;
            var key = permiso.Trim();
            if (!idx.TryGetValue(key, out var set)) idx[key] = set = new HashSet<string>(Comparador);
            set.UnionWith(Normalizar(modulos));
        }
        return idx;
    }
}
