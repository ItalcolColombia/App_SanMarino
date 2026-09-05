// src/ZooSanMarino.Application/Calculos/SeguimientoDiarioProduccionCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// ESPECIFICACIÓN EJECUTABLE de <c>fn_seguimiento_diario_produccion</c> (BD): la fn SQL es la
/// dueña de los números de la grilla diaria de producción y esta clase es su contrato en C#
/// (regla del repo «una sola fórmula por número»: si un número se calcula en SQL y en C#, uno
/// es el dueño y el otro es el test). Los tests xUnit fijan los números testigo reales
/// (lote 130: 9.495→9.039 H / 929→902 M) que la fn debe reproducir.
///
/// Decisiones espejadas (plan seguimiento_produccion_fn_canonica_plan.md, D3/D4):
///  • saldo = GREATEST(0, base − Σ(mort+sel+ERR) − Σ mov_out + Σ mov_in) — CON error de sexaje
///    (semántica de los escritores incrementales y de la carga masiva).
///  • mov_out = movimiento_aves Completado con el lote como ORIGEN (cualquier tipo);
///    mov_in = tipo Traslado con el lote como DESTINO; SOLO movimientos con fecha >=
///    fecha_inicio_produccion (los previos son del levante y ya viven en las aves iniciales).
///  • Universo = días con seguimiento (dedup «gana el timestamp más temprano») ∪ días con
///    movimientos (filas movimiento-only, seg_id NULL).
///  • Semana de vida CRUDA ((fecha − ref)/7)+1, división entera, sin piso 26 ni corte 25.
///  • % postura hen-day diario = huevo_tot / aves_h_inicio_dia * 100 (0 si no hay hembras).
/// Sin EF ni estado: funciones puras.
/// </summary>
public static class SeguimientoDiarioProduccionCalculos
{
    /// <summary>Registro diario crudo (ya deduplicado) que entra a la serie.</summary>
    public sealed record DiaSeguimiento(
        DateOnly Fecha,
        long? SegId,
        int MortH, int MortM,
        int SelH, int SelM,
        int ErrH, int ErrM,
        int HuevoTot, int HuevoInc);

    /// <summary>Movimientos de aves agregados de UN día (ya filtrados por fase producción).</summary>
    public sealed record MovimientosDia(DateOnly Fecha, int OutH, int OutM, int InH, int InM);

    /// <summary>Fila derivada de la serie (espejo de las columnas calculadas de la fn).</summary>
    public sealed record FilaDerivada(
        DateOnly Fecha,
        long? SegId,
        int? AvesHInicioDia, int? AvesMInicioDia,
        int? SaldoAvesH, int? SaldoAvesM,
        long HuevoTotAcum, long HuevoIncAcum,
        double? PctPosturaDia);

