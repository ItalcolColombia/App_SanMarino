namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA del alcance granular de ubicación por usuario-granja (núcleo/galpón/lote o global).
/// Sin EF ni estado: recibe filas proyectadas y computa el CIERRE de visibilidad. Verificable con xUnit.
///
/// Reglas de negocio:
///  - <c>restrictLocations = false</c> (default) ⇒ acceso GLOBAL a la granja (comportamiento clásico,
///    byte a byte idéntico: los consumidores NO aplican ningún filtro adicional).
///  - <c>restrictLocations = true</c> ⇒ el usuario solo ve la unión de sus grants:
///      · grant de NÚCLEO ⇒ todos sus galpones y lotes;
///      · grant de GALPÓN ⇒ todos sus lotes; el núcleo padre queda visible (navegación);
///      · grant de LOTE   ⇒ ese lote; su galpón y núcleo padres quedan visibles (navegación).
///    Con flag encendido y CERO grants ⇒ no ve nada (fail-closed).
///  - Grants que ya no existen en la granja (lote/galpón movido de granja, lugar eliminado) se
///    descartan contra el catálogo actual: una fila muerta NUNCA otorga acceso (fail-closed).
/// </summary>
public static class UserLocationScopeCalculos
{
    /// <summary>Fila de user_farm_scopes proyectada (exactamente un nivel no nulo).</summary>
    public readonly record struct ScopeGrant(string? NucleoId, string? GalponId, int? LoteId);

    /// <summary>Galpón del catálogo de la granja (con su núcleo padre).</summary>
    public readonly record struct GalponUbicacion(string GalponId, string NucleoId);

    /// <summary>Lote del catálogo de la granja (tabla <c>lotes</c>) con sus padres opcionales.</summary>
    public readonly record struct LoteUbicacion(int LoteId, string? NucleoId, string? GalponId);

    /// <summary>Niveles válidos de un grant (contrato del API/UI).</summary>
    public const string LevelNucleo = "nucleo";
    public const string LevelGalpon = "galpon";
    public const string LevelLote   = "lote";

    /// <summary>
    /// Scope efectivo de un usuario sobre una granja. <see cref="IsGlobal"/> = true ⇒ los sets van
    /// vacíos y el consumidor NO debe filtrar (comportamiento clásico).
    /// </summary>
    public sealed record LocationScope(
        bool IsGlobal,
        IReadOnlySet<string> NucleosVisibles,
        IReadOnlySet<string> GalponesVisibles,
        IReadOnlySet<int> LotesPermitidos)
    {
        public static readonly LocationScope Global = new(
            true, new HashSet<string>(), new HashSet<string>(), new HashSet<int>());

        public bool PermiteNucleo(string nucleoId) => IsGlobal || NucleosVisibles.Contains(nucleoId);
        public bool PermiteGalpon(string galponId) => IsGlobal || GalponesVisibles.Contains(galponId);
        public bool PermiteLote(int loteId)        => IsGlobal || LotesPermitidos.Contains(loteId);
    }

