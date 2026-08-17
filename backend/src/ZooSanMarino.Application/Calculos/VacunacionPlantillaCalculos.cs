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

    /// <summary>
    /// Por qué se eligió esa plantilla —o por qué ninguna—, en castellano.
    ///
    /// <para>
    /// No es cosmética. <see cref="ResolverEfectiva"/> devuelve un id, y un id no se puede auditar:
    /// «este lote quedó sin plan» tiene causas muy distintas (la empresa no cargó ninguna, el lote no
    /// tiene raza, todas rigen desde una fecha posterior) y cada una se corrige en otro lado. Sin el
    /// motivo, el usuario ve un vacío y no sabe qué hacer con él.
    /// </para>
    /// </summary>
    /// <param name="idElegida">Id que devolvió <see cref="ResolverEfectiva"/>, o <c>null</c>.</param>
    /// <param name="nombreElegida">Nombre de la elegida, para nombrarla en el mensaje.</param>
    public static string DescribirResolucion(
        IEnumerable<Candidata> candidatas,
        string? lineaProductiva,
        string? raza,
        DateOnly? fechaEncaset,
        int? idElegida,
        string? nombreElegida)
    {
        var linea = (lineaProductiva ?? "").Trim();
        var razaLote = Normalizar(raza);

        var deLaLinea = (candidatas ?? Array.Empty<Candidata>())
            .Where(c => c.Activa && string.Equals((c.LineaProductiva ?? "").Trim(), linea, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (idElegida is { } id)
        {
            var elegida = deLaLinea.FirstOrDefault(c => c.Id == id);
            var nombre = Normalizar(nombreElegida) ?? $"plantilla {id}";

            var porQue = Normalizar(elegida.Raza) is { } r
                ? $"es la plantilla de la raza {r}"
                : razaLote is null
                    ? "es la plantilla general de la línea"
                    : $"es la plantilla general de la línea (no hay una específica para {razaLote})";

            var vigencia = elegida.VigenteDesde is { } d
                ? $", vigente para lotes encasetados desde el {d:dd/MM/yyyy}"
                : "";

            var competencia = deLaLinea.Count > 1 ? $" Le ganó a otras {deLaLinea.Count - 1}." : "";

            return $"Aplica «{nombre}»: {porQue}{vigencia}.{competencia}";
        }

        if (deLaLinea.Count == 0)
            return $"Esta empresa no tiene plantillas activas para {DescribirLinea(linea)}.";

        // Con candidatas y sin elegida, la causa está en uno de los dos filtros. Se reportan en el
        // orden en que descartan: primero la raza, que es la que deja al lote sin ninguna opción.
        var comodines = deLaLinea.Where(c => Normalizar(c.Raza) is null).ToList();
        if (comodines.Count == 0 && razaLote is null)
            return $"Las {deLaLinea.Count} plantillas de {DescribirLinea(linea)} son de una raza específica y este lote " +
                   "no tiene raza cargada. Una plantilla de raza no se le puede asignar a un lote cuya raza se desconoce: " +
                   "cargá la raza del lote o creá una plantilla general de la línea.";

        if (comodines.Count == 0)
        {
            var razas = string.Join(", ", deLaLinea.Select(c => Normalizar(c.Raza)).Where(r => r is not null).Distinct());
            return $"Ninguna plantilla de {DescribirLinea(linea)} es para la raza {razaLote} (hay para: {razas}).";
        }

        // Quedan comodines, así que lo que descartó a todas fue la vigencia.
        if (fechaEncaset is null)
            return $"Hay {deLaLinea.Count} plantilla(s) para {DescribirLinea(linea)}, pero todas rigen desde una fecha y " +
                   "este lote no tiene fecha de encasetamiento. Sin esa fecha no se puede saber cuál le corresponde.";

        var masTemprana = deLaLinea
            .Where(c => c.VigenteDesde.HasValue)
            .Select(c => c.VigenteDesde!.Value)
            .DefaultIfEmpty()
            .Min();

        return $"Las plantillas de {DescribirLinea(linea)} rigen para lotes encasetados desde el " +
               $"{masTemprana:dd/MM/yyyy}, y este se encasetó el {fechaEncaset.Value:dd/MM/yyyy}.";
    }

    private static string DescribirLinea(string linea) => linea.Length == 0 ? "esa línea" : linea;

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

    // ─── Unicidad ─────────────────────────────────────────────────────────────

    /// <summary>Identidad de una plantilla ya guardada, para comparar contra la que se quiere guardar.</summary>
    /// <param name="Id">Para no compararse consigo misma al editar.</param>
    public readonly record struct PlantillaExistente(
        int Id,
        string? Nombre,
        string? LineaProductiva,
        string? Raza,
        DateOnly? VigenteDesde);

    /// <summary>
    /// Motivo por el que la plantilla que se está guardando duplica a otra, o <c>null</c> si no.
    ///
    /// <para>
    /// Duplicar la tupla <c>(línea, raza, vigente desde)</c> <b>no</b> rompe la resolución
    /// —<see cref="ResolverEfectiva"/> es total y elegiría la de id mayor—, pero sí rompe al humano:
    /// dos filas idénticas en pantalla, las dos «vigentes», y ninguna pista de cuál manda. El mensaje
    /// nombra a la que ya existe para que se edite esa en vez de crear una gemela.
    /// </para>
    /// </summary>
    /// <param name="existentes">Plantillas vivas de la MISMA empresa (ya filtradas por empresa y sin borradas).</param>
    /// <param name="idEditando">Id de la plantilla que se edita, o <c>null</c> si es un alta.</param>
    public static string? MotivoPlantillaDuplicada(
        IEnumerable<PlantillaExistente> existentes,
        string? lineaProductiva,
        string? raza,
        DateOnly? vigenteDesde,
        int? idEditando = null)
    {
        if (existentes is null) return null;

        var linea = (lineaProductiva ?? "").Trim();
        var razaNueva = Normalizar(raza);

        foreach (var e in existentes)
        {
            if (idEditando.HasValue && e.Id == idEditando.Value) continue;
            if (!string.Equals((e.LineaProductiva ?? "").Trim(), linea, StringComparison.OrdinalIgnoreCase)) continue;

            var razaExistente = Normalizar(e.Raza);
            var mismaRaza = razaNueva is null
                ? razaExistente is null
                : razaExistente is not null && razaNueva.Equals(razaExistente, StringComparison.OrdinalIgnoreCase);
            if (!mismaRaza) continue;

            if (e.VigenteDesde != vigenteDesde) continue;

            var alcance = razaNueva is null ? "toda la línea" : $"la raza {razaNueva}";
            var vigencia = vigenteDesde is { } d ? $" vigente desde {d:dd/MM/yyyy}" : " sin fecha de vigencia";
            return $"Ya existe la plantilla \"{e.Nombre}\" para {linea} · {alcance}{vigencia}. " +
                   "Editá esa en lugar de crear una igual: con dos idénticas nadie puede saber cuál se aplica.";
        }

        return null;
    }

    /// <summary>Ítem ya cargado en la plantilla, para detectar la carga doble.</summary>
    public readonly record struct ItemExistente(
        int Id,
        int ItemInventarioId,
        string? UnidadObjetivo,
        int ValorObjetivo);

    /// <summary>
    /// Motivo por el que el ítem repite a otro de la misma plantilla, o <c>null</c> si no.
    ///
    /// <para>
    /// La misma vacuna en el <b>mismo</b> objetivo no es un refuerzo —un refuerzo va en otra semana o
    /// en otro día— sino una carga doble, y materializada dejaría dos ítems idénticos en el
    /// cronograma del lote, cada uno pidiendo su registro de aplicación.
    /// </para>
    /// </summary>
    /// <param name="idEditando">Id del ítem que se edita, o <c>null</c> si es un alta.</param>
    public static string? MotivoItemDuplicado(
        IEnumerable<ItemExistente> existentes,
        int itemInventarioId,
        string? unidadObjetivo,
        int valorObjetivo,
        int? idEditando = null)
    {
        if (existentes is null) return null;

        var unidad = (unidadObjetivo ?? "").Trim();

        foreach (var e in existentes)
        {
            if (idEditando.HasValue && e.Id == idEditando.Value) continue;
            if (e.ItemInventarioId != itemInventarioId) continue;
            if (!string.Equals((e.UnidadObjetivo ?? "").Trim(), unidad, StringComparison.OrdinalIgnoreCase)) continue;
            if (e.ValorObjetivo != valorObjetivo) continue;

            var cuando = unidad.Equals(UnidadDia, StringComparison.OrdinalIgnoreCase)
                ? $"el día {valorObjetivo}"
                : $"la semana {valorObjetivo}";
            return $"Esa vacuna ya está programada para {cuando} en esta plantilla. " +
                   "Un refuerzo va en otro momento; repetir el mismo deja dos ítems idénticos en el cronograma del lote.";
        }

        return null;
    }

    /// <summary>
    /// Motivo por el que la unidad no le corresponde a la línea de la plantilla, o <c>null</c>.
    ///
    /// <para>
    /// Sólo se rechaza <b>Engorde programado por semana</b>: un ciclo de engorde entero dura menos de
    /// 7 semanas, así que una franja semanal no distinguiría nada. Al revés sí se permite —un día
    /// exacto en postura es programable— porque ahí la semana es la unidad cómoda, no la única
    /// correcta.
    /// </para>
    /// </summary>
    public static string? MotivoUnidadNoCorrespondeALinea(string? lineaProductiva, string? unidadObjetivo)
    {
        var esEngorde = string.Equals((lineaProductiva ?? "").Trim(), "Engorde", StringComparison.OrdinalIgnoreCase);
        var esSemana = string.Equals((unidadObjetivo ?? "").Trim(), UnidadSemana, StringComparison.OrdinalIgnoreCase);

        if (esEngorde && esSemana)
            return "En Engorde la programación va por día de edad: el ciclo entero dura menos de 7 semanas " +
                   "y una franja semanal no distinguiría nada.";

        return null;
    }
}
