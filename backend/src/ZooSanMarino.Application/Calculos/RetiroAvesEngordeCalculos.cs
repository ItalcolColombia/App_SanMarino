// src/ZooSanMarino.Application/Calculos/RetiroAvesEngordeCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO (sin EF ni estado) del descuento de aves de un lote de pollo engorde por las bajas
/// del seguimiento diario (mortalidad + selección + error de sexaje).
/// <para>
/// <b>Regla central:</b> a qué bucket se descuenta NO lo decide el país ni la empresa, lo decide el
/// DATO del lote. Un lote cuyo maestro solo tiene aves <c>Mixtas</c> descuenta de mixtas — en Panamá,
/// en Ecuador o donde sea. Un lote con sexos conserva el comportamiento por sexo. Así, la plantilla
/// mixta de carga masiva (que manda todo el total en las columnas "H") descuenta correctamente sin
/// que el importador tenga que saber nada del lote.
/// </para>
/// <para>
/// Todas las operaciones hacen <b>clamp a 0</b>: un día histórico que reporte más bajas de las que el
/// maestro tiene NO bloquea la carga (es un dato del pasado), deja el maestro en 0 y se marca
/// <see cref="ResultadoRetiro.Insuficiente"/> para que el llamador lo registre en la observación.
/// </para>
/// </summary>
public static class RetiroAvesEngordeCalculos
{
    /// <summary>Aves disponibles del lote (<c>lote_ave_engorde.hembras_l / machos_l / mixtas</c>).</summary>
    public readonly record struct MaestroAves(int Hembras, int Machos, int Mixtas)
    {
        public int Total => Hembras + Machos + Mixtas;
    }

    /// <summary>Aves movidas en una operación, ya repartidas por bucket. Siempre ≥ 0.</summary>
    public readonly record struct RetiroAves(int Hembras, int Machos, int Mixtas)
    {
        public static readonly RetiroAves Cero = new(0, 0, 0);
        public int Total => Hembras + Machos + Mixtas;
        public bool EsVacio => Total == 0;
    }

    /// <summary>
    /// Maestro resultante + lo que efectivamente se descontó y/o devolvió. Un mismo delta puede
    /// descontar de un bucket y devolver a otro (edición que baja machos y sube hembras).
    /// </summary>
    public readonly record struct ResultadoRetiro(
        MaestroAves Maestro, RetiroAves Descontado, RetiroAves Devuelto, bool Insuficiente)
    {
        public bool SinEfecto => Descontado.EsVacio && Devuelto.EsVacio;
    }

    /// <summary>
    /// El lote se maneja como MIXTO cuando toda su población está en <c>mixtas</c>. Fail-safe: si
    /// tiene aves por sexo (aunque además tenga mixtas), gana el camino por sexo — el de hoy.
    /// </summary>
    public static bool EsLoteMixto(MaestroAves maestro) =>
        maestro.Mixtas > 0 && maestro.Hembras == 0 && maestro.Machos == 0;

    /// <summary>
    /// Reparte las bajas de un día entre los buckets del maestro. En un lote mixto, hembras y machos
    /// se suman en un único total mixto (la plantilla mixta manda todo en "H", pero un archivo por
    /// sexo cargado sobre un lote mixto también termina bien).
    /// </summary>
    public static RetiroAves Repartir(MaestroAves maestro, int bajasHembras, int bajasMachos)
    {
        var h = Math.Max(0, bajasHembras);
        var m = Math.Max(0, bajasMachos);
        return EsLoteMixto(maestro) ? new RetiroAves(0, 0, h + m) : new RetiroAves(h, m, 0);
    }

    /// <summary>
    /// Aplica un DELTA de bajas al maestro. Positivo descuenta, negativo devuelve.
    /// <list type="bullet">
    /// <item>Alta de un seguimiento → delta = bajas del día.</item>
    /// <item>Edición → delta = bajas nuevas − bajas viejas (puede ser negativo).</item>
    /// <item>Borrado → delta = −bajas del registro.</item>
    /// </list>
    /// En un lote mixto los dos sexos se netean en un solo movimiento sobre <c>mixtas</c>.
    /// </summary>
    public static ResultadoRetiro AplicarDelta(MaestroAves maestro, int deltaBajasHembras, int deltaBajasMachos)
    {
        if (EsLoteMixto(maestro))
        {
            var neto = deltaBajasHembras + deltaBajasMachos;
            if (neto == 0) return new(maestro, RetiroAves.Cero, RetiroAves.Cero, false);
            if (neto > 0)
            {
                var efectivo = Math.Min(maestro.Mixtas, neto);
                return new(
                    maestro with { Mixtas = maestro.Mixtas - efectivo },
                    new RetiroAves(0, 0, efectivo), RetiroAves.Cero,
                    Insuficiente: efectivo < neto);
            }
            var devuelto = -neto;
            return new(
                maestro with { Mixtas = maestro.Mixtas + devuelto },
                RetiroAves.Cero, new RetiroAves(0, 0, devuelto), false);
        }

        var (hMaestro, hDesc, hDev, hFalta) = AplicarPorBucket(maestro.Hembras, deltaBajasHembras);
        var (mMaestro, mDesc, mDev, mFalta) = AplicarPorBucket(maestro.Machos, deltaBajasMachos);

        return new(
            maestro with { Hembras = hMaestro, Machos = mMaestro },
            new RetiroAves(hDesc, mDesc, 0),
            new RetiroAves(hDev, mDev, 0),
            Insuficiente: hFalta || mFalta);
    }

    /// <summary>Un bucket contra su delta: descuenta hasta agotar (clamp a 0) o devuelve.</summary>
    private static (int Maestro, int Descontado, int Devuelto, bool Insuficiente) AplicarPorBucket(int disponible, int delta)
    {
        if (delta == 0) return (disponible, 0, 0, false);
        if (delta > 0)
        {
            var efectivo = Math.Min(disponible, delta);
            return (disponible - efectivo, efectivo, 0, efectivo < delta);
        }
        var devuelto = -delta;
        return (disponible + devuelto, 0, devuelto, false);
    }

    /// <summary>Bajas de un día por sexo: mortalidad + selección + error de sexaje (negativos = 0).</summary>
    public static (int Hembras, int Machos) BajasDelDia(
        int mortalidadHembras, int selHembras, int errorSexajeHembras,
        int mortalidadMachos, int selMachos, int errorSexajeMachos) =>
        (Math.Max(0, mortalidadHembras) + Math.Max(0, selHembras) + Math.Max(0, errorSexajeHembras),
         Math.Max(0, mortalidadMachos) + Math.Max(0, selMachos) + Math.Max(0, errorSexajeMachos));
}