    /// <summary>
    /// Computa el cierre de visibilidad para una granja. Los catálogos son los lugares ACTIVOS de la
    /// granja; todo grant se valida contra ellos (una referencia muerta no otorga nada).
    /// </summary>
    public static LocationScope ComputeScope(
        bool restrictLocations,
        IReadOnlyCollection<ScopeGrant> grants,
        IReadOnlyCollection<string> nucleosGranja,
        IReadOnlyCollection<GalponUbicacion> galponesGranja,
        IReadOnlyCollection<LoteUbicacion> lotesGranja)
    {
        if (!restrictLocations) return LocationScope.Global;

        var nucleosCatalogo = nucleosGranja as ISet<string> ?? new HashSet<string>(nucleosGranja);
        var galponPorId = new Dictionary<string, GalponUbicacion>();
        foreach (var g in galponesGranja) galponPorId[g.GalponId] = g;
        var lotePorId = new Dictionary<int, LoteUbicacion>();
        foreach (var l in lotesGranja) lotePorId[l.LoteId] = l;

        // Grants válidos (existentes en la granja hoy)
        var nucleosGranted = new HashSet<string>();
        var galponesGranted = new HashSet<string>();
        var lotesGranted = new HashSet<int>();
        foreach (var gr in grants)
        {
            if (gr.NucleoId is not null && nucleosCatalogo.Contains(gr.NucleoId)) nucleosGranted.Add(gr.NucleoId);
            else if (gr.GalponId is not null && galponPorId.ContainsKey(gr.GalponId)) galponesGranted.Add(gr.GalponId);
            else if (gr.LoteId is int loteId && lotePorId.ContainsKey(loteId)) lotesGranted.Add(loteId);
        }

        // Galpones con acceso a DATOS: granted directos + los de núcleos granted
        var galponesConAcceso = new HashSet<string>(galponesGranted);
        foreach (var g in galponesGranja)
            if (nucleosGranted.Contains(g.NucleoId)) galponesConAcceso.Add(g.GalponId);

        // Lotes permitidos: granted directos + los de núcleos granted + los de galpones con acceso
        var lotesPermitidos = new HashSet<int>(lotesGranted);
        foreach (var l in lotesGranja)
        {
            if (l.NucleoId is not null && nucleosGranted.Contains(l.NucleoId)) lotesPermitidos.Add(l.LoteId);
            else if (l.GalponId is not null && galponesConAcceso.Contains(l.GalponId)) lotesPermitidos.Add(l.LoteId);
        }

        // Visibilidad para NAVEGACIÓN (catálogos): ancestros de cada grant
        var galponesVisibles = new HashSet<string>(galponesConAcceso);
        var nucleosVisibles = new HashSet<string>(nucleosGranted);
        foreach (var galponId in galponesGranted)
            if (galponPorId.TryGetValue(galponId, out var g)) nucleosVisibles.Add(g.NucleoId);
        foreach (var loteId in lotesGranted)
        {
            if (!lotePorId.TryGetValue(loteId, out var l)) continue;
            if (l.GalponId is not null && galponPorId.ContainsKey(l.GalponId))
            {
                galponesVisibles.Add(l.GalponId);
                nucleosVisibles.Add(galponPorId[l.GalponId].NucleoId);
            }
            if (l.NucleoId is not null && nucleosCatalogo.Contains(l.NucleoId)) nucleosVisibles.Add(l.NucleoId);
        }

        return new LocationScope(false, nucleosVisibles, galponesVisibles, lotesPermitidos);
    }

    // ─── Helpers de filtrado en memoria para catálogos multi-granja ───
    // (los servicios pasan solo las granjas RESTRINGIDAS; una granja ausente del diccionario = global)

    public static bool NucleoVisible(IReadOnlyDictionary<int, LocationScope> restringidos, int granjaId, string nucleoId)
        => !restringidos.TryGetValue(granjaId, out var s) || s.PermiteNucleo(nucleoId);

    public static bool GalponVisible(IReadOnlyDictionary<int, LocationScope> restringidos, int granjaId, string galponId)
        => !restringidos.TryGetValue(granjaId, out var s) || s.PermiteGalpon(galponId);

    public static bool LotePermitido(IReadOnlyDictionary<int, LocationScope> restringidos, int granjaId, int? loteId)
        => !restringidos.TryGetValue(granjaId, out var s) || (loteId.HasValue && s.PermiteLote(loteId.Value));

    /// <summary>
    /// Valida un item de grant entrante (API admin): exactamente un nivel según <paramref name="level"/>.
    /// Devuelve mensaje de error o null si es válido.
    /// </summary>
    public static string? ValidarItem(string? level, string? nucleoId, string? galponId, int? loteId)
    {
        switch (level)
        {
            case LevelNucleo:
                if (string.IsNullOrWhiteSpace(nucleoId)) return "El item de nivel 'nucleo' requiere nucleoId.";
                if (galponId is not null || loteId is not null) return "El item de nivel 'nucleo' no admite galponId ni loteId.";
                return null;
            case LevelGalpon:
                if (string.IsNullOrWhiteSpace(galponId)) return "El item de nivel 'galpon' requiere galponId.";
                if (nucleoId is not null || loteId is not null) return "El item de nivel 'galpon' no admite nucleoId ni loteId.";
                return null;
            case LevelLote:
                if (loteId is null) return "El item de nivel 'lote' requiere loteId.";
                if (nucleoId is not null || galponId is not null) return "El item de nivel 'lote' no admite nucleoId ni galponId.";
                return null;
            default:
                return $"Nivel inválido '{level}'. Use '{LevelNucleo}', '{LevelGalpon}' o '{LevelLote}'.";
        }
    }
}
