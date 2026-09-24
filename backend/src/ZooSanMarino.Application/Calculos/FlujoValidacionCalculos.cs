namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo puro (sin EF, sin estado) del motor de flujos de validación: valida la topología de
/// una definición antes de publicar y resuelve las transiciones de una instancia al aprobar o
/// devolver. Es la especificación ejecutable del motor — sus tests son el contrato que el service
/// transaccional debe cumplir (ver CLAUDE.md §"Una sola fórmula por número").
/// </summary>
public static class FlujoValidacionCalculos
{
    public const int MinEtapas = 1;
    public const int MaxEtapas = 20;

    public sealed record PasoTopologia(int Orden, bool TieneCandidatos);

    /// <summary>
    /// Valida que una definición sea publicable: 1..20 etapas, órdenes 1..N sin huecos ni
    /// repetidos, y cada etapa con al menos un candidato. Devuelve la lista de errores concretos
    /// (vacía si es publicable) para que la UI muestre el preflight, no un booleano ciego.
    /// </summary>
    public static IReadOnlyList<string> ValidarTopologiaParaPublicar(IReadOnlyList<PasoTopologia> pasos)
    {
        var errores = new List<string>();

        if (pasos.Count < MinEtapas || pasos.Count > MaxEtapas)
        {
            errores.Add($"El flujo debe tener entre {MinEtapas} y {MaxEtapas} etapas (tiene {pasos.Count}).");
            return errores;
        }

        var ordenes = pasos.Select(p => p.Orden).OrderBy(o => o).ToList();
        var esperados = Enumerable.Range(1, pasos.Count).ToList();
        if (!ordenes.SequenceEqual(esperados))
        {
            errores.Add("El orden de las etapas debe ser 1..N consecutivo, sin huecos ni repetidos.");
        }

        foreach (var paso in pasos.Where(p => !p.TieneCandidatos))
        {
            errores.Add($"La etapa {paso.Orden} no tiene ningún candidato (rol o usuario) asignado.");
        }

        return errores;
    }

    /// <summary>
    /// Al aprobar la etapa <paramref name="pasoActualOrden"/> de un flujo con <paramref name="totalPasos"/>
    /// etapas: true si era la última (la instancia debe finalizar y aplicar efectos), false si
    /// simplemente avanza a la siguiente etapa.
    /// </summary>
    public static bool EsUltimaEtapa(int pasoActualOrden, int totalPasos) =>
        pasoActualOrden >= totalPasos;

    /// <summary>Etapa que queda PENDIENTE tras aprobar una etapa que no era la última.</summary>
    public static int SiguienteEtapa(int pasoActualOrden) => pasoActualOrden + 1;

    /// <summary>
    /// Al devolver desde la etapa <paramref name="pasoQueDevuelveOrden"/>: retrocede exactamente una
    /// etapa. Si se devuelve la etapa 1, no hay etapa anterior (0 = responsabilidad del creador).
    /// </summary>
    public static int EtapaDestinoAlDevolver(int pasoQueDevuelveOrden) =>
        Math.Max(0, pasoQueDevuelveOrden - 1);

    /// <summary>True si la etapa devuelta es la primera, cuyo responsable de corrección es el creador.</summary>
    public static bool DevolucionApuntaAlCreador(int pasoQueDevuelveOrden) =>
        pasoQueDevuelveOrden <= 1;

    /// <summary>
    /// Al corregir y reenviar desde la etapa que recibió la novedad (<paramref name="pasoRetornoOrden"/>):
    /// esa etapa vuelve a estar pendiente para que la firme de nuevo su responsable. Las etapas
    /// aprobadas antes del punto de retroceso permanecen (regla §4.11 del plan).
    /// </summary>
    public static int EtapaTrasReenviar(int pasoRetornoOrden) => pasoRetornoOrden;

    /// <summary>
    /// Calcula el vencimiento total de una instancia a partir de cuándo se creó y el plazo
    /// configurado en el flujo (horas). El plazo se evalúa sobre la instancia completa en Fase 1
    /// (no hay SLA por etapa todavía).
    /// </summary>
    public static DateTime CalcularVencimiento(DateTime creadaEnUtc, int plazoTotalHoras) =>
        creadaEnUtc.AddHours(Math.Max(1, plazoTotalHoras));

    public static bool EstaVencida(DateTime creadaEnUtc, int plazoTotalHoras, DateTime ahoraUtc) =>
        ahoraUtc > CalcularVencimiento(creadaEnUtc, plazoTotalHoras);
}
