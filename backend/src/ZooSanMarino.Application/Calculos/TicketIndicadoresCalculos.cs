// src/ZooSanMarino.Application/Calculos/TicketIndicadoresCalculos.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Indicadores del panel de control de tickets (sin EF ni estado): volumen, efectividad, tiempos
/// promedio y desgloses por país, estado, tipo, prioridad y responsable.
/// </summary>
/// <remarks>
/// La BD filtra y proyecta las filas; el reparto y los promedios se hacen acá para poder fijarlos
/// con tests. Todo promedio ignora las filas sin el dato en vez de contarlas como cero: un caso que
/// nadie tomó todavía no puede bajar el promedio de primera respuesta.
/// </remarks>
public static class TicketIndicadoresCalculos
{
    /// <summary>Proyección mínima de un caso — lo justo para todos los indicadores.</summary>
    public readonly record struct FilaCaso(
        long Id,
        int PaisId,
        string? PaisNombre,
        int CompanyId,
        string? EmpresaNombre,
        string Tipo,
        string Estado,
        string Prioridad,
        Guid? AsignadoGuid,
        string? AsignadoNombre,
        DateTime CreatedAt,
        DateTime? PrimeraApertura,
        DateTime? FechaSolucion,
        DateTime? FechaCierre,
        DateTime? FechaLimite,
        decimal? HorasEstimadas,
        decimal HorasRegistradas,
        int TareasTotal,
        int TareasListas);

    // ── Salidas ──────────────────────────────────────────────────────────────

    public readonly record struct Resumen(
        int Total,
        int Abiertos,
        int EnCurso,
        int Solucionados,
        int Cerrados,
        int Suspendidos,
        int Vencidos,
        int PorVencer,
        int SinAsignar,
        int TareasTotal,
        int TareasListas,
        int TareasPendientes,
        decimal HorasEstimadas,
        decimal HorasRegistradas,
        double? PromedioPrimeraRespuesta,
        double? PromedioResolucion,
        double? PromedioConfirmacionCierre,
        decimal? Efectividad,
        decimal PorcentajeResueltos,
        decimal AvanceTareas,
        int ConCompromiso,
        int CompromisoCumplido);

    /// <summary>
    /// Desglose de un agrupador con identidad propia (país o empresa). Mismos indicadores en
    /// los dos: el administrador compara países y empresas con la misma vara.
    /// </summary>
    public readonly record struct FilaGrupo(
        int Id, string Nombre, int Total, int Abiertos, int EnCurso, int Resueltos,
        int Vencidos, decimal HorasRegistradas, decimal AvanceTareas,
        double? PromedioResolucion, decimal? Efectividad);

    public readonly record struct FilaCategoria(
        string Clave, int Total, int Resueltos, int Vencidos, double? PromedioResolucion);

    public readonly record struct FilaResponsable(
        Guid? Guid, string Nombre, int Asignados, int Resueltos, int Vencidos,
        decimal HorasRegistradas, int TareasListas, double? PromedioResolucion);

    // ── Clasificación ────────────────────────────────────────────────────────

