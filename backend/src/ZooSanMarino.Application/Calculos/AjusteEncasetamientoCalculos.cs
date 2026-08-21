// src/ZooSanMarino.Application/Calculos/AjusteEncasetamientoCalculos.cs
using System.Globalization;
using static ZooSanMarino.Application.Calculos.RetiroAvesEngordeCalculos;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO (sin EF ni estado) del <b>ajuste de encasetamiento</b>: corregir, en un lote que YA
/// tiene seguimiento diario cargado, las aves con que arrancó.
///
/// <para>
/// <b>El problema que resuelve.</b> Un lote guarda el número de aves DOS veces con significados
/// distintos: el <i>inicial</i> (histórico del encasetamiento, no baja nunca) y el <i>saldo vivo</i>
/// (el maestro que las bajas del seguimiento y las ventas van descontando). Cuando el operario se
/// equivoca al crear el lote y quiere corregirlo semanas después, escribir el número nuevo sobre los
/// dos campos <b>pisa el saldo</b>: las bajas ya descontadas se pierden, y la serie diaria —que
/// calcula <c>inicial − Σ(bajas + ventas)</c>— vuelve a restarlas sobre una base equivocada.
/// </para>
///
/// <para>
/// <b>La regla.</b> El inicial se reemplaza, el saldo vivo se corre por el <b>DELTA</b>. Corregir de
/// 10.000 a 10.500 sube el saldo en 500 y conserva intactas todas las bajas aplicadas. Es el mismo
/// criterio con el que el trigger <c>trg_lotes_sync_lote_postura_levante</c> arregló el caso de
/// postura en ago-2026 y con el que <see cref="RetiroAvesEngordeCalculos.AplicarDelta"/> mueve las
/// bajas: en este repositorio el saldo de aves <b>nunca</b> se sobrescribe.
/// </para>
///
/// <para>
/// <b>Restar tiene gate.</b> Bajar el inicial por debajo de lo que el lote ya consumió dejaría días
/// de la serie en negativo. <see cref="Diagnosticar"/> simula la serie completa ANTES de escribir y
/// nombra el primer día que no cierra: mejor un 400 que dice qué día y cuántas aves faltan que un 200
/// que deja el lote descuadrado en silencio (mismo criterio que
/// <see cref="EncasetamientoRetroactivoCalculos"/>).
/// </para>
/// </summary>
public static class AjusteEncasetamientoCalculos
{
    /// <summary>
    /// Diferencia por bucket entre el inicial nuevo y el vigente. Positivo = se agregan aves al lote;
    /// negativo = se quitan.
    /// </summary>
    public readonly record struct Delta(int Hembras, int Machos, int Mixtas)
    {
        public static readonly Delta Cero = new(0, 0, 0);

        /// <summary>Neto de aves que entran (positivo) o salen (negativo) del lote.</summary>
        public int Total => Hembras + Machos + Mixtas;

        /// <summary>El ajuste no mueve ningún bucket ⇒ el llamador no debe escribir nada.</summary>
        public bool EsCero => Hembras == 0 && Machos == 0 && Mixtas == 0;
    }

    /// <summary>
    /// Un día de la serie del lote, tal como lo arma <c>fn_seguimiento_diario_engorde</c>:
    /// <c>perdidas_totales_dia</c> (mortalidad + selección + error de sexaje, los dos sexos) y las
    /// ventas <c>VENTA_AVES</c> del histórico unificado de esa fecha.
    /// </summary>
    /// <param name="Fecha">Día al que corresponde el movimiento.</param>
    /// <param name="Perdidas">Bajas del seguimiento de ese día (ya sumadas los dos sexos).</param>
    /// <param name="Ventas">Aves despachadas ese día (hembras + machos + mixtas).</param>
    public readonly record struct MovimientoDia(DateTime Fecha, int Perdidas, int Ventas)
    {
        public int Total => Math.Max(0, Perdidas) + Math.Max(0, Ventas);
    }

    /// <summary>
    /// Resultado de simular la serie con el inicial propuesto.
    /// <see cref="Compatible"/> es la única señal que decide si el ajuste se puede guardar.
    /// </summary>
    /// <param name="Compatible">Ningún día de la serie cierra en negativo con la base nueva.</param>
    /// <param name="PrimerDiaNegativo">Primer día que no alcanza (null si compatible).</param>
    /// <param name="FaltanAves">Aves que faltan en ese primer día (0 si compatible).</param>
    /// <param name="SaldoFinal">Saldo del lote al final de la serie, SIN clamp (puede ser negativo).</param>
    /// <param name="ConsumoTotal">Bajas + ventas acumuladas de toda la serie.</param>
    public readonly record struct Diagnostico(
        bool Compatible, DateTime? PrimerDiaNegativo, int FaltanAves, int SaldoFinal, int ConsumoTotal);

    // ─────────────────────────────────────────────────────────────────────────
    // Delta
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicial que se va a guardar, normalizado contra la forma del lote. En un lote <b>mixto</b>
    /// (toda su población en <c>mixtas</c>) los sexos digitados se colapsan al bucket mixto, para que
    /// un formulario que manda el total en "hembras" no invente un lote por sexo y deje las mixtas
    /// viejas colgando. Un lote por sexo se guarda tal cual.
    /// <para>
    /// La forma la decide el inicial VIGENTE, no el maestro: el maestro puede estar agotado (todo en
    /// 0) y ahí ya no distingue una cosa de la otra.
    /// </para>
    /// </summary>
    public static MaestroAves Normalizar(MaestroAves inicialVigente, MaestroAves digitado)
    {
        var h = Math.Max(0, digitado.Hembras);
        var m = Math.Max(0, digitado.Machos);
        var x = Math.Max(0, digitado.Mixtas);
        return EsLoteMixto(inicialVigente) ? new MaestroAves(0, 0, h + m + x) : new MaestroAves(h, m, x);
    }

