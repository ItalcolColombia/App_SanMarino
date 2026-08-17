namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Resolución de la plantilla de vacunación que le corresponde a un lote.
///
/// <para>
/// El problema que resuelve: varias plantillas pueden competir por el mismo lote —una específica de
/// la raza y una comodín para toda la línea, o dos versiones del mismo plan con fechas de vigencia
/// distintas—. Sin una regla escrita, cuál gana depende del orden en que la base devuelva las filas,
/// que es decir: <b>del azar</b>. Y el síntoma sería el peor posible: dos lotes iguales con
/// cronogramas distintos y nadie sabiendo por qué.
/// </para>
///
/// <para>
/// La regla, en orden: <b>(1)</b> la raza exacta le gana al comodín; <b>(2)</b> a igual
/// especificidad, la de <c>VigenteDesde</c> más reciente; <b>(3)</b> si hasta ahí empatan, el
/// <c>Id</c> mayor —la última cargada—. El tercer criterio no es cosmético: es lo que garantiza que
/// la función sea <b>total y determinista</b>, o sea que dos corridas sobre los mismos datos den
/// siempre lo mismo.
/// </para>
///
/// <para>
/// Sin candidata aplicable devuelve <c>null</c>, y eso significa exactamente «este lote no tiene
/// cronograma automático». <b>Nunca se inventa uno</b>: un plan sanitario aproximado es peor que no
/// tener plan, porque se ve igual de correcto en pantalla.
/// </para>
/// </summary>
public static class VacunacionPlantillaCalculos
{
    /// <summary>Lo mínimo que hace falta de una plantilla para elegirla. Sin EF ni entidades.</summary>
    /// <param name="Id">Identidad, y desempate final.</param>
    /// <param name="LineaProductiva">"Levante" | "Produccion" | "Engorde".</param>
    /// <param name="Raza">Raza a la que aplica; <c>null</c> o vacío = comodín de la línea.</param>
    /// <param name="VigenteDesde">Aplica a lotes encasetados desde esta fecha; <c>null</c> = siempre.</param>
    /// <param name="Activa">Una plantilla apagada no compite.</param>
    public readonly record struct Candidata(
        int Id,
        string? LineaProductiva,
        string? Raza,
        DateOnly? VigenteDesde,
        bool Activa);

    /// <summary>
    /// Plantilla que le toca a un lote, o <c>null</c> si ninguna aplica.
    /// </summary>
    /// <param name="candidatas">Plantillas de la empresa del lote (ya filtradas por empresa).</param>
    /// <param name="lineaProductiva">Línea del lote.</param>
    /// <param name="raza">Raza del lote; <c>null</c> ⇒ sólo puede tomar comodines.</param>
    /// <param name="fechaEncaset">
    /// Encasetamiento del lote, contra el que se evalúa <c>VigenteDesde</c>. <c>null</c> ⇒ el lote
    /// todavía no tiene fecha, así que sólo aplican las plantillas sin vigencia (fail-closed: una
    /// plantilla con vigencia no se le puede asignar a un lote cuya fecha no se conoce).
    /// </param>
    public static Candidata? ResolverEfectiva(
        IEnumerable<Candidata> candidatas,
        string? lineaProductiva,
        string? raza,
        DateOnly? fechaEncaset)
    {
        if (candidatas is null || string.IsNullOrWhiteSpace(lineaProductiva)) return null;

        var linea = lineaProductiva.Trim();
        var razaLote = Normalizar(raza);

        Candidata? mejor = null;

        foreach (var c in candidatas)
        {
            if (!c.Activa) continue;
            if (!string.Equals((c.LineaProductiva ?? "").Trim(), linea, StringComparison.OrdinalIgnoreCase))
                continue;

            var razaPlantilla = Normalizar(c.Raza);
            var esComodin = razaPlantilla is null;

            // Una plantilla de raza sólo aplica si el lote tiene ESA raza. Un lote sin raza cargada
            // no puede tomarla: no se sabe si le corresponde, y adivinar es inventar un plan.
            if (!esComodin && (razaLote is null || !razaLote.Equals(razaPlantilla, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Vigencia: la plantilla aplica a lotes encasetados EN o DESPUÉS de su fecha.
            if (c.VigenteDesde is { } desde)
            {
                if (fechaEncaset is null) continue;
                if (fechaEncaset.Value < desde) continue;
            }

            if (mejor is null || EsMejor(c, mejor.Value)) mejor = c;
        }

        return mejor;
    }

    /// <summary>
    /// ¿<paramref name="a"/> le gana a <paramref name="b"/>? Los tres criterios, en orden.
    /// Ambas ya pasaron los filtros de aplicabilidad, así que acá sólo se comparan.
    /// </summary>
    private static bool EsMejor(Candidata a, Candidata b)
    {
        var aEspecifica = Normalizar(a.Raza) is not null;
        var bEspecifica = Normalizar(b.Raza) is not null;
        if (aEspecifica != bEspecifica) return aEspecifica;

        // MinValue para las que no tienen vigencia: una plantilla fechada le gana a la genérica,
        // que es la que estaba antes de que alguien se tomara el trabajo de versionar el plan.
        var aDesde = a.VigenteDesde ?? DateOnly.MinValue;
        var bDesde = b.VigenteDesde ?? DateOnly.MinValue;
        if (aDesde != bDesde) return aDesde > bDesde;

        return a.Id > b.Id;
    }

    /// <summary>Texto significativo, o <c>null</c>. Vacío y espacios cuentan como ausencia.</summary>
    private static string? Normalizar(string? s)
    {
        var t = (s ?? "").Trim();
        return t.Length == 0 ? null : t;
    }

    // ─── Unidades ─────────────────────────────────────────────────────────────

    public const string UnidadSemana = "Semana";
    public const string UnidadDia    = "Dia";

    /// <summary>
    /// Unidad que corresponde a una línea productiva: Postura programa por <b>semana</b> de vida y
    /// Engorde por <b>día</b>, porque un ciclo de engorde entero dura menos que 7 semanas y una
    /// franja semanal no distinguiría nada.
    /// </summary>
    public static string UnidadPorDefecto(string? lineaProductiva) =>
        string.Equals((lineaProductiva ?? "").Trim(), "Engorde", StringComparison.OrdinalIgnoreCase)
            ? UnidadDia
            : UnidadSemana;

    /// <summary>
    /// Motivo por el que un ítem de plantilla es inválido, o <c>null</c> si está bien. Se valida acá
    /// —y no sólo con CHECKs— para poder devolver un mensaje que diga qué corregir.
    /// </summary>
    public static string? MotivoItemInvalido(string? unidadObjetivo, int valorObjetivo, int rangoAntes, int rangoDespues)
    {
        var u = (unidadObjetivo ?? "").Trim();
        if (!u.Equals(UnidadSemana, StringComparison.OrdinalIgnoreCase) &&
            !u.Equals(UnidadDia, StringComparison.OrdinalIgnoreCase))
            return "La unidad de la plantilla debe ser 'Semana' o 'Dia'. Una fecha fija no se puede " +
                   "plantillar: sería la misma para lotes encasetados en meses distintos.";

        if (valorObjetivo < 0)
            return "El objetivo no puede ser negativo.";

        if (rangoAntes < 0 || rangoDespues < 0)
            return "La franja de días no puede ser negativa.";

        return null;
    }
}
