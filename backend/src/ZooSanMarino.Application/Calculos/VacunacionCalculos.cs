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
    /// Estado + desviación de una aplicación confirmada frente a la franja del ítem.
    /// Desviación positiva = tardía (después del fin de franja); negativa = adelantada (antes del
    /// inicio); cero = dentro de franja. Incumplido ("rojo") solo aplica a tardanza que alcanza el
    /// umbral configurado por empresa/país.
    /// </summary>
    public static ResultadoAplicacion CalcularEstadoAplicacion(
        Franja franja, DateTime fechaAplicacion, int diasUmbralIncumplido)
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

        var incumplido = diasDesviacion >= diasUmbralIncumplido;
        var requiereMotivo = diasDesviacion != 0;

        return new ResultadoAplicacion(estado, diasDesviacion, incumplido, requiereMotivo);
    }

    /// <summary>No aplicado: siempre exige motivo, nunca se marca incumplido por desviación (no hay fecha de aplicación).</summary>
    public static ResultadoAplicacion CalcularEstadoNoAplicado()
        => new(EstadoNoAplicado, DiasDesviacion: 0, Incumplido: false, RequiereMotivo: true);
}