    /// <summary>
    /// Delta por bucket entre el inicial vigente y el propuesto. El propuesto se normaliza primero
    /// (ver <see cref="Normalizar"/>), así que sobre un lote mixto el delta vive siempre en
    /// <c>mixtas</c> y nunca deja residuos en los buckets por sexo.
    /// </summary>
    public static Delta Calcular(MaestroAves inicialVigente, MaestroAves inicialPropuesto)
    {
        var nuevo = Normalizar(inicialVigente, inicialPropuesto);
        return new Delta(
            nuevo.Hembras - inicialVigente.Hembras,
            nuevo.Machos - inicialVigente.Machos,
            nuevo.Mixtas - inicialVigente.Mixtas);
    }

    /// <summary>Atajo legible para el llamador: nada que escribir.</summary>
    public static bool SinCambio(Delta delta) => delta.EsCero;

    /// <summary>
    /// Corre el saldo vivo por el delta, bucket a bucket, con <b>clamp a 0</b>: un delta negativo
    /// mayor que el saldo lo deja en 0 y no genera un maestro negativo. (El gate de
    /// <see cref="Diagnosticar"/> ya habrá rechazado ese caso antes de llegar acá; el clamp es la red
    /// de seguridad, no el camino esperado.)
    /// <para>
    /// <b>No pisa:</b> suma. Ésa es toda la diferencia con el comportamiento anterior y la razón por
    /// la que las bajas ya descontadas sobreviven al ajuste.
    /// </para>
    /// </summary>
    public static MaestroAves AplicarDelta(MaestroAves maestro, Delta delta) =>
        new(Math.Max(0, maestro.Hembras + delta.Hembras),
            Math.Max(0, maestro.Machos + delta.Machos),
            Math.Max(0, maestro.Mixtas + delta.Mixtas));

    // ─────────────────────────────────────────────────────────────────────────
    // Gate
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simula la serie diaria con el inicial propuesto y devuelve el primer día que quedaría en
    /// negativo. Es el espejo ejecutable de <c>fn_seguimiento_diario_engorde</c> §13
    /// (<c>saldo_aves = inicial − Σ(perdidas_totales_dia + ventas_dia)</c>) <b>sin</b> su
    /// <c>GREATEST(0, …)</c>: el clamp de la fn es de presentación y esconde justamente el sobregiro
    /// que hay que detectar.
    /// <para>
    /// La base arranca en <c>inicial − mortalidad en caja</c> con piso 0, igual que la rama
    /// <c>aves_iniciales</c> de la fn (v8).
    /// </para>
    /// </summary>
    /// <param name="inicialPropuesto">Total de aves con que quedaría encasetado el lote.</param>
    /// <param name="mortalidadCaja">Mortalidad en caja del lote (<c>mort_caja_h + mort_caja_m</c>).</param>
    /// <param name="serie">Días con movimiento. El orden lo impone el cálculo, no el llamador.</param>
    public static Diagnostico Diagnosticar(
        int inicialPropuesto, int mortalidadCaja, IEnumerable<MovimientoDia> serie)
    {
        var saldo = Math.Max(0, inicialPropuesto - Math.Max(0, mortalidadCaja));
        var consumo = 0;
        DateTime? primerNegativo = null;
        var faltan = 0;

        foreach (var dia in serie.OrderBy(d => d.Fecha))
        {
            consumo += dia.Total;
            saldo -= dia.Total;
            if (saldo < 0 && primerNegativo is null)
            {
                primerNegativo = dia.Fecha;
                faltan = -saldo;
            }
        }

        return new Diagnostico(
            Compatible: primerNegativo is null,
            PrimerDiaNegativo: primerNegativo,
            FaltanAves: faltan,
            SaldoFinal: saldo,
            ConsumoTotal: consumo);
    }

    /// <summary>
    /// Mensaje de rechazo para el usuario: qué pasa, desde qué día, cuántas aves faltan y qué puede
    /// hacer. Un 400 explicativo es preferible a un 200 que deja el lote inconsistente en silencio.
    /// </summary>
    public static string MensajeIncompatible(Diagnostico diagnostico, int inicialPropuesto)
    {
        // Sin separador de miles y con la fecha en dd/MM/yyyy invariante, igual que el resto de los
        // mensajes del repositorio. Un `N0` deja el número a merced de la cultura del servidor: en
        // ECS sale "20,407", que un lector hispanohablante puede leer como veinte-coma-cuatro.
        var fecha = diagnostico.PrimerDiaNegativo?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "-";
        return $"No se puede dejar el lote en {inicialPropuesto} aves: el {fecha} el seguimiento ya no "
             + $"alcanzaría por {diagnostico.FaltanAves} ave(s). El lote lleva {diagnostico.ConsumoTotal} "
             + "aves consumidas entre bajas y ventas, así que ése es el mínimo con el que puede quedar "
             + "encasetado. Corregí primero los registros de mortalidad o las ventas que sobren, y "
             + "volvé a intentar el ajuste.";
    }
}
