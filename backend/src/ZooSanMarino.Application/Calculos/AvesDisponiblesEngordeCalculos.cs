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

    /// <summary>
    /// Aves disponibles por sexo de un lote pollo engorde. <b>Única fórmula</b> del número: la comparten
    /// el widget del seguimiento diario y la disponibilidad para venta (individual, por granja y Panamá).
    /// <para>
    /// <b>Por qué existe:</b> el número vivía duplicado en dos services. Cuando
    /// <see cref="BajasPendientesDeAplicar"/> corrigió el doble descuento, solo se aplicó al seguimiento y
    /// la venta quedó restando las bajas ya aplicadas al maestro ⇒ mostraba de menos y bloqueaba despachos
    /// que sí tenían aves (CAROLINA G4 lote 2603: 40 en el seguimiento, 33 en la venta; Sacachun 3A: 194 vs 0).
    /// Centralizarlo hace que la divergencia no pueda volver por olvido.
    /// </para>
    /// </summary>
    /// <param name="maestroHembras">Saldo del maestro <c>lote_ave_engorde.hembras_l</c>.</param>
    /// <param name="maestroMachos">Saldo del maestro <c>lote_ave_engorde.machos_l</c>.</param>
    /// <param name="mortCajaHembras">Mortalidad en caja del lote.</param>
    /// <param name="mortCajaMachos">Mortalidad en caja del lote.</param>
    /// <param name="sieteDiasCompletos">
    /// <c>true</c> cuando TODOS los lotes reproductora completaron sus 7 días confirmados: las aves ya
    /// «regresaron» al lote, así que no se restan las asignadas sino la mortalidad en caja de reproductora.
    /// </param>
    /// <param name="asignadasHembras">Aves asignadas a lotes reproductora (solo si NO están devueltas).</param>
    /// <param name="asignadasMachos">Aves asignadas a lotes reproductora (solo si NO están devueltas).</param>
    /// <param name="mortCajaReproHembras">Mortalidad en caja de los reproductora (solo si ya están devueltas).</param>
    /// <param name="mortCajaReproMachos">Mortalidad en caja de los reproductora (solo si ya están devueltas).</param>
    /// <param name="registradasHembras">Bajas del seguimiento: mortalidad + selección + error de sexaje.</param>
    /// <param name="registradasMachos">Bajas del seguimiento: mortalidad + selección + error de sexaje.</param>
    /// <param name="aplicadasHembras">Filas <c>BAJA_SEGUIMIENTO</c> no anuladas del histórico unificado.</param>
    /// <param name="aplicadasMachos">Filas <c>BAJA_SEGUIMIENTO</c> no anuladas del histórico unificado.</param>
    /// <param name="aplicadasMixtas">Filas <c>BAJA_SEGUIMIENTO</c> registradas en un solo bucket mixto.</param>
    /// <param name="reservadasHembras">Reservas de movimientos en estado <c>Pendiente</c> (venta/despacho/retiro).</param>
    /// <param name="reservadasMachos">Reservas de movimientos en estado <c>Pendiente</c> (venta/despacho/retiro).</param>
    public static (int Hembras, int Machos) DisponiblesPorSexo(
        int maestroHembras, int maestroMachos,
        int mortCajaHembras, int mortCajaMachos,
        bool sieteDiasCompletos,
        int asignadasHembras, int asignadasMachos,
        int mortCajaReproHembras, int mortCajaReproMachos,
        int registradasHembras, int registradasMachos,
        int aplicadasHembras, int aplicadasMachos, int aplicadasMixtas,
        int reservadasHembras, int reservadasMachos)
    {
        // Solo se resta lo que el maestro TODAVÍA no tiene descontado: el resto ya está reflejado en
        // maestroHembras/maestroMachos y restarlo otra vez lo contaría dos veces.
        var (bajasPendH, bajasPendM) = BajasPendientesDeAplicar(
            registradasHembras, registradasMachos,
            aplicadasHembras, aplicadasMachos, aplicadasMixtas);

        // Con las aves devueltas se resta la mortalidad en caja de los reproductora (sus bajas diarias
        // de los días 1-7 ya viajan en los registros de cruce del seguimiento); si no, se restan las
        // asignadas, que siguen viviendo en los lotes reproductora.
        var descuentoH = sieteDiasCompletos ? mortCajaReproHembras : asignadasHembras;
        var descuentoM = sieteDiasCompletos ? mortCajaReproMachos : asignadasMachos;

        var hembras = Math.Max(0, maestroHembras - mortCajaHembras - descuentoH - bajasPendH - reservadasHembras);
        var machos  = Math.Max(0, maestroMachos  - mortCajaMachos  - descuentoM - bajasPendM - reservadasMachos);

        return (hembras, machos);
    }
}
