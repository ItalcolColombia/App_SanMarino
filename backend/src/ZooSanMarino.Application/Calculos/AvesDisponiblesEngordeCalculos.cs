// src/ZooSanMarino.Application/Calculos/AvesDisponiblesEngordeCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO (sin EF ni estado) de las bajas de seguimiento que todavía NO se descontaron del
/// maestro de aves de un lote pollo engorde.
/// <para>
/// <b>Por qué existe:</b> «Aves disponibles» parte de <c>lote_ave_engorde.hembras_l / machos_l</c> y le
/// resta la mortalidad, selección y error de sexaje acumulados del seguimiento. Esa fórmula era correcta
/// cuando el maestro solo bajaba por ventas; desde que <c>RetiroAvesEngordeAplicador</c> descuenta también
/// las bajas del seguimiento, restarlas de nuevo las cuenta DOS VECES y el widget queda por debajo del
/// saldo que muestra la tabla diaria (que calcula <c>aves_encasetadas − bajas</c> y sí es correcto).
/// </para>
/// <para>
/// <b>Regla:</b> solo se resta lo que el maestro todavía no tiene descontado, medido por las filas
/// <c>BAJA_SEGUIMIENTO</c> del histórico unificado — la misma fuente que usa
/// <c>CorreccionAvesDisponiblesEngordeService</c>. Los lotes anteriores al descuento automático no tienen
/// esas filas, así que su pendiente es el total y **conservan exactamente la fórmula previa**
/// (retrocompatible por construcción).
/// </para>
/// </summary>
public static class AvesDisponiblesEngordeCalculos
{
    /// <summary>
    /// Bajas del seguimiento que aún no se reflejaron en el maestro, por sexo.
    /// <para>
    /// <paramref name="aplicadasMixtas"/> corresponde a los lotes que <c>RetiroAvesEngordeCalculos</c>
    /// trató como MIXTOS: ahí el descuento se hizo sobre <c>mixtas</c> en un solo bucket, así que se
    /// consume contra el pendiente de hembras y luego contra el de machos (mismo orden con el que
    /// <c>Repartir</c> sumó <c>h + m</c>). Todo con clamp a 0: un maestro con más descontado que lo
    /// registrado no genera pendientes negativos que inflarían las disponibles.
    /// </para>
    /// </summary>
    public static (int Hembras, int Machos) BajasPendientesDeAplicar(
        int registradasHembras, int registradasMachos,
        int aplicadasHembras, int aplicadasMachos, int aplicadasMixtas)
    {
        var pendH = Math.Max(0, Math.Max(0, registradasHembras) - Math.Max(0, aplicadasHembras));
        var pendM = Math.Max(0, Math.Max(0, registradasMachos) - Math.Max(0, aplicadasMachos));

        var restante = Math.Max(0, aplicadasMixtas);
        if (restante > 0)
        {
            var consumeH = Math.Min(pendH, restante);
            pendH -= consumeH;
            restante -= consumeH;

            pendM -= Math.Min(pendM, restante);
        }

        return (pendH, pendM);
    }
}
