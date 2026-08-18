namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Qué hay que escribir en el cronograma de un lote para que refleje el plan de vacunación de su
/// empresa —y, sobre todo, <b>qué no hay que tocar</b>.
///
/// <para>
/// Ésta es la única fase del módulo que escribe sobre datos de lotes vivos, así que la decisión se
/// toma acá, en una función <b>pura</b>: entra el plan y entra lo que el lote ya tiene, sale la lista
/// de altas, la de modificaciones y la de intocables. Sin EF y sin base de datos, los dos invariantes
/// que no se pueden romper —<i>un ítem ya aplicado nunca se modifica</i> y <i>materializar N veces da
/// el mismo resultado</i>— se prueban con tests de tabla en vez de con un smoke que depende del
/// estado en que quedó una base.
/// </para>
///
/// <para>
/// El mismo cálculo alimenta la <b>vista previa</b> y la <b>aplicación</b>. No es una comodidad: si
/// se calcularan por caminos distintos, el preview empezaría a mentir el día que uno de los dos
/// cambie, y el preview es justamente el gate donde el usuario apoya la decisión de escribir.
/// </para>
///
/// <para>
/// <b>Nada de lo que devuelve borra filas.</b> Ni las suyas. Ver <see cref="Sobrante"/>.
/// </para>
/// </summary>
public static class VacunacionMaterializadorCalculos
{
    // ─── Entradas ─────────────────────────────────────────────────────────────

    /// <summary>Una vacuna del plan de la empresa, tal como se va a copiar al lote.</summary>
    /// <param name="Id">
    /// <c>vacunacion_plan_plantilla_item.id</c>. Es la clave con la que el cronograma del lote
    /// recuerda de dónde salió cada fila, y por lo tanto la clave de la idempotencia.
    /// </param>
    public readonly record struct ItemPlantilla(
        int Id,
        int ItemInventarioId,
        string? UnidadObjetivo,
        int ValorObjetivo,
        int RangoDiasAntes,
        int RangoDiasDespues,
        int Orden,
        string? Notas);

    /// <summary>Una fila que el cronograma del lote ya tiene.</summary>
    /// <param name="OrigenPlantillaItemId">Ítem de plantilla del que salió, o <c>null</c> si se cargó a mano.</param>
    /// <param name="GeneradoAutomatico"><c>false</c> = la escribió o la corrigió una persona.</param>
    /// <param name="TieneRegistro">Ya tiene registro de aplicación. El invariante más duro del módulo.</param>
    public readonly record struct ItemCronograma(
        int Id,
        int? OrigenPlantillaItemId,
        bool GeneradoAutomatico,
        bool TieneRegistro,
        int ItemInventarioId,
        string? UnidadObjetivo,
        int? ValorObjetivo,
        int RangoDiasAntes,
        int RangoDiasDespues,
        int Orden,
        string? Notas);

    // ─── Salidas ──────────────────────────────────────────────────────────────

    /// <summary>Por qué una fila no se toca.</summary>
    public enum MotivoPreservado
    {
        /// <summary>
        /// Tiene registro de aplicación. Cambiarle el objetivo reescribiría la desviación y el
        /// <c>Incumplido</c> de un hecho que <b>ya ocurrió</b>: el reporte de cumplimiento pasaría a
        /// decir otra cosa sobre el pasado.
        /// </summary>
        YaAplicado,

        /// <summary>
        /// Nació a mano, o se emancipó al corregirla alguien. Una corrección sobre <b>este</b> lote es
        /// una decisión explícita y el plan de la empresa no la puede deshacer en silencio.
        /// </summary>
        Manual,

        /// <summary>
        /// Ya dice exactamente lo que dice el plan. Se separa de las otras dos a propósito: en la
        /// vista previa, «12 preservados» no le dice nada al usuario; «10 ya estaban bien y 2 ya se
        /// aplicaron» sí.
        /// </summary>
        SinCambios,
    }

    /// <summary>Por qué una fila derivada del plan ya no le corresponde a ningún ítem del plan.</summary>
    public enum MotivoSobrante
    {
        /// <summary>La vacuna se quitó de la plantilla después de haberse materializado.</summary>
        PlantillaSinEseItem,

        /// <summary>
        /// Dos filas del lote reclaman el mismo ítem de plantilla. El índice único parcial lo hace
        /// imposible de ahora en más; se contempla igual para que la función sea <b>total</b> y no
        /// dependa de que la base esté sana para no perder una fila por el camino.
        /// </summary>
        Duplicado,
    }

    public readonly record struct Preservado(int CronogramaItemId, int OrigenPlantillaItemId, MotivoPreservado Motivo);

    /// <summary>Fila derivada del plan cuyo objetivo, franja, orden, nota o vacuna cambió.</summary>
    public readonly record struct Actualizable(int CronogramaItemId, ItemPlantilla Plantilla);

    /// <summary>
    /// Fila derivada del plan que el plan ya no reclama. <b>No se borra</b>: puede tener registro de
    /// aplicación —y borrarla se llevaría por cascada la prueba de que la vacuna se puso— y, además,
    /// quitar algo del plan a futuro no es lo mismo que declarar que no había que ponerlo en los lotes
    /// que ya venían corriendo. Se reporta para que se decida a mano.
    /// </summary>
    public readonly record struct Sobrante(int CronogramaItemId, int OrigenPlantillaItemId, bool TieneRegistro, MotivoSobrante Motivo);