    /// <summary>Un caso cuenta como resuelto cuando llegó a SOLUCIONADO o a CERRADO.</summary>
    public static bool EsResuelto(string estado) =>
        estado.Equals(TicketEstados.Solucionado, StringComparison.OrdinalIgnoreCase) ||
        estado.Equals(TicketEstados.Cerrado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Está siendo trabajado: alguna de las cuatro fases, o transferido.</summary>
    public static bool EsEnCurso(string estado) =>
        TicketEstados.FasesTrabajo.Contains(estado, StringComparer.OrdinalIgnoreCase) ||
        estado.Equals(TicketEstados.Transferido, StringComparison.OrdinalIgnoreCase);

    // ── Resumen general ──────────────────────────────────────────────────────

    public static Resumen CalcularResumen(IEnumerable<FilaCaso> filas, DateTime ahora)
    {
        var lista = filas as IList<FilaCaso> ?? filas.ToList();
        var total = lista.Count;

        var conCompromiso = 0;
        var cumplidos = 0;
        var vencidos = 0;
        var porVencer = 0;
        var umbral = ahora.AddHours(TicketMetricasCalculos.HorasUmbralPorVencer);

        foreach (var f in lista)
        {
            var sla = TicketMetricasCalculos.EstadoSla(f.FechaLimite, f.FechaSolucion, ahora);
            switch (sla)
            {
                case TicketMetricasCalculos.SlaCumplido:   conCompromiso++; cumplidos++; break;
                case TicketMetricasCalculos.SlaIncumplido: conCompromiso++; break;
                case TicketMetricasCalculos.SlaVencido:    conCompromiso++; vencidos++; break;
                case TicketMetricasCalculos.SlaPorVencer:  conCompromiso++; porVencer++; break;
                case TicketMetricasCalculos.SlaEnTiempo:   conCompromiso++; break;
            }
        }

        var tareasTotal = lista.Sum(f => f.TareasTotal);
        var tareasListas = lista.Sum(f => f.TareasListas);
        var resueltos = lista.Count(f => EsResuelto(f.Estado));

        return new Resumen(
            Total:          total,
            Abiertos:       lista.Count(f => f.Estado.Equals(TicketEstados.Abierto, StringComparison.OrdinalIgnoreCase)),
            EnCurso:        lista.Count(f => EsEnCurso(f.Estado)),
            Solucionados:   lista.Count(f => f.Estado.Equals(TicketEstados.Solucionado, StringComparison.OrdinalIgnoreCase)),
            Cerrados:       lista.Count(f => f.Estado.Equals(TicketEstados.Cerrado, StringComparison.OrdinalIgnoreCase)),
            Suspendidos:    lista.Count(f => f.Estado.Equals(TicketEstados.Suspendido, StringComparison.OrdinalIgnoreCase)),
            Vencidos:       vencidos,
            PorVencer:      porVencer,
            SinAsignar:     lista.Count(f => f.AsignadoGuid is null),
            TareasTotal:    tareasTotal,
            TareasListas:   tareasListas,
            TareasPendientes: Math.Max(0, tareasTotal - tareasListas),
            HorasEstimadas:   Redondear(lista.Sum(f => f.HorasEstimadas ?? 0m)),
            HorasRegistradas: Redondear(lista.Sum(f => f.HorasRegistradas)),
            PromedioPrimeraRespuesta: PromedioHoras(lista
                .Where(f => f.PrimeraApertura.HasValue)
                .Select(f => TicketMetricasCalculos.HorasPrimeraRespuesta(f.CreatedAt, f.PrimeraApertura)!.Value)),
            PromedioResolucion: PromedioHoras(lista
                .Where(f => f.FechaSolucion.HasValue)
                .Select(f => TicketMetricasCalculos.HorasResolucion(f.CreatedAt, f.FechaSolucion, ahora))),
            PromedioConfirmacionCierre: PromedioHoras(lista
                .Select(f => TicketMetricasCalculos.HorasConfirmacionCierre(f.FechaSolucion, f.FechaCierre))
                .Where(h => h.HasValue).Select(h => h!.Value)),
            Efectividad: Porcentaje(cumplidos, conCompromiso),
            PorcentajeResueltos: Porcentaje(resueltos, total) ?? 0m,
            AvanceTareas: TicketMetricasCalculos.PorcentajeAvanceTareas(tareasTotal, tareasListas),
            ConCompromiso: conCompromiso,
            CompromisoCumplido: cumplidos);
    }

    // ── Desglose por país y por empresa ──────────────────────────────────────

    public static IReadOnlyList<FilaGrupo> PorPais(IEnumerable<FilaCaso> filas, DateTime ahora) =>
        Agrupar(filas, f => (f.PaisId, f.PaisNombre), "País", ahora);

    public static IReadOnlyList<FilaGrupo> PorEmpresa(IEnumerable<FilaCaso> filas, DateTime ahora) =>
        Agrupar(filas, f => (f.CompanyId, f.EmpresaNombre), "Empresa", ahora);

    /// <summary>
    /// Motor común de los desgloses con identidad. <paramref name="etiquetaSinNombre"/> arma el
    /// respaldo cuando el catálogo no resolvió el nombre («País 9», «Empresa 4»): esconder el
    /// grupo sería peor, porque sus casos desaparecerían del total del desglose.
    /// </summary>
    private static IReadOnlyList<FilaGrupo> Agrupar(
        IEnumerable<FilaCaso> filas, Func<FilaCaso, (int Id, string? Nombre)> clave,
        string etiquetaSinNombre, DateTime ahora) =>
        filas.GroupBy(clave)
            .Select(g =>
            {
                var r = CalcularResumen(g.ToList(), ahora);
                return new FilaGrupo(
                    g.Key.Id,
                    string.IsNullOrWhiteSpace(g.Key.Nombre) ? $"{etiquetaSinNombre} {g.Key.Id}" : g.Key.Nombre!,
                    r.Total, r.Abiertos, r.EnCurso, r.Solucionados + r.Cerrados, r.Vencidos,
                    r.HorasRegistradas, r.AvanceTareas, r.PromedioResolucion, r.Efectividad);
            })
            .OrderByDescending(p => p.Total).ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ── Desgloses por categoría (estado / tipo / prioridad) ──────────────────

    /// <summary>Agrupa por una clave arbitraria del caso (estado, tipo, prioridad…).</summary>
    public static IReadOnlyList<FilaCategoria> PorCategoria(
        IEnumerable<FilaCaso> filas, Func<FilaCaso, string> clave, DateTime ahora) =>
        filas.GroupBy(clave, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToList();
                return new FilaCategoria(
                    g.Key.ToUpperInvariant(),
                    items.Count,
                    items.Count(f => EsResuelto(f.Estado)),
                    items.Count(f => TicketMetricasCalculos.EstadoSla(f.FechaLimite, f.FechaSolucion, ahora)
                                     == TicketMetricasCalculos.SlaVencido),
                    PromedioHoras(items.Where(f => f.FechaSolucion.HasValue)
                        .Select(f => TicketMetricasCalculos.HorasResolucion(f.CreatedAt, f.FechaSolucion, ahora))));
            })
            .OrderByDescending(c => c.Total).ThenBy(c => c.Clave, StringComparer.Ordinal)
            .ToList();

