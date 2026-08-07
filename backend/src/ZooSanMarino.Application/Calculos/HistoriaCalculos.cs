// src/ZooSanMarino.Application/Calculos/HistoriaCalculos.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura de la HISTORIA (épica de ItalJira): código correlativo, normalización, sellado de
/// fechas reales, avance derivado de las tareas y rango de plan derivado.
/// </summary>
/// <remarks>
/// La historia comparte vocabulario y reglas de fecha con las tareas: estado, prioridad y sellado
/// DELEGAN en <see cref="TicketTareaCalculos"/> en vez de reimplementarse. Es la regla de «una sola
/// fórmula por número»: si mañana cambia cuándo se sella <c>fecha_inicio_real</c>, cambia en un solo
/// lugar y los dos niveles se mueven juntos.
///
/// El reordenamiento del tablero también se reutiliza: <see cref="TicketTareaCalculos.Reordenar"/>
/// opera sobre <c>Posicion(Id, Estado, Orden)</c>, que no sabe si la fila es tarea o historia.
/// </remarks>
public static class HistoriaCalculos
{
    private const string Prefijo = "HIS";

    // ── Código correlativo ───────────────────────────────────────────────────

    /// <summary>Código de la historia: <c>HIS-{año}-{consecutivo:0000}</c> (ej: <c>HIS-2026-0001</c>).</summary>
    public static string GenerarCodigo(int anio, int consecutivo)
    {
        var n = consecutivo < 1 ? 1 : consecutivo;
        return $"{Prefijo}-{anio:0000}-{n:0000}";
    }

    /// <summary>
    /// Siguiente consecutivo del año a partir de los códigos ya emitidos. Los códigos de otros años
    /// y los que no tienen el formato esperado se ignoran (no rompen la numeración).
    /// </summary>
    /// <remarks>
    /// Se calcula sobre TODOS los códigos emitidos, incluidos los de historias borradas: el
    /// correlativo no se reutiliza, igual que el de las tareas y el de los casos.
    /// </remarks>
    public static int SiguienteConsecutivo(IEnumerable<string?> codigosExistentes, int anio)
    {
        var prefijoAnio = $"{Prefijo}-{anio:0000}-";
        var maximo = 0;

        foreach (var codigo in codigosExistentes)
        {
            if (string.IsNullOrWhiteSpace(codigo)) continue;

            var limpio = codigo.Trim();
            if (!limpio.StartsWith(prefijoAnio, StringComparison.OrdinalIgnoreCase)) continue;

            if (int.TryParse(limpio[prefijoAnio.Length..], out var n) && n > maximo) maximo = n;
        }

        return maximo + 1;
    }

    // ── Normalización de entrada ─────────────────────────────────────────────

    /// <summary>Estado válido en mayúsculas; vacío/desconocido cae a BACKLOG.</summary>
    public static string NormalizarEstado(string? estado) =>
        TicketTareaCalculos.NormalizarEstado(estado);

    /// <summary>Prioridad válida en mayúsculas; vacía/desconocida cae a MEDIA.</summary>
    public static string NormalizarPrioridad(string? prioridad) =>
        TicketTareaCalculos.NormalizarPrioridad(prioridad);

    /// <summary>Mismas marcas de tiempo que una tarea: EN_CURSO sella inicio, LISTO sella fin.</summary>
    public static (DateTime? InicioReal, DateTime? FinReal) SellarFechasReales(
        string estadoNuevo, DateTime? inicioActual, DateTime? finActual, DateTime ahora) =>
        TicketTareaCalculos.SellarFechasReales(estadoNuevo, inicioActual, finActual, ahora);

    // ── Traducción caso → vocabulario de trabajo ─────────────────────────────

