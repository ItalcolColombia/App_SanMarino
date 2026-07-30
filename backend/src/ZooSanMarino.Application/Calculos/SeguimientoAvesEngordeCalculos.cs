// Cálculo PURO del seguimiento diario de aves de engorde (Colombia).
// Extraído verbatim de SeguimientoAvesEngordeService para aislar la aritmética
// (fechas efectivas, saldo de alimento, cuadre de saldos) del acceso a datos EF.
// Sin dependencias de EF/_ctx/estado → testeable con xUnit (equivalencia de comportamiento).
//
// NOTA: YmdHistoricoEfectivo es específico de Colombia (extrae la fecha efectiva desde la
// referencia del evento); Ecuador usa FechaOperacion a secas — por eso NO vive en
// SaldoAlimentoEngordeCalculos (compartido multi-país), sino aquí.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

public static class SeguimientoAvesEngordeCalculos
{
    /// <summary>
    /// Fecha calendario (yyyy-MM-DD) para ordenar/agrupar movimientos del histórico, alineada con el front
    /// (tabs-principal-engorde: ymdHistoricoEfectivo).
    /// </summary>
    public static string? YmdHistoricoEfectivo(LoteRegistroHistoricoUnificado h)
    {
        var referencia = $"{h.Referencia ?? ""} {h.NumeroDocumento ?? ""}".Trim();
        var mSeg = Regex.Match(referencia, @"Seguimiento\s+aves\s+engorde\s+#\d+\s+(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase);
        if (mSeg.Success)
            return mSeg.Groups[1].Value;
        if (string.Equals(h.TipoEvento, "INV_CONSUMO", StringComparison.OrdinalIgnoreCase))
        {
            var mAny = Regex.Match(referencia, @"(\d{4}-\d{2}-\d{2})");
            if (mAny.Success)
                return mAny.Groups[1].Value;
        }
        return h.FechaOperacion.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatYmd(DateTime d) =>
        d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FormatKg(decimal kg)
        => kg.ToString("0.###", CultureInfo.InvariantCulture);

    public static long TsHistorico(LoteRegistroHistoricoUnificado h) =>
        h.CreatedAt.ToUnixTimeMilliseconds();

    public static long TsSeguimiento(SeguimientoDiarioAvesEngorde s)
    {
        var t = new DateTimeOffset(s.Fecha.Year, s.Fecha.Month, s.Fecha.Day, 12, 0, 0, TimeSpan.Zero);
        return t.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Stock (kg) antes del primer día de seguimiento: solo movimientos histórico con fecha efectiva &lt; primer seguimiento.
    /// Tras cada movimiento piso en 0 (misma regla que el front).
    /// ⚠️ FIX #12 (2026-05-28): si <paramref name="fechaEncaset"/> se proporciona, los movimientos
    /// anteriores se ignoran. Antes la apertura heredaba el inventario residual del lote previo que
    /// ocupó el galpón (ej. lote 75/2602: 132,277 kg → saldo día 1 137,557 vs esperado 5,280).
    /// El corte NO es la fecha de encaset sino la ventana previa de
    /// <see cref="VentanaAlimentoPrevioCalculos"/>: en engorde el preiniciador llega antes que los
    /// pollitos y cortar seco en el encaset dejaba fuera alimento propio del lote.
    /// ⚠️ FIX v11 (2026-07-29): <paramref name="lotesAjenos"/> descarta los movimientos del CICLO
    /// ANTERIOR del galpón. La ventana previa al encaset caía dentro de ese ciclo justo cuando se
    /// vacía su bodega y, como las devoluciones se excluyen pero los traslados de salida no, la
    /// apertura salía negativa (lote 98 Kilometro 22/G0036: −7.960 kg corridos en las 43 filas).
    /// </summary>
    public static decimal ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
        IReadOnlyList<LoteRegistroHistoricoUnificado> hist,
        DateTime firstSegDate,
        DateTime? fechaEncaset = null,
        int? diasAlimentoPrevio = null,
        IReadOnlySet<int>? lotesAjenos = null,
        DateTime? finCicloAnterior = null)
    {
        var firstYmd = FormatYmd(firstSegDate.Date);
        // ⭐ v12: la ventana no puede retroceder más allá del fin del ciclo anterior del galpón.
        var corte = SaldoAlimentoEngordeCalculos.ResolverCorteApertura(
            VentanaAlimentoPrevioCalculos.FechaCorte(fechaEncaset, diasAlimentoPrevio), finCicloAnterior);
        var encasetYmd = corte.HasValue ? FormatYmd(corte.Value) : null;
        var rows = new List<(string ymd, long ts, decimal delta)>();
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) >= 0)
                continue;
            if (encasetYmd is not null && string.Compare(ymd, encasetYmd, StringComparison.Ordinal) < 0)
                continue;
            if (SaldoAlimentoEngordeCalculos.EsDeCicloAjeno(h, lotesAjenos))
                continue;
            if (!SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(h, out var d, out _))
                continue;
            rows.Add((ymd, TsHistorico(h), d));
        }
        rows.Sort((a, b) =>
        {
            var c = string.Compare(a.ymd, b.ymd, StringComparison.Ordinal);
            if (c != 0) return c;
            return a.ts.CompareTo(b.ts);
        });
        decimal bal = 0;
        foreach (var r in rows)
            bal += r.delta;
        return bal;
    }

    private readonly record struct SaldoAlimentoEvent(string Ymd, int Ord, long Tie, long? SegId, decimal Delta);

    /// <summary>
    /// Reducción PURA del saldo de alimento (kg) por registro de seguimiento del lote.
    /// Misma lógica que el front (computeSaldoAlimentoKgPorSeguimiento): apertura de galpón +
    /// eventos de histórico (INV_INGRESO/TRASLADO), menos consumo del seguimiento, orden estable.
    /// El saldo puede quedar NEGATIVO cuando el consumo va por delante de las llegadas registradas:
    /// es información real de la operación, y recortarlo a 0 descuadraba el acumulado contra el
    /// inventario. Devuelve el saldo por Id de seguimiento y el saldo final acumulado
    /// (fallback para seguimientos sin evento propio). El llamador (EF) persiste el resultado.
    /// Requiere <paramref name="segs"/> no vacío.
    /// </summary>
    public static (IReadOnlyDictionary<long, decimal> SaldoPorSegId, decimal SaldoFinal) CalcularSaldoAlimentoPorSeguimiento(
        IReadOnlyList<LoteRegistroHistoricoUnificado> hist,
        IReadOnlyList<SeguimientoDiarioAvesEngorde> segs,
        DateTime? fechaEncaset,
        int? diasAlimentoPrevio = null,
        IReadOnlySet<int>? lotesAjenos = null,
        DateTime? finCicloAnterior = null)
    {
        var firstSegDate = segs.Min(s => s.Fecha.Date);
        var corte = VentanaAlimentoPrevioCalculos.FechaCorte(fechaEncaset, diasAlimentoPrevio);
        var encYmd = corte.HasValue ? FormatYmd(corte.Value) : null;
        var firstYmd = FormatYmd(firstSegDate);

        // ⭐ v11: `lotesAjenos` SOLO en la apertura. Desde el primer seguimiento el galpón es de
        // este lote y todo lo que entra es suyo, aunque el movimiento haya quedado etiquetado con
        // el id del ciclo anterior (ver SaldoAlimentoEngordeCalculos.EsDeCicloAjeno).
        var opening = ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, firstSegDate, fechaEncaset, diasAlimentoPrevio, lotesAjenos, finCicloAnterior);

        var events = new List<SaldoAlimentoEvent>(hist.Count + segs.Count);

        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) < 0)
                continue;
            if (encYmd is not null && string.Compare(ymd, encYmd, StringComparison.Ordinal) < 0)
                continue;
            if (!SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(h, out var delta, out var ord))
                continue;
            events.Add(new SaldoAlimentoEvent(ymd, ord, TsHistorico(h), null, delta));
        }

        foreach (var s in segs)
        {
            var ymd = FormatYmd(s.Fecha.Date);
            var ch = s.ConsumoKgHembras ?? 0;
            var cm = s.ConsumoKgMachos ?? 0;
            var cons = ch + cm;
            events.Add(new SaldoAlimentoEvent(ymd, 3, TsSeguimiento(s), s.Id, -cons));
        }

        events.Sort((a, b) =>
        {
            var c = string.Compare(a.Ymd, b.Ymd, StringComparison.Ordinal);
            if (c != 0) return c;
            if (a.Ord != b.Ord) return a.Ord.CompareTo(b.Ord);
            if (a.Tie != b.Tie) return a.Tie.CompareTo(b.Tie);
            return (a.SegId ?? 0L).CompareTo(b.SegId ?? 0L);
        });

        var saldoPorSegId = new Dictionary<long, decimal>();
        decimal bal = opening;
        foreach (var e in events)
        {
            // SIN piso en 0. El piso evitaba mostrar negativos, pero cada recorte regalaba alimento que
            // no existe y el acumulado terminaba por encima del inventario: en el lote testigo el saldo
            // tocaba −10.634,13 kg (las llegadas están fechadas después del consumo que las gasta) y el
            // reporte cerraba en 12.869,46 contra 2.235,33 reales. Un saldo negativo no es un error de
            // cálculo: dice que el alimento se consumió antes de que su llegada quedara registrada.
            bal += e.Delta;
            if (e.SegId.HasValue)
                saldoPorSegId[e.SegId.Value] = bal;
        }

        return (saldoPorSegId, bal);
    }

    /// <summary>
    /// Busca el candidato más probable en el histórico completo para coincidir con el monto dado.
    /// Prioriza: mismo documento + mismo monto; si no, mismo monto más cercano en fecha.
    /// </summary>
    public static LoteRegistroHistoricoUnificado? BuscarCandidatoHistorico(
        IEnumerable<LoteRegistroHistoricoUnificado> hist,
        HashSet<long> idsUsados,
        string tipoEvento,
        decimal montoKg,
        string? documento)
    {
        var candidatos = hist
            .Where(h => h.TipoEvento == tipoEvento && !idsUsados.Contains(h.Id))
            .ToList();

        // Intento 1: mismo monto + mismo documento
        if (!string.IsNullOrWhiteSpace(documento))
        {
            var byDoc = candidatos.FirstOrDefault(h =>
                Math.Abs((h.CantidadKg ?? 0m) - montoKg) < 0.001m &&
                (string.Equals(h.NumeroDocumento?.Trim(), documento, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(h.Referencia?.Trim(), documento, StringComparison.OrdinalIgnoreCase)));
            if (byDoc != null) return byDoc;
        }

        // Intento 2: solo mismo monto
        return candidatos.FirstOrDefault(h => Math.Abs((h.CantidadKg ?? 0m) - montoKg) < 0.001m);
    }

    public static bool MetadataYaTieneCamposKardex(JsonDocument? metadata)
    {
        if (metadata is null) return false;
        var root = metadata.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return false;

        static bool HasNonEmpty(JsonElement obj, string key)
        {
            if (!obj.TryGetProperty(key, out var v)) return false;
            return v.ValueKind switch
            {
                JsonValueKind.String => !string.IsNullOrWhiteSpace(v.GetString()),
                JsonValueKind.Number => v.GetDecimal() != 0m,
                JsonValueKind.True => true,
                _ => false
            };
        }

        return
            HasNonEmpty(root, "ingresoAlimento") ||
            HasNonEmpty(root, "traslado") ||
            HasNonEmpty(root, "documento") ||
            HasNonEmpty(root, "despachoHembras") ||
            HasNonEmpty(root, "despachoMachos");
    }
}
