// src/ZooSanMarino.Application/Calculos/TicketTareaCalculos.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del tablero (sin EF ni estado): reordenamiento al soltar una tarjeta, generación
/// del código correlativo de tarea y normalización de tipo/estado/prioridad.
/// </summary>
/// <remarks>
/// El reordenamiento es la parte delicada del drag &amp; drop: si el <c>orden</c> queda con huecos o
/// repetido, la próxima carga del tablero devuelve las tarjetas barajadas. Estas funciones
/// garantizan que cada columna tocada quede con <c>orden</c> 0..n-1 exacto.
/// </remarks>
public static class TicketTareaCalculos
{
    /// <summary>Posición de una tarjeta dentro de una columna del tablero.</summary>
    public readonly record struct Posicion(long Id, string Estado, int Orden);

    /// <summary>
    /// Recalcula las posiciones tras soltar la tarjeta <paramref name="idMovido"/> en
    /// <paramref name="estadoDestino"/>, en el índice <paramref name="indiceDestino"/>.
    ///
    /// Devuelve SOLO las tarjetas cuya posición cambió (incluida la movida), para que el service
    /// actualice lo mínimo. Las columnas no involucradas no se tocan.
    /// </summary>
    /// <param name="actuales">Estado actual de todas las tarjetas del tablero.</param>
    /// <param name="idMovido">Tarjeta arrastrada.</param>
    /// <param name="estadoDestino">Columna donde se soltó.</param>
    /// <param name="indiceDestino">Posición dentro de la columna destino (se recorta a [0, n]).</param>
    public static IReadOnlyList<Posicion> Reordenar(
        IEnumerable<Posicion> actuales, long idMovido, string estadoDestino, int indiceDestino)
    {
        var lista = actuales.ToList();
        var movido = lista.FirstOrDefault(p => p.Id == idMovido);
        if (movido.Id != idMovido) return Array.Empty<Posicion>();   // no está en el tablero

        var destino = estadoDestino.ToUpperInvariant();
        var estadoOrigen = movido.Estado.ToUpperInvariant();

        // Columna destino sin la tarjeta movida, en su orden actual.
        var columnaDestino = lista
            .Where(p => p.Id != idMovido && p.Estado.Equals(destino, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Orden)
            .ToList();

        var indice = Math.Clamp(indiceDestino, 0, columnaDestino.Count);
        columnaDestino.Insert(indice, movido with { Estado = destino });

        var resultado = new List<Posicion>();
        for (var i = 0; i < columnaDestino.Count; i++)
        {
            var p = columnaDestino[i];
            // La movida siempre se reporta: cambió de columna y/o de posición.
            if (p.Id == idMovido || p.Orden != i)
                resultado.Add(p with { Estado = destino, Orden = i });
        }

        // Si cambió de columna, la de origen se compacta para no dejar huecos.
        if (!estadoOrigen.Equals(destino, StringComparison.OrdinalIgnoreCase))
        {
            var columnaOrigen = lista
                .Where(p => p.Id != idMovido && p.Estado.Equals(estadoOrigen, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Orden)
                .ToList();

            for (var i = 0; i < columnaOrigen.Count; i++)
                if (columnaOrigen[i].Orden != i)
                    resultado.Add(columnaOrigen[i] with { Orden = i });
        }

        return resultado;
    }

    /// <summary>
    /// Código correlativo de una tarea dentro de su caso: <c>{codigoCaso}-T{n}</c>.
    /// Sin código de caso cae a <c>TK-{ticketId}-T{n}</c> para no dejar la tarea sin identificador.
    /// </summary>
    public static string GenerarCodigoTarea(string? codigoCaso, long ticketId, int consecutivo)
    {
        var n = consecutivo < 1 ? 1 : consecutivo;
        var baseCodigo = string.IsNullOrWhiteSpace(codigoCaso) ? $"TK-{ticketId}" : codigoCaso.Trim();
        return $"{baseCodigo}-T{n}";
    }

    /// <summary>Siguiente consecutivo a partir de los códigos ya emitidos en el caso.</summary>
    public static int SiguienteConsecutivo(IEnumerable<string?> codigosExistentes)
    {
        var maximo = 0;
        foreach (var codigo in codigosExistentes)
        {
            if (string.IsNullOrWhiteSpace(codigo)) continue;
            var idx = codigo.LastIndexOf("-T", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            if (int.TryParse(codigo[(idx + 2)..], out var n) && n > maximo) maximo = n;
        }
        return maximo + 1;
    }

    // ── Normalización de entrada ─────────────────────────────────────────────

    /// <summary>Tipo válido en mayúsculas; vacío/desconocido cae a TAREA.</summary>
    public static string NormalizarTipo(string? tipo) =>
        TicketTareaTipos.EsValido(tipo) ? tipo!.ToUpperInvariant() : TicketTareaTipos.Tarea;

    /// <summary>Estado válido en mayúsculas; vacío/desconocido cae a BACKLOG.</summary>
    public static string NormalizarEstado(string? estado) =>
        TicketTareaEstados.EsValido(estado) ? estado!.ToUpperInvariant() : TicketTareaEstados.Backlog;

    /// <summary>Prioridad válida en mayúsculas; vacía/desconocida cae a MEDIA.</summary>
    public static string NormalizarPrioridad(string? prioridad) =>
        TicketPrioridades.EsValida(prioridad) ? prioridad!.ToUpperInvariant() : TicketPrioridades.Media;

    /// <summary>
    /// Marcas de tiempo reales que corresponden al pasar a un estado.
    /// <c>EN_CURSO</c> sella el inicio (solo la primera vez) y <c>LISTO</c> sella el fin;
    /// sacar una tarea de LISTO borra el fin, porque volvió a estar en juego.
    /// </summary>
    public static (DateTime? InicioReal, DateTime? FinReal) SellarFechasReales(
        string estadoNuevo, DateTime? inicioActual, DateTime? finActual, DateTime ahora)
    {
        var estado = NormalizarEstado(estadoNuevo);

        var inicio = inicioActual;
        if (inicio is null && !estado.Equals(TicketTareaEstados.Backlog, StringComparison.OrdinalIgnoreCase))
            inicio = ahora;

        DateTime? fin = TicketTareaEstados.EsTerminal(estado) ? finActual ?? ahora : null;

        return (inicio, fin);
    }
}