    /// <summary>
    /// Traduce el estado de un CASO (<see cref="TicketEstados"/>, 9 valores con su propia máquina)
    /// al vocabulario de las tareas, para que el avance y el tablero de la historia puedan tratar a
    /// casos y tareas como la misma unidad de trabajo.
    /// </summary>
    /// <remarks>
    /// SOLUCIONADO cuenta como terminado a propósito: para el área de desarrollo el trabajo ya está
    /// hecho; el cierre restante es la confirmación del solicitante, que no es esfuerzo de
    /// desarrollo. TRANSFERIDO y SUSPENDIDO caen a BLOQUEADA porque el trabajo no avanza.
    /// </remarks>
    public static string EstadoTrabajoDeCaso(string? estadoCaso) => estadoCaso?.ToUpperInvariant() switch
    {
        TicketEstados.Abierto          => TicketTareaEstados.Backlog,
        TicketEstados.EnAnalisis       => TicketTareaEstados.Analisis,
        TicketEstados.EnDocumentacion  => TicketTareaEstados.Documentacion,
        TicketEstados.EnImplementacion => TicketTareaEstados.EnCurso,
        TicketEstados.EnRevision       => TicketTareaEstados.EnRevision,
        TicketEstados.Solucionado      => TicketTareaEstados.Listo,
        TicketEstados.Cerrado          => TicketTareaEstados.Listo,
        TicketEstados.Transferido      => TicketTareaEstados.Bloqueada,
        TicketEstados.Suspendido       => TicketTareaEstados.Bloqueada,
        _                              => TicketTareaEstados.Backlog,
    };

    // ── Avance ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Porcentaje de avance de la historia (0..100, entero) a partir de los estados de sus trabajos
    /// vivos — tareas, subtareas y casos agrupados cuentan por igual, una unidad cada uno.
    /// </summary>
    /// <remarks>
    /// SIN trabajos la historia no puede derivar nada: se cae a su propio estado (100 % si está en
    /// LISTO, 0 % si no). Es lo que evita que una historia recién creada muestre «100 % completada»
    /// por dividir 0/0.
    /// </remarks>
    public static int AvancePorTareas(IEnumerable<string> estadosDeTrabajosVivos, string? estadoHistoria)
    {
        var estados = estadosDeTrabajosVivos as IList<string> ?? estadosDeTrabajosVivos.ToList();

        if (estados.Count == 0)
            return TicketTareaEstados.EsTerminal(NormalizarEstado(estadoHistoria)) ? 100 : 0;

        var terminadas = estados.Count(TicketTareaEstados.EsTerminal);
        return (int)Math.Round(terminadas * 100m / estados.Count, MidpointRounding.AwayFromZero);
    }

    /// <summary>Cuenta de trabajos terminados / total, para mostrar «12/20» junto a la barra.</summary>
    public static (int Terminados, int Total) ConteoAvance(IEnumerable<string> estadosDeTrabajosVivos)
    {
        var estados = estadosDeTrabajosVivos as IList<string> ?? estadosDeTrabajosVivos.ToList();
        return (estados.Count(TicketTareaEstados.EsTerminal), estados.Count);
    }

    // ── Rango de plan derivado ───────────────────────────────────────────────

    /// <summary>
    /// Rango que debería dibujar el roadmap cuando la historia no tiene fechas propias: el mínimo
    /// de los inicios y el máximo de los fines de sus trabajos. Devuelve <c>(null, null)</c> si
    /// ninguno tiene fechas — así el roadmap sabe que la barra no se puede dibujar.
    /// </summary>
    public static (DateOnly? Inicio, DateOnly? Fin) RangoPlanDerivado(
        IEnumerable<(DateOnly? Inicio, DateOnly? Fin)> fechasDeTrabajos)
    {
        DateOnly? min = null, max = null;

        foreach (var (inicio, fin) in fechasDeTrabajos)
        {
            if (inicio is { } i && (min is null || i < min)) min = i;
            if (fin is { } f && (max is null || f > max)) max = f;
        }

        return (min, max);
    }

    /// <summary>
    /// Fechas efectivas de la barra: las propias de la historia mandan; cada extremo que falte se
    /// completa con el derivado de los trabajos. Nunca devuelve un rango invertido.
    /// </summary>
    public static (DateOnly? Inicio, DateOnly? Fin) RangoEfectivo(
        DateOnly? inicioPropio, DateOnly? finPropio,
        IEnumerable<(DateOnly? Inicio, DateOnly? Fin)> fechasDeTrabajos)
    {
        var (derivadoInicio, derivadoFin) = RangoPlanDerivado(fechasDeTrabajos);

        var inicio = inicioPropio ?? derivadoInicio;
        var fin    = finPropio    ?? derivadoFin;

        if (inicio is { } i && fin is { } f && f < i) fin = inicio;

        return (inicio, fin);
    }
}