    /// <summary>Lo que hay que hacerle al cronograma de un lote. Las cuatro listas son disjuntas.</summary>
    public sealed record Plan(
        IReadOnlyList<ItemPlantilla> Faltantes,
        IReadOnlyList<Actualizable> Actualizables,
        IReadOnlyList<Preservado> Preservados,
        IReadOnlyList<Sobrante> Sobrantes)
    {
        /// <summary>Aplicarlo no escribiría nada. Es lo que tiene que dar la segunda pasada.</summary>
        public bool NoEscribeNada => Faltantes.Count == 0 && Actualizables.Count == 0;

        public static readonly Plan Vacio = new([], [], [], []);
    }

    // ─── El cálculo ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reparte cada ítem del plan y cada fila del lote en las cuatro listas.
    ///
    /// <para>
    /// Es <b>total y determinista</b>: no lanza, no descarta nada en silencio y el resultado no
    /// depende del orden en que la base devuelva las filas. Las filas del lote sin
    /// <c>OrigenPlantillaItemId</c> se ignoran por completo —son del lote, no del plan— y por eso un
    /// cronograma cargado a mano es invisible para el materializador.
    /// </para>
    /// </summary>
    /// <param name="plantilla">Ítems vivos de la plantilla efectiva del lote.</param>
    /// <param name="cronograma">Filas vivas del cronograma del lote.</param>
    public static Plan Planificar(IEnumerable<ItemPlantilla>? plantilla, IEnumerable<ItemCronograma>? cronograma)
    {
        var delPlan = (plantilla ?? []).ToList();
        var delLote = (cronograma ?? []).ToList();

        if (delPlan.Count == 0 && delLote.Count == 0) return Plan.Vacio;

        var faltantes = new List<ItemPlantilla>();
        var actualizables = new List<Actualizable>();
        var preservados = new List<Preservado>();
        var sobrantes = new List<Sobrante>();

        // Quién reclama cada ítem del plan. Ante un duplicado imposible manda el id menor —el que se
        // materializó primero— y el otro se reporta como sobrante en vez de desaparecer del informe.
        var reclamadas = new Dictionary<int, ItemCronograma>();
        foreach (var fila in delLote.Where(f => f.OrigenPlantillaItemId.HasValue).OrderBy(f => f.Id))
        {
            var origen = fila.OrigenPlantillaItemId!.Value;
            if (reclamadas.TryAdd(origen, fila)) continue;
            sobrantes.Add(new Sobrante(fila.Id, origen, fila.TieneRegistro, MotivoSobrante.Duplicado));
        }

        foreach (var item in delPlan.OrderBy(i => i.Orden).ThenBy(i => i.ValorObjetivo).ThenBy(i => i.Id))
        {
            if (!reclamadas.TryGetValue(item.Id, out var fila))
            {
                faltantes.Add(item);
                continue;
            }

            // El orden importa: primero lo que no se puede tocar, y recién después si hay diferencia.
            if (fila.TieneRegistro)
                preservados.Add(new Preservado(fila.Id, item.Id, MotivoPreservado.YaAplicado));
            else if (!fila.GeneradoAutomatico)
                preservados.Add(new Preservado(fila.Id, item.Id, MotivoPreservado.Manual));
            else if (YaDiceLoMismo(fila, item))
                preservados.Add(new Preservado(fila.Id, item.Id, MotivoPreservado.SinCambios));
            else
                actualizables.Add(new Actualizable(fila.Id, item));
        }

        var idsDelPlan = delPlan.Select(i => i.Id).ToHashSet();
        foreach (var (origen, fila) in reclamadas.OrderBy(p => p.Value.Id))
        {
            if (idsDelPlan.Contains(origen)) continue;
            sobrantes.Add(new Sobrante(fila.Id, origen, fila.TieneRegistro, MotivoSobrante.PlantillaSinEseItem));
        }

        return new Plan(
            faltantes,
            actualizables,
            preservados.OrderBy(p => p.CronogramaItemId).ToList(),
            sobrantes.OrderBy(s => s.CronogramaItemId).ToList());
    }

    /// <summary>
    /// ¿La fila del lote ya dice exactamente lo que dice el plan?
    ///
    /// <para>
    /// Se comparan los siete campos que el materializador copia —incluidos <c>Orden</c> y
    /// <c>Notas</c>, porque reordenar el plan o corregirle la indicación a una vacuna también es
    /// cambiarlo—. Lo que <b>no</b> se compara es la ubicación (granja/núcleo/galpón): eso sale del
    /// lote, no del plan.
    /// </para>
    /// <para>
    /// Devolver <c>true</c> acá es lo que evita un <c>UPDATE</c> que no cambia nada: además de ensuciar
    /// <c>updated_at</c>, inflaría el contador del preview y haría que la segunda pasada no se viera
    /// idempotente aunque lo fuera.
    /// </para>
    /// </summary>
    private static bool YaDiceLoMismo(ItemCronograma fila, ItemPlantilla item) =>
        fila.ItemInventarioId == item.ItemInventarioId
        && MismoTexto(fila.UnidadObjetivo, item.UnidadObjetivo)
        && fila.ValorObjetivo == item.ValorObjetivo
        && fila.RangoDiasAntes == item.RangoDiasAntes
        && fila.RangoDiasDespues == item.RangoDiasDespues
        && fila.Orden == item.Orden
        && MismoTexto(fila.Notas, item.Notas);

    /// <summary>Texto significativo comparado sin importar mayúsculas ni espacios; vacío = ausente.</summary>
    private static bool MismoTexto(string? a, string? b)
    {
        var x = (a ?? "").Trim();
        var y = (b ?? "").Trim();
        return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