    /// <summary>
    /// Dedup por día calendario: el registro con el timestamp MÁS TEMPRANO gana el día
    /// (≙ <c>DISTINCT ON (dia) ... ORDER BY dia, ts</c> de la fn y de las fns semanales).
    /// El día calendario lo resuelve el llamador (en BD es el día America/Bogota del ts).
    /// </summary>
    public static IReadOnlyList<T> DedupPorDia<T>(IEnumerable<(DateOnly Dia, DateTime Ts, T Fila)> filas)
        => filas
            .GroupBy(f => f.Dia)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(f => f.Ts).First().Fila)
            .ToList();

    /// <summary>
    /// Registro crudo del día — subconjunto de columnas de <c>seguimiento_diario_produccion</c>
    /// relevante para <see cref="AgruparPorDia"/>. Espejo de las columnas <c>c_*</c> de
    /// <c>seg_dias_agrupado</c> en <c>fn_seguimiento_diario_produccion.sql</c> (v3).
    /// </summary>
    public sealed record RegistroCrudo(
        long? SegId,
        int MortH, int MortM, int SelH, int SelM, int ErrH, int ErrM,
        double ConsKgH, double ConsKgM,
        int HuevoTot, int HuevoInc,
        decimal? PesoH, decimal? PesoM,
        decimal? Uniformidad, decimal? CoeficienteVariacion,
        string? TipoAlimento, string? Observaciones,
        bool EsTraslado);

    /// <summary>
    /// Agrupa por día calendario cuando la empresa tiene <c>permite_multiples_seguimientos_diarios</c>
    /// ON — espejo de <c>seg_dias_agrupado</c> (fn v3). Con UN solo registro el día, cada regla de
    /// abajo devuelve exactamente el valor de esa fila (mismo resultado que <see cref="DedupPorDia"/>).
    ///
    /// Reglas (plan seguimiento_produccion_multiples_registros_dia, §3):
    ///  • Aditivos (mortalidad, selección, error de sexaje, consumo, huevos) → SUMA.
    ///  • Peso promedio (ave) → PROMEDIO simple (equivalente a ponderar por aves vivas, que es un
    ///    valor de DÍA constante entre los registros del mismo día).
    ///  • Uniformidad, CV%, observaciones, tipo de alimento → gana el ÚLTIMO registro del día.
    ///  • es_traslado → TRUE si CUALQUIER registro del día lo fue.
    ///  • seg_id → el primero no nulo (mínimo), para no tapar un registro real con un stub.
    /// </summary>
    public static IReadOnlyList<(DateOnly Dia, RegistroCrudo Fila)> AgruparPorDia(
        IEnumerable<(DateOnly Dia, DateTime Ts, RegistroCrudo Fila)> filas)
        => filas
            .GroupBy(f => f.Dia)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordenadas = g.OrderBy(f => f.Ts).ToList();
                var ultima = ordenadas[^1].Fila;
                var agregada = new RegistroCrudo(
                    SegId: ordenadas.Select(f => f.Fila.SegId).Where(id => id.HasValue).DefaultIfEmpty().Min(),
                    MortH: ordenadas.Sum(f => f.Fila.MortH),
                    MortM: ordenadas.Sum(f => f.Fila.MortM),
                    SelH: ordenadas.Sum(f => f.Fila.SelH),
                    SelM: ordenadas.Sum(f => f.Fila.SelM),
                    ErrH: ordenadas.Sum(f => f.Fila.ErrH),
                    ErrM: ordenadas.Sum(f => f.Fila.ErrM),
                    ConsKgH: ordenadas.Sum(f => f.Fila.ConsKgH),
                    ConsKgM: ordenadas.Sum(f => f.Fila.ConsKgM),
                    HuevoTot: ordenadas.Sum(f => f.Fila.HuevoTot),
                    HuevoInc: ordenadas.Sum(f => f.Fila.HuevoInc),
                    PesoH: PromedioONulo(ordenadas.Select(f => f.Fila.PesoH)),
                    PesoM: PromedioONulo(ordenadas.Select(f => f.Fila.PesoM)),
                    Uniformidad: ultima.Uniformidad,
                    CoeficienteVariacion: ultima.CoeficienteVariacion,
                    TipoAlimento: ultima.TipoAlimento,
                    Observaciones: ultima.Observaciones,
                    EsTraslado: ordenadas.Any(f => f.Fila.EsTraslado));
                return (g.Key, agregada);
            })
            .ToList();

    /// <summary>AVG ignorando nulos, igual que SQL — null si ningún valor de la serie lo trae.</summary>
    private static decimal? PromedioONulo(IEnumerable<decimal?> valores)
    {
        var noNulos = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return noNulos.Count == 0 ? null : noNulos.Average();
    }

    /// <summary>Edad en días desde la fecha de referencia, con piso 0 (≙ GREATEST(0, fecha − ref)).</summary>
    public static int EdadDias(DateOnly fecha, DateOnly refDate)
        => Math.Max(0, fecha.DayNumber - refDate.DayNumber);

    /// <summary>
    /// Semana de vida CRUDA: ((fecha − ref) / 7) + 1 con división ENTERA (mismo criterio que
    /// fn_indicadores/fn_clasificacion/fn_resumen y que HuevosLevanteCalculos.SemanaVida).
    /// Sin piso 26 ni corte 25: eso es del consumidor.
    /// </summary>
    public static int SemanaVida(DateOnly fecha, DateOnly refDate)
        => ((fecha.DayNumber - refDate.DayNumber) / 7) + 1;

    /// <summary>
    /// Calcula la serie derivada día a día sobre el universo YA ordenado y deduplicado
    /// (seguimientos ∪ días solo-movimiento). <paramref name="baseH"/>/<paramref name="baseM"/>
    /// = COALESCE(lpp.aves_h_inicial, lpp.hembras_iniciales_prod, 0); NULL = rama legacy sin
    /// LPP ⇒ saldos NULL (nunca 0: en SQL, GREATEST ignora NULLs y por eso la fn usa CASE).
    /// </summary>
    public static IReadOnlyList<FilaDerivada> CalcularSerie(
        int? baseH,
        int? baseM,
        IReadOnlyList<DiaSeguimiento?> dias,
        IReadOnlyDictionary<DateOnly, MovimientosDia> movimientosPorDia,
        IReadOnlyList<DateOnly> fechasUniverso)
    {
        if (dias.Count != fechasUniverso.Count)
            throw new ArgumentException("dias y fechasUniverso deben tener la misma longitud (universo alineado).");

        var resultado = new List<FilaDerivada>(fechasUniverso.Count);
        long bajasAcumH = 0, bajasAcumM = 0;
        long outAcumH = 0, outAcumM = 0, inAcumH = 0, inAcumM = 0;
        long huevoTotAcum = 0, huevoIncAcum = 0;

        for (var i = 0; i < fechasUniverso.Count; i++)
        {
            var fecha = fechasUniverso[i];
            var dia = dias[i];
            movimientosPorDia.TryGetValue(fecha, out var mov);

            var inicioH = Saldo(baseH, bajasAcumH, outAcumH, inAcumH);
            var inicioM = Saldo(baseM, bajasAcumM, outAcumM, inAcumM);

            bajasAcumH += dia is null ? 0 : dia.MortH + dia.SelH + dia.ErrH;
            bajasAcumM += dia is null ? 0 : dia.MortM + dia.SelM + dia.ErrM;
            outAcumH += mov?.OutH ?? 0;
            outAcumM += mov?.OutM ?? 0;
            inAcumH += mov?.InH ?? 0;
            inAcumM += mov?.InM ?? 0;
            huevoTotAcum += dia?.HuevoTot ?? 0;
            huevoIncAcum += dia?.HuevoInc ?? 0;

            var saldoH = Saldo(baseH, bajasAcumH, outAcumH, inAcumH);
            var saldoM = Saldo(baseM, bajasAcumM, outAcumM, inAcumM);

            double? pct = baseH is null
                ? null
                : inicioH is > 0
                    ? 100.0 * (dia?.HuevoTot ?? 0) / inicioH.Value
                    : 0d;

            resultado.Add(new FilaDerivada(
                fecha, dia?.SegId,
                inicioH, inicioM, saldoH, saldoM,
                huevoTotAcum, huevoIncAcum, pct));
        }

        return resultado;
    }

    /// <summary>GREATEST(0, base − bajas − salidas + entradas); NULL si no hay base (rama legacy).</summary>
    private static int? Saldo(int? baseAves, long bajas, long salidas, long entradas)
        => baseAves is null ? null : (int)Math.Max(0L, baseAves.Value - bajas - salidas + entradas);
}
