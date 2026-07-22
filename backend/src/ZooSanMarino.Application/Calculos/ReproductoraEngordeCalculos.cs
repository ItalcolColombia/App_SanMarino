namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica pura del estado del lote reproductora aves de engorde.
/// Regla de negocio: el lote cierra SOLO cuando los días de recogida están CONFIRMADOS
/// (la confirmación es la que sincroniza cada registro hacia pollo engorde). Mientras haya
/// registros pendientes, el lote sigue "Vigente" — aunque se agoten las aves.
/// </summary>
public static class ReproductoraEngordeCalculos
{
    /// <summary>Días de recogida de datos del lote reproductora. Al confirmarlos todos, cierra.</summary>
    public const int DiasRecogidaReproductora = 7;

    /// <summary>
    /// Estado ("Cerrado"/"Vigente") + aves actuales del lote reproductora.
    /// <para>El cierre depende EXCLUSIVAMENTE de la cantidad de días CONFIRMADOS
    /// (<paramref name="numConfirmados"/>), no de los registrados. <paramref name="avesActuales"/>
    /// se conserva solo como saldo mostrado (no dispara el cierre).</para>
    /// </summary>
    public static (string Estado, int AvesActuales) CalcularEstado(
        int avesEncasetadas,
        int ventas,
        int mortalidad,
        int seleccion,
        int errorSexaje = 0,
        int numConfirmados = 0,
        int dias = DiasRecogidaReproductora)
    {
        var avesActuales = Math.Max(0, avesEncasetadas - mortalidad - seleccion - errorSexaje - ventas);
        var estado = numConfirmados >= dias ? "Cerrado" : "Vigente";
        return (estado, avesActuales);
    }
}
