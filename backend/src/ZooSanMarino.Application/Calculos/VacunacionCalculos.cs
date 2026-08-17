namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin EF/estado) del módulo Vacunación: franja válida de un ítem de cronograma
/// (semana/día/fecha) y estado + desviación de un registro de aplicación frente a esa franja.
/// Genérico entre líneas: Postura programa por semana, Engorde por día de edad; ambas resuelven
/// a la misma franja [FechaInicio, FechaFin] en días de calendario.
/// </summary>
public static class VacunacionCalculos
{
    public const string UnidadSemana = "Semana";
    public const string UnidadDia = "Dia";
    public const string UnidadFecha = "Fecha";

    public const string EstadoPendiente = "Pendiente";
    public const string EstadoAplicado = "Aplicado";
    public const string EstadoAplicadoTardio = "AplicadoTardio";
    public const string EstadoAplicadoAdelantado = "AplicadoAdelantado";
    public const string EstadoNoAplicado = "NoAplicado";

    public readonly record struct Franja(DateTime FechaInicio, DateTime FechaFin);

    public readonly record struct ResultadoAplicacion(
        string Estado, int DiasDesviacion, bool Incumplido, bool RequiereMotivo);

    /// <summary>
    /// Proyección de una aplicación SIN el umbral de incumplimiento: estado, desviación y si exige
    /// motivo. Es la parte de <see cref="CalcularEstadoAplicacion"/> que no depende de la
    /// configuración por empresa, y por eso puede responderse antes de guardar (pre-chequeo de la UI).
    /// </summary>
    public readonly record struct ProyeccionAplicacion(
        string Estado, int DiasDesviacion, bool RequiereMotivo);

    /// <summary>
    /// Franja válida del ítem de cronograma. "Semana" y "Dia" se resuelven contra
    /// <paramref name="fechaEncaset"/> (edad en días desde encaset); "Fecha" usa
    /// <paramref name="fechaObjetivo"/> directamente, sin importar la fase.
    /// </summary>
    public static Franja CalcularFranja(
        DateTime? fechaEncaset, string unidadObjetivo, int? valorObjetivo, DateTime? fechaObjetivo,
        int rangoDiasAntes, int rangoDiasDespues)
    {
        DateTime fechaBase = unidadObjetivo switch
        {
            UnidadSemana when fechaEncaset.HasValue && valorObjetivo.HasValue
                => fechaEncaset.Value.Date.AddDays((valorObjetivo.Value - 1) * 7),
            UnidadDia when fechaEncaset.HasValue && valorObjetivo.HasValue
                => fechaEncaset.Value.Date.AddDays(valorObjetivo.Value),
            UnidadFecha when fechaObjetivo.HasValue
                => fechaObjetivo.Value.Date,
            _ => throw new InvalidOperationException(
                $"No se puede calcular la franja: unidadObjetivo='{unidadObjetivo}' requiere fechaEncaset+valorObjetivo (Semana/Dia) o fechaObjetivo (Fecha).")
        };

        return new Franja(fechaBase.AddDays(-rangoDiasAntes), fechaBase.AddDays(rangoDiasDespues));
    }

    /// <summary>
    /// Estado + desviación de una aplicación frente a la franja, SIN resolver el incumplimiento
    /// (eso necesita el umbral de la empresa). Desviación positiva = tardía (después del fin de
    /// franja); negativa = adelantada (antes del inicio); cero = dentro de franja.
    ///
    /// <para>Dueña única de la regla "¿esto queda fuera de franja?": la usa el registro al guardar y
    /// la UI para desplegar la novedad ANTES de guardar. El espejo del front
    /// (<c>evaluar-aplicacion-hoy.funcion.ts</c>) debe mantenerse idéntico.</para>
    /// </summary>
    public static ProyeccionAplicacion ProyectarAplicacion(Franja franja, DateTime fechaAplicacion)
    {
        var fecha = fechaAplicacion.Date;
        int diasDesviacion;
        string estado;

        if (fecha < franja.FechaInicio)
        {
            diasDesviacion = -(franja.FechaInicio - fecha).Days;
            estado = EstadoAplicadoAdelantado;
        }
        else if (fecha > franja.FechaFin)
        {
            diasDesviacion = (fecha - franja.FechaFin).Days;
            estado = EstadoAplicadoTardio;
        }
        else
        {
            diasDesviacion = 0;
            estado = EstadoAplicado;
        }

        return new ProyeccionAplicacion(estado, diasDesviacion, RequiereMotivo: diasDesviacion != 0);
    }

    /// <summary>
    /// Estado + desviación de una aplicación confirmada frente a la franja del ítem.
    /// Desviación positiva = tardía (después del fin de franja); negativa = adelantada (antes del
    /// inicio); cero = dentro de franja. Incumplido ("rojo") solo aplica a tardanza que alcanza el
    /// umbral configurado por empresa/país.
    /// </summary>
    public static ResultadoAplicacion CalcularEstadoAplicacion(
        Franja franja, DateTime fechaAplicacion, int diasUmbralIncumplido)
    {
        var p = ProyectarAplicacion(franja, fechaAplicacion);
        return new ResultadoAplicacion(
            p.Estado, p.DiasDesviacion,
            Incumplido: p.DiasDesviacion >= diasUmbralIncumplido,
            RequiereMotivo: p.RequiereMotivo);
    }

    /// <summary>No aplicado: siempre exige motivo, nunca se marca incumplido por desviación (no hay fecha de aplicación).</summary>
    public static ResultadoAplicacion CalcularEstadoNoAplicado()
        => new(EstadoNoAplicado, DiasDesviacion: 0, Incumplido: false, RequiereMotivo: true);
}