    /// <summary>Desglose por estado en el orden del tablero (incluye los estados en cero).</summary>
    public static IReadOnlyList<FilaCategoria> PorEstado(IEnumerable<FilaCaso> filas, DateTime ahora)
    {
        var lista = filas as IList<FilaCaso> ?? filas.ToList();
        var mapa = PorCategoria(lista, f => f.Estado, ahora)
            .ToDictionary(c => c.Clave, StringComparer.OrdinalIgnoreCase);

        // Se listan TODOS los estados, también los que están en cero: un tablero al que le falta
        // una columna se lee como si el estado no existiera.
        return TicketEstados.ColumnasTablero
            .Concat(new[] { TicketEstados.Transferido, TicketEstados.Suspendido })
            .Select(e => mapa.TryGetValue(e, out var c) ? c : new FilaCategoria(e, 0, 0, 0, null))
            .ToList();
    }

    // ── Desglose por responsable ─────────────────────────────────────────────

    public static IReadOnlyList<FilaResponsable> PorResponsable(IEnumerable<FilaCaso> filas, DateTime ahora) =>
        filas.GroupBy(f => new { f.AsignadoGuid, f.AsignadoNombre })
            .Select(g =>
            {
                var items = g.ToList();
                return new FilaResponsable(
                    g.Key.AsignadoGuid,
                    string.IsNullOrWhiteSpace(g.Key.AsignadoNombre) ? "Sin asignar" : g.Key.AsignadoNombre!,
                    items.Count,
                    items.Count(f => EsResuelto(f.Estado)),
                    items.Count(f => TicketMetricasCalculos.EstadoSla(f.FechaLimite, f.FechaSolucion, ahora)
                                     == TicketMetricasCalculos.SlaVencido),
                    Redondear(items.Sum(f => f.HorasRegistradas)),
                    items.Sum(f => f.TareasListas),
                    PromedioHoras(items.Where(f => f.FechaSolucion.HasValue)
                        .Select(f => TicketMetricasCalculos.HorasResolucion(f.CreatedAt, f.FechaSolucion, ahora))));
            })
            .OrderByDescending(r => r.Asignados).ThenBy(r => r.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Promedio en horas con dos decimales. Null si no hay ningún valor que promediar.</summary>
    private static double? PromedioHoras(IEnumerable<double> valores)
    {
        var lista = valores as IList<double> ?? valores.ToList();
        return lista.Count == 0 ? null : Math.Round(lista.Average(), 2);
    }

    /// <summary>Porcentaje 0..100 con un decimal. Null cuando el denominador es cero.</summary>
    private static decimal? Porcentaje(int parte, int total) =>
        total <= 0 ? null : Math.Round(parte * 100m / total, 1);

    private static decimal Redondear(decimal v) => Math.Round(v, 2);
}
