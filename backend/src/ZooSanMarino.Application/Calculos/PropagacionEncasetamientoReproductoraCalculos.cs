namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO de la propagación de la fecha/hora de encasetamiento del lote POLLO ENGORDE a sus
/// lotes REPRODUCTORA hijos (plan <c>fase_de_desarrollo/fecha_encaset_recalculo_cascada_plan.md</c> §4.2).
///
/// <para>
/// <b>Por qué se propaga.</b> Es la misma llegada física de pollitos: el lote de engorde y sus
/// reproductora comparten el día en que entraron las aves (medido sobre la copia de producción: 12 de
/// 13 lotes con reproductora tienen exactamente la misma fecha). Si se corrige la del padre y la del
/// hijo queda vieja, las edades de la reproductora se corren y el cruce consolida días equivocados.
/// </para>
///
/// <para>
/// <b>Por qué se diagnostica ANTES de escribir.</b> La ventana de la semana de recogida es
/// <c>edad ∈ [edadMínima, 7]</c>: mover la fecha de encasetamiento puede dejar registros ya
/// capturados fuera de ella. Es mejor un 400 que dice QUÉ registros estorban que un 200 que deja
/// media jerarquía en un estado que la propia regla considera inválido — mismo criterio que
/// <see cref="EncasetamientoRetroactivoCalculos"/>.
/// </para>
/// </summary>
public static class PropagacionEncasetamientoReproductoraCalculos
{
    /// <summary>Un registro que la fecha nueva dejaría fuera de la ventana, con lo necesario para nombrarlo.</summary>
    public readonly record struct RegistroFuera(int LoteReproductoraId, string NombreLote, DateTime Fecha, int Edad);

    /// <summary>Los seguimientos ya capturados de un lote reproductora hijo.</summary>
    public readonly record struct LoteHijo(int Id, string NombreLote, IReadOnlyList<DateTime> FechasRegistros);

    /// <summary>
    /// Resultado del diagnóstico. <see cref="Compatible"/> es la única señal que decide si la
    /// propagación se puede escribir.
    /// </summary>
    public readonly record struct Diagnostico(bool Compatible, IReadOnlyList<RegistroFuera> Fuera);

    /// <summary>
    /// ¿La fecha/hora nueva deja algún registro de los lotes reproductora fuera de su ventana?
    /// <para>
    /// Sin fecha nueva no hay nada que propagar ⇒ compatible. La hora que rige a los hijos es la que
    /// se está propagando (la propia se pisa), así que el mínimo de la ventana sale de ella.
    /// </para>
    /// </summary>
    public static Diagnostico Diagnosticar(
        DateTime? nuevaFechaEncasetamiento, TimeOnly? nuevaHoraEncasetamiento, IEnumerable<LoteHijo> hijos)
    {
        if (!nuevaFechaEncasetamiento.HasValue)
            return new Diagnostico(true, Array.Empty<RegistroFuera>());

        var edadMinima = EncasetamientoCalculos.EdadMinimaConRegistro(nuevaHoraEncasetamiento);
        var fuera = new List<RegistroFuera>();

        foreach (var hijo in hijos)
        {
            foreach (var fecha in hijo.FechasRegistros ?? Array.Empty<DateTime>())
            {
                var edad = ReproductoraEngordeCalculos.EdadSeguimientoDias(nuevaFechaEncasetamiento.Value, fecha);
                if (!ReproductoraEngordeCalculos.EsEdadSeguimientoValida(
                        edad, ReproductoraEngordeCalculos.DiasRecogidaReproductora, edadMinima))
                    fuera.Add(new RegistroFuera(hijo.Id, hijo.NombreLote, fecha, edad));
            }
        }

        return new Diagnostico(fuera.Count == 0, fuera);
    }

    /// <summary>Cuántos registros se nombran en el mensaje antes de resumir el resto.</summary>
    private const int MaximoDetallado = 5;

    /// <summary>
    /// Mensaje de rechazo: dice cuántos registros quedan fuera, en qué lote y en qué fecha, para que
    /// la persona sepa exactamente qué corregir antes de reintentar.
    /// </summary>
    public static string MensajeIncompatible(Diagnostico diagnostico, DateTime nuevaFechaEncasetamiento)
    {
        var detalle = string.Join("; ", diagnostico.Fuera
            .Take(MaximoDetallado)
            .Select(f => $"lote reproductora '{f.NombreLote}' del {f.Fecha:yyyy-MM-dd} (quedaría en edad {f.Edad})"));

        var resto = diagnostico.Fuera.Count > MaximoDetallado
            ? $" y {diagnostico.Fuera.Count - MaximoDetallado} más"
            : string.Empty;

        return $"No se puede mover el encasetamiento al {nuevaFechaEncasetamiento:yyyy-MM-dd}: dejaría "
             + $"{diagnostico.Fuera.Count} registro(s) de reproductora fuera de la semana de recogida "
             + $"(edad válida 0 a {ReproductoraEngordeCalculos.DiasRecogidaReproductora}) — {detalle}{resto}. "
             + "Corregí o eliminá esos registros antes de cambiar la fecha.";
    }
}
