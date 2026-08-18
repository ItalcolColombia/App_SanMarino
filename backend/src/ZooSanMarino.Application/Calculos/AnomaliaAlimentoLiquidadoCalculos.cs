// Señalamiento de la anomalía R2: el alimento que queda en el galpón cuando se liquida un lote.
//
// La regla operativa la fijó el dueño del producto: al liquidar, el galpón tiene que quedar en CERO y
// el sobrante se TRASLADA fuera. Un lote que congeló su liquidación con saldo de alimento no es un
// caso a compensar con guardas — es una ANOMALÍA que el sistema tiene que SEÑALAR. Acá vive la lectura
// de esos kilos: cuánto quedó sin trasladar, cuánto de eso ya ni existe en el galpón, y qué se hace.
//
// Es puro y testeado a propósito: la tolerancia y el orden de severidad son decisiones de negocio, y
// si viven inline en una consulta nadie vuelve a mirarlas.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Veredicto de un lote liquidado con saldo de alimento, de menor a mayor severidad.</summary>
public enum EstadoAlimentoLiquidado
{
    /// <summary>
    /// El sobrante salió del galpón por traslado después del último seguimiento: se siguió el
    /// procedimiento. Informativo, no exige acción.
    /// </summary>
    Trasladado = 0,

    /// <summary>
    /// Quedaron kilos sin trasladar y el stock del galpón todavía los respalda: el sobrante sigue
    /// físicamente ahí. Se traslada, o lo toma el ciclo siguiente.
    /// </summary>
    PendienteEnGalpon = 1,

    /// <summary>
    /// La foto congelada reclama kilos que ya NO están en el galpón: se los consumió otro ciclo. Es el
    /// «fantasma contable» — el saldo del lote cerrado y el inventario cuentan cosas distintas.
    /// </summary>
    SinRespaldoFisico = 2,
}

public static class AnomaliaAlimentoLiquidadoCalculos
{
    /// <summary>
    /// Tolerancia en kg — la MISMA de <see cref="CuadreAlimentoEngordeCalculos.ToleranciaKg"/>: los dos
    /// números miran el mismo invariante y separarlos haría que un galpón cuadrara en una pantalla y no
    /// en la otra.
    /// </summary>
    public const decimal ToleranciaKg = CuadreAlimentoEngordeCalculos.ToleranciaKg;

    /// <summary>
    /// Kilos que la liquidación dejó en el galpón y que NUNCA salieron por un traslado.
    /// <para>
    /// <paramref name="salidasPostKg"/> son los traslados de salida posteriores al último día de
    /// seguimiento del lote — el mismo corte que usa <c>fn_cuadre_alimento_engorde</c> para
    /// <c>mov_post</c>: lo que se movió después del último seguimiento no cabe en la foto congelada.
    /// </para>
    /// Nunca es negativo: si salió MÁS de lo que decía la foto, el sobrante ya no existe.
    /// </summary>
    public static decimal KgSinTrasladar(decimal saldoCongeladoKg, decimal salidasPostKg)
        => Math.Max(0m, saldoCongeladoKg - salidasPostKg);

    /// <summary>
    /// De lo que quedó sin trasladar, los kilos que el stock del galpón ya no respalda. Son los que
    /// consumió otro ciclo: la foto los sigue reclamando y el inventario no los tiene.
    /// </summary>
    public static decimal KgSinRespaldo(decimal saldoCongeladoKg, decimal salidasPostKg, decimal stockGalponKg)
        => Math.Max(0m, KgSinTrasladar(saldoCongeladoKg, salidasPostKg) - Math.Max(0m, stockGalponKg));

    /// <summary>Clasifica un lote liquidado con saldo de alimento.</summary>
    public static EstadoAlimentoLiquidado Clasificar(
        decimal saldoCongeladoKg, decimal salidasPostKg, decimal stockGalponKg)
    {
        if (KgSinTrasladar(saldoCongeladoKg, salidasPostKg) <= ToleranciaKg)
            return EstadoAlimentoLiquidado.Trasladado;

        if (KgSinRespaldo(saldoCongeladoKg, salidasPostKg, stockGalponKg) > ToleranciaKg)
            return EstadoAlimentoLiquidado.SinRespaldoFisico;

        return EstadoAlimentoLiquidado.PendienteEnGalpon;
    }

    /// <summary>Explicación en una línea, en el idioma de la operación: qué pasó y qué hacer.</summary>
    public static string Describir(decimal saldoCongeladoKg, decimal salidasPostKg, decimal stockGalponKg)
    {
        var sinTrasladar = KgSinTrasladar(saldoCongeladoKg, salidasPostKg);
        var sinRespaldo  = KgSinRespaldo(saldoCongeladoKg, salidasPostKg, stockGalponKg);

        return Clasificar(saldoCongeladoKg, salidasPostKg, stockGalponKg) switch
        {
            EstadoAlimentoLiquidado.SinRespaldoFisico =>
                $"La liquidación dejó {Kg(sinTrasladar)} kg sin trasladar y el galpón solo tiene " +
                $"{Kg(stockGalponKg)} kg: {Kg(sinRespaldo)} kg ya los consumió otro ciclo. Revisar el " +
                "cuadre del galpón antes de cerrar costos.",

            EstadoAlimentoLiquidado.PendienteEnGalpon =>
                $"Quedaron {Kg(sinTrasladar)} kg en el galpón al liquidar. Trasladarlos fuera del " +
                "galpón, o dejar constancia de que los toma el ciclo siguiente.",

            _ => "El sobrante salió del galpón por traslado.",
        };
    }

    private static string Kg(decimal v) => v.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
}
