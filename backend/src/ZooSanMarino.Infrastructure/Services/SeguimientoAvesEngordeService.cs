// Seguimiento Diario Aves de Engorde: persiste en tabla seguimiento_diario_aves_engorde (FK a lote_ave_engorde).
// Filtros del módulo muestran lotes de lote_ave_engorde. DTO mantiene LoteId = lote_ave_engorde_id para el front.
//
// Inventario nuevo (inventario-gestion / item_inventario_ecuador): este módulo es el único que aplica consumo
// y devolución sobre el inventario nuevo. El módulo Seguimiento diario postura (ProduccionService) no usa
// inventario-gestion; los dos módulos de inventario están divididos (postura → su inventario; pollo engorde → inventario-gestion).
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoAvesEngordeService : ISeguimientoAvesEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly IInventarioGestionService? _inventarioGestionService;

    public SeguimientoAvesEngordeService(
        ZooSanMarinoContext ctx,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService,
        IInventarioGestionService? inventarioGestionService = null)
    {
        _ctx = ctx;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
        _inventarioGestionService = inventarioGestionService;
    }

    private static readonly string[] _docMetadataKeys =
        ["documento", "documentoAlimento", "nroDocumento", "numeroDocumento"];

    /// <summary>
    /// Limpia campos de documento en la metadata que fueron contaminados con la referencia
    /// "devolución por eliminación" generada al borrar un seguimiento anterior en la misma fecha.
    /// Modifica la entidad en memoria; no persiste cambios.
    /// </summary>
    private static void SanitizeContaminatedDocumentMetadata(SeguimientoDiarioAvesEngorde s)
    {
        if (s.Metadata is null) return;
        try
        {
            var root = s.Metadata.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            var needsClean = false;
            foreach (var key in _docMetadataKeys)
            {
                if (root.TryGetProperty(key, out var v)
                    && v.ValueKind == JsonValueKind.String)
                {
                    var text = v.GetString() ?? "";
                    if (text.Contains("devolución por eliminación", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("devolucion por eliminacion", StringComparison.OrdinalIgnoreCase))
                    {
                        needsClean = true;
                        break;
                    }
                }
            }
            if (!needsClean) return;

            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())
                       ?? new Dictionary<string, JsonElement>();
            foreach (var key in _docMetadataKeys)
            {
                if (dict.TryGetValue(key, out var v)
                    && v.ValueKind == JsonValueKind.String)
                {
                    var text = v.GetString() ?? "";
                    if (text.Contains("devolución por eliminación", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("devolucion por eliminacion", StringComparison.OrdinalIgnoreCase))
                        dict.Remove(key);
                }
            }
            s.Metadata = JsonDocument.Parse(JsonSerializer.Serialize(dict));
        }
        catch { /* metadata malformado: no modificar */ }
    }

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioAvesEngorde e)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)e.Id,
            LoteId: e.LoteAveEngordeId,
            LotePosturaLevanteId: null,
            FechaRegistro: e.Fecha,
            MortalidadHembras: e.MortalidadHembras ?? 0,
            MortalidadMachos: e.MortalidadMachos ?? 0,
            SelH: e.SelH ?? 0,
            SelM: e.SelM ?? 0,
            ErrorSexajeHembras: e.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: e.ErrorSexajeMachos ?? 0,
            ConsumoKgHembras: (double)(e.ConsumoKgHembras ?? 0),
            TipoAlimento: e.TipoAlimento ?? "",
            Observaciones: e.Observaciones,
            KcalAlH: e.KcalAlH,
            ProtAlH: e.ProtAlH,
            KcalAveH: e.KcalAveH,
            ProtAveH: e.ProtAveH,
            Ciclo: e.Ciclo ?? "Normal",
            ConsumoKgMachos: e.ConsumoKgMachos.HasValue ? (double)e.ConsumoKgMachos.Value : null,
            PesoPromH: e.PesoPromHembras,
            PesoPromM: e.PesoPromMachos,
            UniformidadH: e.UniformidadHembras,
            UniformidadM: e.UniformidadMachos,
            CvH: e.CvHembras,
            CvM: e.CvMachos,
            Metadata: e.Metadata,
            ItemsAdicionales: e.ItemsAdicionales,
            ConsumoAguaDiario: e.ConsumoAguaDiario,
            ConsumoAguaPh: e.ConsumoAguaPh,
            ConsumoAguaOrp: e.ConsumoAguaOrp,
            ConsumoAguaTemperatura: e.ConsumoAguaTemperatura,
            CreatedByUserId: e.CreatedByUserId,
            SaldoAlimentoKg: e.SaldoAlimentoKg.HasValue ? (double)e.SaldoAlimentoKg.Value : null,
            HistoricoConsumoAlimento: e.HistoricoConsumoAlimento
        );
    }

    public async Task<SeguimientoAvesEngordePorLoteResponseDto> GetByLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            return new SeguimientoAvesEngordePorLoteResponseDto(
                Array.Empty<SeguimientoLoteLevanteDto>(),
                Array.Empty<LoteRegistroHistoricoUnificadoDto>());

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        foreach (var s in list)
            SanitizeContaminatedDocumentMetadata(s);
        var seguimientos = list.Select(MapToDto).ToList();

        var historico = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<LoteRegistroHistoricoUnificadoDto>> GetHistoricoUnificadoPorLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists) return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        return await QueryHistoricoUnificadoDtosAsync(loteId, companyId);
    }

    public async Task<LiquidacionLoteEngordeResumenDto?> GetLiquidacionResumenAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new
            {
                l.LoteAveEngordeId,
                l.LoteNombre,
                l.EstadoOperativoLote,
                l.HembrasL,
                l.MachosL,
                l.Mixtas,
                l.AvesEncasetadas
            })
            .SingleOrDefaultAsync();
        if (lote is null) return null;

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var saldo = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderByDescending(s => s.Fecha)
            .Select(s => s.SaldoAlimentoKg)
            .FirstOrDefaultAsync();

        var ventas = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h => h.LoteAveEngordeId == loteId && h.CompanyId == companyId && !h.Anulado && h.TipoEvento == "VENTA_AVES")
            .ToListAsync();

        var vh = ventas.Sum(v => v.CantidadHembras ?? 0);
        var vm = ventas.Sum(v => v.CantidadMachos ?? 0);
        var vx = ventas.Sum(v => v.CantidadMixtas ?? 0);

        // Encaset / inicio real: mismo criterio que historial_lote_pollo_engorde (Inicio al crear el lote).
        var ini = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId &&
                h.LoteAveEngordeId == loteId &&
                h.TipoLote == "LoteAveEngorde" &&
                h.TipoRegistro == "Inicio")
            .OrderBy(h => h.Id)
            .FirstOrDefaultAsync();

        int hInicio;
        int mInicio;
        int xInicio;
        if (ini != null)
        {
            hInicio = ini.AvesHembras;
            mInicio = ini.AvesMachos;
            xInicio = ini.AvesMixtas;
        }
        else
        {
            hInicio = lote.HembrasL ?? 0;
            mInicio = lote.MachosL ?? 0;
            xInicio = lote.Mixtas ?? 0;
            if (hInicio + mInicio + xInicio == 0 && lote.AvesEncasetadas.HasValue && lote.AvesEncasetadas.Value > 0)
                xInicio = lote.AvesEncasetadas.Value;
        }

        var totalInicio = hInicio + mInicio + xInicio;

        // Aves vivas actuales: total inicio - (bajas acumuladas del seguimiento) - (ventas acumuladas)
        var bajas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s =>
                (s.MortalidadHembras ?? 0) +
                (s.MortalidadMachos ?? 0) +
                (s.SelH ?? 0) +
                (s.SelM ?? 0) +
                (s.ErrorSexajeHembras ?? 0) +
                (s.ErrorSexajeMachos ?? 0))
            .SumAsync();
        var avesVivas = Math.Max(0, totalInicio - bajas - (vh + vm + vx));

        return new LiquidacionLoteEngordeResumenDto(
            lote.LoteAveEngordeId ?? loteId,
            lote.LoteNombre ?? "",
            lote.EstadoOperativoLote ?? "Abierto",
            hInicio,
            mInicio,
            xInicio,
            totalInicio,
            vh,
            vm,
            vx,
            avesVivas,
            ventas.Count,
            saldo);
    }

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(int loteId, int companyId)
    {
        // Resolve the lote's physical location (granja / nucleo / galpon).
        // This is the source of truth used to filter by event type:
        //   - VENTA_AVES       → lote level  (lote_ave_engorde_id)
        //   - food movements   → galpon level (farm_id + nucleo_id + galpon_id)
        //     (food is received at galpon level; lote_ave_engorde_id may be NULL if the
        //      trigger ran before the lote was created — this covers that case too)
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int farmId      = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        // Calcular rango de fechas del ciclo de vida del lote:
        // Límite inferior: min(fecha de seguimiento) — Límite superior: max(fecha de seguimiento)
        // Esto aísla el histórico del lote actual de registros de lotes previos en el mismo galpón.
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                // Excluir INV_INGRESO generados por el sistema de seguimiento (devoluciones
                // por edición a la baja). Estos ingresos son reversiones contables del
                // inventario físico y no deben mostrarse como "ingreso de alimento" en la
                // tabla diaria; su ausencia del filtro haría que ingresoKg apareciera inflado.
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    // Bird sales: scoped to the specific lote
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    // Food movements: scoped to the galpon regardless of lote assignment
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

        // Aplicar filtro de rango de fechas (ciclo de vida del lote)
        if (fechaMinSeg.HasValue)
            query = query.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var rows = await query
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        return rows.Select(MapHistoricoUnificado).ToList();
    }

    /// <summary>
    /// Calcula el rango de fechas del ciclo de vida de un lote basado en sus registros de seguimiento.
    /// Retorna (fechaMin, fechaMax) donde:
    ///   - fechaMin = fecha del primer seguimiento registrado para este lote
    ///   - fechaMax = fecha del último seguimiento registrado para este lote
    /// Si el lote no tiene seguimientos, ambos son null.
    /// Este rango aísla el histórico del lote actual de registros de lotes previos en el mismo galpón.
    /// </summary>
    private async Task<(DateTime?, DateTime?)> CalcularRangoFechasLoteAsync(int loteId)
    {
        var segFechas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s => s.Fecha)
            .ToListAsync();

        if (segFechas.Count == 0)
            return (null, null);

        return (segFechas.Min(), segFechas.Max());
    }

    /// <summary>
    /// Fecha calendario (yyyy-MM-DD) para ordenar/agrupar movimientos del histórico, alineada con el front
    /// (tabs-principal-engorde: ymdHistoricoEfectivo).
    /// </summary>
    private static string? YmdHistoricoEfectivo(LoteRegistroHistoricoUnificado h)
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

    private static string FormatYmd(DateTime d) =>
        d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static long TsHistorico(LoteRegistroHistoricoUnificado h) =>
        h.CreatedAt.ToUnixTimeMilliseconds();

    private static long TsSeguimiento(SeguimientoDiarioAvesEngorde s)
    {
        var t = new DateTimeOffset(s.Fecha.Year, s.Fecha.Month, s.Fecha.Day, 12, 0, 0, TimeSpan.Zero);
        return t.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Delta de kg por movimiento de histórico (sin INV_CONSUMO: el consumo va en el seguimiento diario).
    /// Solo se consideran movimientos físicos de alimento: INV_INGRESO, INV_TRASLADO_ENTRADA, INV_TRASLADO_SALIDA.
    /// INV_OTRO (AjusteStock / EliminacionStock) son correcciones administrativas del registro de stock
    /// y NO representan alimento físico que entre o salga del galpón; incluirlos inflaría el saldo.
    /// Alineado con <c>deltaHistoricoMovimientoStock</c> en el front.
    /// </summary>
    private static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
    {
        delta = 0;
        ord = 0;
        if (h.Anulado)
            return false;
        var kg = h.CantidadKg ?? 0;
        switch (h.TipoEvento)
        {
            case "INV_INGRESO":
                if (kg == 0) return false;
                delta = kg;
                ord = 0;
                return true;
            case "INV_TRASLADO_ENTRADA":
                if (kg == 0) return false;
                delta = kg;
                ord = 1;
                return true;
            case "INV_TRASLADO_SALIDA":
                if (kg == 0) return false;
                delta = -Math.Abs(kg);
                ord = 2;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Stock (kg) antes del primer día de seguimiento: solo movimientos histórico con fecha efectiva &lt; primer seguimiento.
    /// Tras cada movimiento piso en 0 (misma regla que el front).
    /// </summary>
    private static decimal ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
        IReadOnlyList<LoteRegistroHistoricoUnificado> hist,
        DateTime firstSegDate)
    {
        var firstYmd = FormatYmd(firstSegDate.Date);
        var rows = new List<(string ymd, long ts, decimal delta)>();
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) >= 0)
                continue;
            if (!TryGetHistDeltaAndOrd(h, out var d, out _))
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
        {
            bal += r.delta;
            if (bal < 0) bal = 0;
        }
        return bal;
    }

    private readonly record struct SaldoAlimentoEvent(string Ymd, int Ord, long Tie, long? SegId, decimal Delta);

    /// <summary>
    /// Recalcula y persiste <see cref="SeguimientoDiarioAvesEngorde.SaldoAlimentoKg"/> para todos los registros diarios del lote.
    /// Misma lógica que el front (computeSaldoAlimentoKgPorSeguimiento): no duplica INV_CONSUMO del histórico,
    /// resta consumo del seguimiento, orden estable, piso en 0 tras cada paso.
    /// </summary>
    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.FechaEncaset, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);
        if (lote is null)
            return;

        // Usar scope de galpón (misma lógica que QueryHistoricoUnificadoDtosAsync y el frontend):
        // los movimientos de alimento (INV_INGRESO, INV_TRASLADO_*) se registran a nivel galpón
        // y pueden tener lote_ave_engorde_id nulo cuando el trigger corre antes de que exista el lote.
        var farmId     = lote.GranjaId;
        var nucleoId   = (lote.NucleoId ?? "").Trim();
        var galponId   = (lote.GalponId ?? "").Trim();

        var hist = await _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && !h.Anulado
                && h.TipoEvento != "VENTA_AVES"
                // Excluir INV_INGRESO generados por el propio sistema de seguimiento
                // (devoluciones por eliminación y ajustes a la baja en edición).
                // Estos son asientos contables de reversión del inventario físico; el consumo
                // real ya queda capturado en los registros de seguimiento diario, y incluirlos
                // aquí inflaría el saldo de alimento calculado.
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && h.FarmId == farmId
                && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync(ct);

        var segs = await _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        if (segs.Count == 0)
            return;

        var firstSegDate = segs.Min(s => s.Fecha.Date);
        var encYmd = lote.FechaEncaset.HasValue 
            ? FormatYmd(lote.FechaEncaset.Value.Date) 
            : null;
        var firstYmd = FormatYmd(firstSegDate);

        var opening = ComputeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegDate);

        var events = new List<SaldoAlimentoEvent>(hist.Count + segs.Count);

        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) < 0)
                continue;
            if (encYmd is not null && string.Compare(ymd, encYmd, StringComparison.Ordinal) < 0)
                continue;
            if (!TryGetHistDeltaAndOrd(h, out var delta, out var ord))
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
            bal += e.Delta;
            if (bal < 0) bal = 0;
            if (e.SegId.HasValue)
                saldoPorSegId[e.SegId.Value] = bal;
        }

        foreach (var s in segs)
        {
            s.SaldoAlimentoKg = saldoPorSegId.TryGetValue(s.Id, out var sal) ? sal : bal;
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private static LoteRegistroHistoricoUnificadoDto MapHistoricoUnificado(LoteRegistroHistoricoUnificado e) =>
        new(
            Id: e.Id,
            CompanyId: e.CompanyId,
            LoteAveEngordeId: e.LoteAveEngordeId,
            FarmId: e.FarmId,
            NucleoId: e.NucleoId,
            GalponId: e.GalponId,
            FechaOperacion: e.FechaOperacion,
            TipoEvento: e.TipoEvento,
            OrigenTabla: e.OrigenTabla,
            OrigenId: e.OrigenId,
            MovementTypeOriginal: e.MovementTypeOriginal,
            ItemInventarioEcuadorId: e.ItemInventarioEcuadorId,
            ItemResumen: e.ItemResumen,
            CantidadKg: e.CantidadKg,
            Unidad: e.Unidad,
            CantidadHembras: e.CantidadHembras,
            CantidadMachos: e.CantidadMachos,
            CantidadMixtas: e.CantidadMixtas,
            Referencia: e.Referencia,
            NumeroDocumento: e.NumeroDocumento,
            AcumuladoEntradasAlimentoKg: e.AcumuladoEntradasAlimentoKg,
            Anulado: e.Anulado,
            CreatedAt: e.CreatedAt);

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                       where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        return e is null ? null : MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        var q = from s in _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
                join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                where l.CompanyId == companyId && l.DeletedAt == null
                   && (!loteId.HasValue || s.LoteAveEngordeId == loteId.Value)
                   && (!desde.HasValue || s.Fecha >= desde.Value)
                   && (!hasta.HasValue || s.Fecha <= hasta.Value)
                orderby s.Fecha
                select s;
        var list = await q.ToListAsync();
        return list.Select(MapToDto);
    }

    /// <summary>
    /// Construye el histórico de consumo de alimento por ítem para el campo historico_consumo_alimento.
    /// saldo_inicial = stock actual + oldConsumo (para edición, para restituir al estado pre-consumo del registro anterior).
    /// </summary>
    private async Task<JsonDocument?> BuildHistoricoConsumoAlimentoAsync(
        JsonDocument? metadata,
        int farmId, string? nucleoId, string? galponId,
        Dictionary<int, decimal>? oldByItemId = null)
    {
        if (metadata is null) return null;

        var newByItemId = ParseMetadataItemsToKg(metadata.RootElement);
        if (newByItemId.Count == 0) return null;

        var itemIds = newByItemId.Keys.ToList();

        var catalogItems = await _ctx.ItemInventarioEcuador
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre);

        var nucleoIdN = (nucleoId ?? "").Trim();
        var galponIdN = (galponId ?? "").Trim();
        var stockByItem = await _ctx.InventarioGestionStock
            .AsNoTracking()
            .Where(s =>
                s.FarmId == farmId &&
                (s.NucleoId == null ? "" : s.NucleoId.Trim()) == nucleoIdN &&
                (s.GalponId == null ? "" : s.GalponId.Trim()) == galponIdN &&
                itemIds.Contains(s.ItemInventarioEcuadorId))
            .ToDictionaryAsync(s => s.ItemInventarioEcuadorId, s => s.Quantity);

        var historico = new List<object>();
        foreach (var kv in newByItemId)
        {
            var itemId = kv.Key;
            var consumo = kv.Value;
            var nombre = catalogItems.GetValueOrDefault(itemId, $"Ítem #{itemId}");
            var oldConsumo = oldByItemId?.GetValueOrDefault(itemId, 0m) ?? 0m;
            var currentStock = stockByItem.GetValueOrDefault(itemId, 0m);
            var saldoInicial = currentStock + oldConsumo;
            var saldoFinal = Math.Max(0, saldoInicial - consumo);

            historico.Add(new
            {
                nombre_alimento = nombre,
                saldo_inicial = saldoInicial,
                consumo = consumo,
                saldo_final = saldoFinal,
                unidad_medida = "kg"
            });
        }

        if (historico.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(historico));
    }

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se pueden agregar registros diarios.");

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);

        // Para que el "Registro diario" muestre Ingreso/Traslado/Documento/Despacho,
        // llenamos campos en metadata desde el histórico unificado por lote+fecha.
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForEntity = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        // Snapshot del consumo por ítem antes de descontar del inventario.
        var historicoConsumo = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId);

        var ent = new SeguimientoDiarioAvesEngorde
        {
            LoteAveEngordeId = dto.LoteId,
            Fecha = dto.FechaRegistro,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = (decimal)consumoKgH,
            ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            Ciclo = dto.Ciclo,
            PesoPromHembras = dto.PesoPromH,
            PesoPromMachos = dto.PesoPromM,
            UniformidadHembras = dto.UniformidadH,
            UniformidadMachos = dto.UniformidadM,
            CvHembras = dto.CvH,
            CvMachos = dto.CvM,
            ConsumoAguaDiario = dto.ConsumoAguaDiario,
            ConsumoAguaPh = dto.ConsumoAguaPh,
            ConsumoAguaOrp = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata = metadataForEntity,
            ItemsAdicionales = dto.ItemsAdicionales,
            KcalAlH = kcalAlH,
            ProtAlH = protAlH,
            KcalAveH = kcalAveH,
            ProtAveH = protAveH,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            HistoricoConsumoAlimento = historicoConsumo
        };
        _ctx.SeguimientoDiarioAvesEngorde.Add(ent);
        await _ctx.SaveChangesAsync();

        if (_inventarioGestionService != null && dto.Metadata != null)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                foreach (var kv in byItem)
                    if (kv.Value > 0)
                        await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null));
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar consumo inventario (aves engorde): {ex.Message}"); }
        }

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM + dto.ErrorSexajeHembras + dto.ErrorSexajeMachos;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras,
                    machosRetirados: dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos,
                    mixtasRetiradas: 0,
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Engorde",
                    observaciones: $"Aves de Engorde - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | Error sexaje H: {dto.ErrorSexajeHembras}, M: {dto.ErrorSexajeMachos} | {dto.Observaciones}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde: {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, _current.CompanyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == dto.LoteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede editar el registro.");

        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == dto.Id && l.CompanyId == companyId && l.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        var oldHRet = (ent.MortalidadHembras ?? 0) + (ent.SelH ?? 0) + (ent.ErrorSexajeHembras ?? 0);
        var oldMRet = (ent.MortalidadMachos ?? 0) + (ent.SelM ?? 0) + (ent.ErrorSexajeMachos ?? 0);

        ent.Fecha = dto.FechaRegistro;
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = (decimal)consumoKgH;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;
        ent.Ciclo = dto.Ciclo;
        ent.PesoPromHembras = dto.PesoPromH;
        ent.PesoPromMachos = dto.PesoPromM;
        ent.UniformidadHembras = dto.UniformidadH;
        ent.UniformidadMachos = dto.UniformidadM;
        ent.CvHembras = dto.CvH;
        ent.CvMachos = dto.CvM;
        ent.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        ent.ConsumoAguaPh = dto.ConsumoAguaPh;
        ent.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        ent.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        var oldByItemId = ent.Metadata != null ? ParseMetadataItemsToKg(ent.Metadata.RootElement) : new Dictionary<int, decimal>();

        // Reconstruir snapshot de consumo por ítem (saldo_inicial = stock actual + consumo anterior del registro).
        var historicoConsumoUpdate = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId, oldByItemId);

        // Patch de metadata (Ingreso/Traslado/Documento/Despacho) desde histórico unificado.
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForSave = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        // jsonb + JsonDocument: forzar persistencia; si no, EF puede no marcar Metadata como modificado y el inventario sí aplica el diff desde dto.Metadata.
        ent.Metadata = CloneJsonDocument(metadataForSave);
        ent.ItemsAdicionales = CloneJsonDocument(dto.ItemsAdicionales);
        ent.HistoricoConsumoAlimento = CloneJsonDocument(historicoConsumoUpdate);
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
        // Reforzar persistencia de todas las columnas escalares (además de jsonb).
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        _ctx.Entry(ent).Property(e => e.HistoricoConsumoAlimento).IsModified = true;
        await _ctx.SaveChangesAsync();

        if (_inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0))
        {
            try
            {
                var newByItemId = dto.Metadata != null ? ParseMetadataItemsToKg(dto.Metadata.RootElement) : new Dictionary<int, decimal>();
                var allItemIds = new HashSet<int>(oldByItemId.Keys);
                foreach (var k in newByItemId.Keys) allItemIds.Add(k);
                var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                var farmId = lote.GranjaId;
                var nucleoId = lote.NucleoId?.Trim();
                var galponId = lote.GalponId?.Trim();
                foreach (var itemId in allItemIds)
                {
                    var newQty = newByItemId.GetValueOrDefault(itemId);
                    var oldQty = oldByItemId.GetValueOrDefault(itemId);
                    var diff = newQty - oldQty;
                    if (diff > 0)
                        await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            farmId, nucleoId, galponId, itemId, diff, "kg", refStr + " (ajuste)", null));
                    else if (diff < 0)
                        await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            farmId, nucleoId, galponId, itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento aves engorde"));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al actualizar inventario (aves engorde): {ex.Message}"); }
        }

        var newHRet = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
        var newMRet = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
        var deltaHRet = newHRet - oldHRet;
        var deltaMRet = newMRet - oldMRet;
        if (deltaHRet != 0 || deltaMRet != 0)
        {
            try
            {
                if (deltaHRet > 0 || deltaMRet > 0)
                {
                    await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                        loteId: dto.LoteId,
                        hembrasRetiradas: Math.Max(0, deltaHRet),
                        machosRetirados: Math.Max(0, deltaMRet),
                        mixtasRetiradas: 0,
                        fechaMovimiento: dto.FechaRegistro,
                        fuenteSeguimiento: "Engorde",
                        observaciones: $"Aves de Engorde (actualización) - ajuste retiro H:{deltaHRet}, M:{deltaMRet}");
                }

                if (deltaHRet < 0 || deltaMRet < 0)
                {
                    await DevolverAvesAlInventarioAsync(
                        dto.LoteId,
                        Math.Abs(Math.Min(0, deltaHRet)),
                        Math.Abs(Math.Min(0, deltaMRet)));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde (actualización): {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, companyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                         select new { Seguimiento = s, l.GranjaId, l.NucleoId, l.GalponId, l.EstadoOperativoLote }).SingleOrDefaultAsync();
        if (ent is null) return false;
        if (string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede eliminar el registro.");

        if (_inventarioGestionService != null && ent.Seguimiento.Metadata != null)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(ent.Seguimiento.Metadata.RootElement);
                var refStr = $"Seguimiento aves engorde #{id} (devolución por eliminación)";
                foreach (var kv in byItem)
                    if (kv.Value > 0)
                        await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            ent.GranjaId, ent.NucleoId?.Trim(), ent.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento aves engorde"));
            }
            catch (Exception ex) { Console.WriteLine($"Error al devolver inventario al eliminar seguimiento aves engorde: {ex.Message}"); }
        }

        // Anular INV_CONSUMO del seguimiento eliminado en el histórico unificado.
        // Sin esto, si se crea un nuevo seguimiento para la misma fecha, aparecerían dos
        // registros INV_CONSUMO activos (el antiguo + el nuevo), duplicando consumoBodegaKg.
        // La "devolución por eliminación" INV_INGRESO ya revierte el stock; el INV_CONSUMO
        // original debe quedar anulado para que el histórico refleje solo el estado real.
        try
        {
            var refPrefix = $"Seguimiento aves engorde #{id}";
            var farmIdDel   = ent.GranjaId;
            var nucleoIdDel = (ent.NucleoId ?? "").Trim();
            var galponIdDel = (ent.GalponId ?? "").Trim();
            var consumosHuerfanos = await _ctx.LoteRegistroHistoricoUnificados
                .Where(h => h.TipoEvento == "INV_CONSUMO"
                         && !h.Anulado
                         && h.FarmId == farmIdDel
                         && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoIdDel
                         && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponIdDel
                         && h.Referencia != null
                         && h.Referencia.StartsWith(refPrefix))
                .ToListAsync();
            foreach (var r in consumosHuerfanos)
                r.Anulado = true;
            if (consumosHuerfanos.Count > 0)
                await _ctx.SaveChangesAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Error al anular INV_CONSUMO al eliminar seguimiento aves engorde: {ex.Message}"); }

        var retH = (ent.Seguimiento.MortalidadHembras ?? 0) + (ent.Seguimiento.SelH ?? 0) + (ent.Seguimiento.ErrorSexajeHembras ?? 0);
        var retM = (ent.Seguimiento.MortalidadMachos ?? 0) + (ent.Seguimiento.SelM ?? 0) + (ent.Seguimiento.ErrorSexajeMachos ?? 0);
        if (retH > 0 || retM > 0)
        {
            try { await DevolverAvesAlInventarioAsync(ent.Seguimiento.LoteAveEngordeId, retH, retM); }
            catch (Exception ex) { Console.WriteLine($"Error al devolver aves al eliminar seguimiento engorde: {ex.Message}"); }
        }

        var loteIdSeg = ent.Seguimiento.LoteAveEngordeId;
        _ctx.SeguimientoDiarioAvesEngorde.Remove(ent.Seguimiento);
        await _ctx.SaveChangesAsync();
        await RecalcularSaldoAlimentoPorLoteAsync(loteIdSeg, companyId);
        return true;
    }

    private async Task DevolverAvesAlInventarioAsync(int loteId, int hembras, int machos)
    {
        if (hembras <= 0 && machos <= 0) return;
        var inv = await _ctx.InventarioAves
            .Where(i => i.LoteId == loteId &&
                        i.CompanyId == _current.CompanyId &&
                        i.DeletedAt == null &&
                        i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();
        if (inv == null) return;
        inv.CantidadHembras += Math.Max(0, hembras);
        inv.CantidadMachos += Math.Max(0, machos);
        inv.FechaActualizacion = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    /// <summary>Clona JsonDocument para que EF Core persista cambios en columnas jsonb (evita comparador que ignora actualizaciones).</summary>
    private static JsonDocument? CloneJsonDocument(JsonDocument? doc)
    {
        if (doc is null) return null;
        return JsonDocument.Parse(doc.RootElement.GetRawText());
    }

    private static string FormatKg(decimal kg)
        => kg.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Calcula totales de Ingreso/Traslado (alimento) y Despacho (ventas aves)
    /// para el lote+fecha del seguimiento, desde la tabla unificada.
    /// Filtra por el rango de fechas del ciclo de vida del lote para evitar duplicación
    /// de datos de lotes anteriores en el mismo galpón.
    /// </summary>
    private async Task<Dictionary<string, object?>> BuildStockMetadataPatchAsync(int loteId, DateTime fecha)
    {
        var day = fecha.Date;
        var companyId = _current.CompanyId;

        // Calcular rango de fechas del lote (ciclo de vida) para aislar de lotes previos
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId
                && x.LoteAveEngordeId == loteId
                && x.FechaOperacion == day
                && !x.Anulado
                && !((x.Referencia != null && x.Referencia.Contains("devolución por eliminación"))
                     || (x.Referencia != null && x.Referencia.Contains("devolucion por eliminacion")))
                // Excluir INV_INGRESO de devoluciones por edición de seguimiento para que
                // ingresoAlimento en metadata no aparezca inflado.
                && !(x.TipoEvento == "INV_INGRESO"
                     && x.Referencia != null
                     && x.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (x.TipoEvento == "INV_INGRESO"
                    || x.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || x.TipoEvento == "VENTA_AVES"));

        // Aplicar filtro de rango de fechas (ciclo de vida del lote)
        if (fechaMinSeg.HasValue)
            query = query.Where(x => x.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            query = query.Where(x => x.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var agg = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                IngresoKg = g.Sum(x => x.TipoEvento == "INV_INGRESO" ? (x.CantidadKg ?? 0m) : 0m),
                TrasladoKg = g.Sum(x => x.TipoEvento == "INV_TRASLADO_ENTRADA" ? (x.CantidadKg ?? 0m) : 0m),
                DespachoH = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadHembras ?? 0) : 0),
                DespachoM = g.Sum(x => x.TipoEvento == "VENTA_AVES" ? (x.CantidadMachos ?? 0) : 0),
                Documento = g
                    .Where(x => x.TipoEvento == "INV_INGRESO")
                    .Select(x => x.NumeroDocumento ?? x.Referencia)
                    .Max()
            })
            .SingleOrDefaultAsync();

        var patch = new Dictionary<string, object?>();
        if (agg is null) return patch;

        if (agg.IngresoKg > 0)
        {
            var s = FormatKg(agg.IngresoKg);
            patch["ingresoAlimento"] = s;
            patch["ingreso_alimento"] = s;
            patch["ingresoAlimentoKg"] = agg.IngresoKg;
        }

        if (agg.TrasladoKg > 0)
        {
            var s = FormatKg(agg.TrasladoKg);
            patch["traslado"] = s;
            patch["notaTraslado"] = s;
            patch["trasladoAlimento"] = s;
        }

        if (!string.IsNullOrWhiteSpace(agg.Documento))
        {
            var d = agg.Documento.Trim();
            patch["documento"] = d;
            patch["documentoAlimento"] = d;
            patch["nroDocumento"] = d;
            patch["numeroDocumento"] = d;
        }

        if (agg.DespachoH > 0)
        {
            patch["despachoHembras"] = agg.DespachoH;
            patch["despachoH"] = agg.DespachoH;
            patch["despacho_hembra"] = agg.DespachoH;
        }

        if (agg.DespachoM > 0)
        {
            patch["despachoMachos"] = agg.DespachoM;
            patch["despachoM"] = agg.DespachoM;
            patch["despacho_macho"] = agg.DespachoM;
        }

        return patch;
    }

    private static JsonDocument? MergeMetadataWithPatch(JsonDocument? existing, Dictionary<string, object?> patch)
    {
        if ((patch is null || patch.Count == 0) && existing is null)
            return null;

        if (patch is null || patch.Count == 0)
            return existing;

        Dictionary<string, object?> dict;
        if (existing != null)
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(existing.RootElement.GetRawText())
                   ?? new Dictionary<string, object?>();
        }
        else
        {
            dict = new Dictionary<string, object?>();
        }

        foreach (var kv in patch)
            dict[kv.Key] = kv.Value;

        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private static decimal ToKg(double cantidad, string? unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }

    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
    {
        var byItemId = new Dictionary<int, decimal>();
        if (root.TryGetProperty("itemsHembras", out var arrH))
            foreach (var e in arrH.EnumerateArray())
            {
                var id = 0;
                if (e.TryGetProperty("itemInventarioEcuadorId", out var pid) && pid.ValueKind != JsonValueKind.Null)
                    id = pid.GetInt32();
                if (id <= 0 && e.TryGetProperty("catalogItemId", out var cid))
                    id = cid.GetInt32();
                if (id <= 0) continue;
                var cant = e.TryGetProperty("cantidad", out var c) ? c.GetDouble() : 0;
                var un = e.TryGetProperty("unidad", out var u) ? u.GetString() : "kg";
                byItemId[id] = byItemId.GetValueOrDefault(id) + ToKg(cant, un);
            }
        if (root.TryGetProperty("itemsMachos", out var arrM))
            foreach (var e in arrM.EnumerateArray())
            {
                var id = 0;
                if (e.TryGetProperty("itemInventarioEcuadorId", out var pid) && pid.ValueKind != JsonValueKind.Null)
                    id = pid.GetInt32();
                if (id <= 0 && e.TryGetProperty("catalogItemId", out var cid))
                    id = cid.GetInt32();
                if (id <= 0) continue;
                var cant = e.TryGetProperty("cantidad", out var c) ? c.GetDouble() : 0;
                var un = e.TryGetProperty("unidad", out var u) ? u.GetString() : "kg";
                byItemId[id] = byItemId.GetValueOrDefault(id) + ToKg(cant, un);
            }
        return byItemId;
    }

    private async Task<int> CalcularHembrasVivasAsync(int loteAveEngordeId)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
            .Select(l => new { Base = l.HembrasL ?? 0, MortCaja = l.MortCajaH ?? 0 })
            .SingleOrDefaultAsync();
        if (lote is null) return 0;
        int baseH = lote.Base, mortCajaH = lote.MortCaja;

        var sum = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(x => x.LoteAveEngordeId == loteAveEngordeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0)
            })
            .SingleOrDefaultAsync();

        int mort = sum?.MortH ?? 0, sel = sum?.SelH ?? 0, err = sum?.ErrH ?? 0;
        var vivas = baseH - mortCajaH - mort - sel - err;
        return Math.Max(0, vivas);
    }

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = kcalAlH is null ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = protAlH is null ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
    }

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }

    public async Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{loteId}' no existe o no pertenece a la compañía.");

        return await Task.FromResult(new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, 0, new List<ResultadoLevanteItemDto>()));
    }

    public async Task<SeguimientoAvesEngordeBackfillResultDto> BackfillMetadataAsync(
        int loteId,
        DateTime? desde,
        DateTime? hasta,
        bool onlyIfMissing = true)
    {
        var companyId = _current.CompanyId;

        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            throw new InvalidOperationException($"Lote aves de engorde '{loteId}' no existe o no pertenece a la compañía.");

        var q = _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId);
        if (desde.HasValue) q = q.Where(s => s.Fecha >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(s => s.Fecha <= hasta.Value.Date);

        var list = await q.OrderBy(s => s.Fecha).ToListAsync();
        var total = list.Count;

        var actualizados = 0;
        var omitidos = 0;
        var sinDatosHistorico = 0;

        foreach (var s in list)
        {
            if (onlyIfMissing && MetadataYaTieneCamposKardex(s.Metadata))
            {
                omitidos++;
                continue;
            }

            var patch = await BuildStockMetadataPatchAsync(loteId, s.Fecha.Date);
            if (patch.Count == 0)
            {
                sinDatosHistorico++;
                omitidos++;
                continue;
            }

            s.Metadata = MergeMetadataWithPatch(s.Metadata, patch);
            _ctx.Entry(s).Property(x => x.Metadata).IsModified = true;
            actualizados++;
        }

        if (actualizados > 0)
            await _ctx.SaveChangesAsync();

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        return new SeguimientoAvesEngordeBackfillResultDto(
            LoteId: loteId,
            Desde: desde?.Date,
            Hasta: hasta?.Date,
            TotalRegistros: total,
            Actualizados: actualizados,
            Omitidos: omitidos,
            SinDatosHistorico: sinDatosHistorico);
    }

    private static bool MetadataYaTieneCamposKardex(JsonDocument? metadata)
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

    // ─── Cuadrar Saldos ────────────────────────────────────────────────────────

    public async Task<CuadrarSaldosValidarResponseDto> ValidarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<FilaExcelCuadrarSaldosDto> filasExcel)
    {
        var companyId = _current.CompanyId;

        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Lote {loteId} no encontrado.");

        int farmId     = loteInfo.GranjaId;
        string nucId   = (loteInfo.NucleoId ?? "").Trim();
        string galId   = (loteInfo.GalponId ?? "").Trim();

        // Rango de fechas: primer y último seguimiento registrado en la aplicación para este lote.
        // Esto evita mezclar registros de otros lotes que hayan usado el mismo galpón en otras épocas.
        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        // Si no hay seguimientos en la aplicación, usar el rango que trae el propio Excel
        if (fechaMinSeg == null && filasExcel.Count > 0)
        {
            var excelDates = filasExcel
                .Select(f => DateTime.TryParseExact(f.Fecha?.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null)
                .Where(d => d.HasValue).Select(d => d!.Value).ToList();
            if (excelDates.Count > 0)
            {
                fechaMinSeg = excelDates.Min();
                fechaMaxSeg = excelDates.Max();
            }
        }

        // Cargar movimientos de alimento relevantes del galpón, acotados al rango del lote (sin anulados, sin devoluciones de sistema)
        var histQuery = _ctx.LoteRegistroHistoricoUnificados
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                   || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                && (h.TipoEvento == "INV_INGRESO"
                    || h.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || h.TipoEvento == "INV_TRASLADO_SALIDA")
                && h.FarmId == farmId
                && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucId
                && (h.GalponId == null ? "" : h.GalponId.Trim()) == galId);

        if (fechaMinSeg.HasValue)
            histQuery = histQuery.Where(h => h.FechaOperacion >= fechaMinSeg.Value.Date);
        if (fechaMaxSeg.HasValue)
            histQuery = histQuery.Where(h => h.FechaOperacion <= fechaMaxSeg.Value.Date.AddDays(1).AddTicks(-1));

        var hist = await histQuery
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        // Agrupar por fecha efectiva
        var histByDate = new Dictionary<string, List<LoteRegistroHistoricoUnificado>>(StringComparer.Ordinal);
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd == null) continue;
            if (!histByDate.TryGetValue(ymd, out var lst)) { lst = new(); histByDate[ymd] = lst; }
            lst.Add(h);
        }

        // Excel por fecha
        var excelByDate = new Dictionary<string, FilaExcelCuadrarSaldosDto>(StringComparer.Ordinal);
        foreach (var f in filasExcel)
        {
            var k = (f.Fecha ?? "").Trim();
            if (!string.IsNullOrEmpty(k)) excelByDate.TryAdd(k, f);
        }

        // Todas las fechas a revisar (unión ordenada)
        var allDates = new SortedSet<string>(histByDate.Keys.Concat(excelByDate.Keys), StringComparer.Ordinal);

        var inconsistencias = new List<InconsistenciaCuadrarSaldosDto>();
        var acciones = new List<AccionCorreccionCuadrarSaldosDto>();

        // Colección de IDs ya asignados a una acción (para no sugerir el mismo movimiento dos veces)
        var histIdsUsados = new HashSet<long>();

        foreach (var fecha in allDates)
        {
            excelByDate.TryGetValue(fecha, out var excelFila);
            histByDate.TryGetValue(fecha, out var histFila);
            histFila ??= [];

            // Totales del sistema en esta fecha
            var sysIngresoKg = histFila
                .Where(h => h.TipoEvento == "INV_INGRESO")
                .Sum(h => h.CantidadKg ?? 0m);
            var sysTrasladoEntradaKg = histFila
                .Where(h => h.TipoEvento == "INV_TRASLADO_ENTRADA")
                .Sum(h => h.CantidadKg ?? 0m);
            var sysTrasladoSalidaKg = histFila
                .Where(h => h.TipoEvento == "INV_TRASLADO_SALIDA")
                .Sum(h => Math.Abs(h.CantidadKg ?? 0m));
            var sysDocumentos = histFila
                .Select(h => (h.NumeroDocumento?.Trim() ?? h.Referencia?.Trim() ?? "").Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            // ── Ingresos ──
            var excelIngreso = excelFila?.IngresoAlimentoKg ?? 0m;
            if (Math.Abs(excelIngreso - sysIngresoKg) > 0.001m)
            {
                var primerHistId = histFila.FirstOrDefault(h => h.TipoEvento == "INV_INGRESO")?.Id;
                var sysDoc = sysDocumentos.FirstOrDefault();

                if (excelIngreso > 0 && sysIngresoKg == 0)
                {
                    inconsistencias.Add(new("INGRESO_FALTANTE", fecha,
                        $"Excel tiene ingreso de {excelIngreso:F3} kg pero el sistema no registra ninguno ese día.",
                        excelIngreso, 0m, null, excelFila?.Documento, null));

                    // Buscar candidato en otro día: primero por documento+monto, luego solo monto
                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_INGRESO",
                        excelIngreso, excelFila?.Documento);

                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha,
                            null, null, null,
                            candidato.NumeroDocumento ?? candidato.Referencia,
                            $"Mover ingreso de {excelIngreso:F3} kg (ID:{candidato.Id}, fecha actual: {YmdHistoricoEfectivo(candidato)}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_INGRESO", excelIngreso,
                            excelFila?.Documento,
                            $"Insertar ingreso de {excelIngreso:F3} kg en {fecha} (doc: {excelFila?.Documento ?? "-"})"));
                    }
                }
                else if (excelIngreso == 0 && sysIngresoKg > 0)
                {
                    inconsistencias.Add(new("INGRESO_SOBRANTE", fecha,
                        $"Sistema tiene ingreso de {sysIngresoKg:F3} kg pero el Excel no muestra ninguno ese día.",
                        0m, sysIngresoKg, primerHistId, null, sysDoc));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_INGRESO"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular ingreso sobrante de {h.CantidadKg:F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("INGRESO_MONTO_DIFERENTE", fecha,
                        $"Ingreso Excel: {excelIngreso:F3} kg — sistema: {sysIngresoKg:F3} kg.",
                        excelIngreso, sysIngresoKg, primerHistId, excelFila?.Documento, sysDoc));
                }
            }

            // ── Traslados entrada ──
            var excelTrasladoEntrada = excelFila?.TrasladoEntradaKg ?? 0m;
            if (Math.Abs(excelTrasladoEntrada - sysTrasladoEntradaKg) > 0.001m)
            {
                var h0 = histFila.FirstOrDefault(h => h.TipoEvento == "INV_TRASLADO_ENTRADA");

                if (excelTrasladoEntrada > 0 && sysTrasladoEntradaKg == 0)
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_FALTANTE", fecha,
                        $"Excel tiene traslado entrada de {excelTrasladoEntrada:F3} kg pero el sistema no registra ninguno ese día.",
                        excelTrasladoEntrada, 0m, null, excelFila?.Documento, null));

                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_TRASLADO_ENTRADA",
                        excelTrasladoEntrada, null);
                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha, null, null, null, null,
                            $"Mover traslado entrada de {excelTrasladoEntrada:F3} kg (ID:{candidato.Id}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_TRASLADO_ENTRADA", excelTrasladoEntrada, excelFila?.Documento,
                            $"Insertar traslado entrada de {excelTrasladoEntrada:F3} kg en {fecha}"));
                    }
                }
                else if (excelTrasladoEntrada == 0 && sysTrasladoEntradaKg > 0)
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_SOBRANTE", fecha,
                        $"Sistema tiene traslado entrada de {sysTrasladoEntradaKg:F3} kg pero Excel no.",
                        0m, sysTrasladoEntradaKg, h0?.Id, null, null));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_TRASLADO_ENTRADA"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular traslado entrada sobrante de {h.CantidadKg:F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("TRASLADO_ENTRADA_DIFERENTE", fecha,
                        $"Traslado entrada Excel: {excelTrasladoEntrada:F3} kg — sistema: {sysTrasladoEntradaKg:F3} kg.",
                        excelTrasladoEntrada, sysTrasladoEntradaKg, h0?.Id, null, null));
                }
            }

            // ── Traslados salida ──
            var excelTrasladoSalida = excelFila?.TrasladoSalidaKg ?? 0m;
            if (Math.Abs(excelTrasladoSalida - sysTrasladoSalidaKg) > 0.001m)
            {
                var h0 = histFila.FirstOrDefault(h => h.TipoEvento == "INV_TRASLADO_SALIDA");

                if (excelTrasladoSalida > 0 && sysTrasladoSalidaKg == 0)
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_FALTANTE", fecha,
                        $"Excel tiene traslado salida de {excelTrasladoSalida:F3} kg pero el sistema no registra ninguno ese día.",
                        excelTrasladoSalida, 0m, null, null, null));

                    var candidato = BuscarCandidatoHistorico(hist, histIdsUsados, "INV_TRASLADO_SALIDA",
                        excelTrasladoSalida, null);
                    if (candidato != null)
                    {
                        histIdsUsados.Add(candidato.Id);
                        acciones.Add(new(
                            "AJUSTAR_FECHA", candidato.Id, fecha, null, null, null, null,
                            $"Mover traslado salida de {excelTrasladoSalida:F3} kg (ID:{candidato.Id}) → {fecha}"));
                    }
                    else
                    {
                        acciones.Add(new(
                            "INSERTAR", null, null,
                            fecha, "INV_TRASLADO_SALIDA", excelTrasladoSalida, null,
                            $"Insertar traslado salida de {excelTrasladoSalida:F3} kg en {fecha}"));
                    }
                }
                else if (excelTrasladoSalida == 0 && sysTrasladoSalidaKg > 0)
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_SOBRANTE", fecha,
                        $"Sistema tiene traslado salida de {sysTrasladoSalidaKg:F3} kg pero Excel no.",
                        0m, sysTrasladoSalidaKg, h0?.Id, null, null));

                    foreach (var h in histFila.Where(x => x.TipoEvento == "INV_TRASLADO_SALIDA"))
                    {
                        if (histIdsUsados.Add(h.Id))
                            acciones.Add(new(
                                "ANULAR", h.Id, null, null, null, null, null,
                                $"Anular traslado salida sobrante de {Math.Abs(h.CantidadKg ?? 0):F3} kg en {fecha} (ID:{h.Id})"));
                    }
                }
                else
                {
                    inconsistencias.Add(new("TRASLADO_SALIDA_DIFERENTE", fecha,
                        $"Traslado salida Excel: {excelTrasladoSalida:F3} kg — sistema: {sysTrasladoSalidaKg:F3} kg.",
                        excelTrasladoSalida, sysTrasladoSalidaKg, h0?.Id, null, null));
                }
            }

            // ── Documento ──
            var excelDoc = (excelFila?.Documento ?? "").Trim();
            if (!string.IsNullOrEmpty(excelDoc) && sysDocumentos.Count > 0)
            {
                var matchDoc = sysDocumentos.Any(d =>
                    string.Equals(d, excelDoc, StringComparison.OrdinalIgnoreCase));
                if (!matchDoc)
                {
                    inconsistencias.Add(new("DOCUMENTO_DIFERENTE", fecha,
                        $"Documento Excel: \"{excelDoc}\" — sistema: \"{string.Join(", ", sysDocumentos)}\".",
                        null, null,
                        histFila.FirstOrDefault()?.Id,
                        excelDoc, string.Join(", ", sysDocumentos)));
                }
            }
        }

        return new CuadrarSaldosValidarResponseDto(
            loteId,
            filasExcel.Count,
            inconsistencias.Count,
            inconsistencias,
            acciones);
    }

    /// <summary>
    /// Busca el candidato más probable en el histórico completo para coincidir con el monto dado.
    /// Prioriza: mismo documento + mismo monto; si no, mismo monto más cercano en fecha.
    /// </summary>
    private static LoteRegistroHistoricoUnificado? BuscarCandidatoHistorico(
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

    public async Task<CuadrarSaldosAplicarResponseDto> AplicarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<AccionCorreccionCuadrarSaldosDto> acciones,
        IReadOnlyList<FilaExcelCuadrarSaldosDto>? filasExcel = null)
    {
        var companyId = _current.CompanyId;

        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException($"Lote {loteId} no encontrado.");

        int fechasAjustadas = 0, registrosAnulados = 0, registrosInsertados = 0;
        var nuevosParaFixOrigenId = new List<LoteRegistroHistoricoUnificado>();

        // Cargar de una vez los IDs a modificar
        var idsModificar = acciones
            .Where(a => a.HistoricoId.HasValue && a.TipoAccion is "AJUSTAR_FECHA" or "ANULAR")
            .Select(a => a.HistoricoId!.Value)
            .Distinct()
            .ToList();

        var entidades = idsModificar.Count > 0
            ? await _ctx.LoteRegistroHistoricoUnificados
                .Where(h => idsModificar.Contains(h.Id) && h.CompanyId == companyId)
                .ToListAsync()
            : [];

        var entidadPorId = entidades.ToDictionary(h => h.Id);

        foreach (var accion in acciones)
        {
            switch (accion.TipoAccion)
            {
                case "AJUSTAR_FECHA":
                    if (accion.HistoricoId.HasValue
                        && !string.IsNullOrEmpty(accion.NuevaFecha)
                        && DateTime.TryParse(accion.NuevaFecha, null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var nuevaFecha)
                        && entidadPorId.TryGetValue(accion.HistoricoId.Value, out var hAj))
                    {
                        hAj.FechaOperacion = nuevaFecha.Date;
                        fechasAjustadas++;
                    }
                    break;

                case "ANULAR":
                    if (accion.HistoricoId.HasValue
                        && entidadPorId.TryGetValue(accion.HistoricoId.Value, out var hAn))
                    {
                        hAn.Anulado = true;
                        registrosAnulados++;
                    }
                    break;

                case "INSERTAR":
                    if (!string.IsNullOrEmpty(accion.FechaInsertar)
                        && !string.IsNullOrEmpty(accion.TipoEvento)
                        && accion.CantidadKg.HasValue
                        && DateTime.TryParse(accion.FechaInsertar, null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out var fechaIns))
                    {
                        var nuevo = new LoteRegistroHistoricoUnificado
                        {
                            CompanyId  = companyId,
                            LoteAveEngordeId = loteId,
                            FarmId     = loteInfo.GranjaId,
                            NucleoId   = loteInfo.NucleoId,
                            GalponId   = loteInfo.GalponId,
                            FechaOperacion = fechaIns.Date,
                            TipoEvento = accion.TipoEvento,
                            OrigenTabla = "cuadrar_saldos_engorde",
                            OrigenId   = 0, // se corrige tras SaveChanges usando el Id generado
                            CantidadKg = accion.TipoEvento == "INV_TRASLADO_SALIDA"
                                ? -Math.Abs(accion.CantidadKg.Value)
                                : accion.CantidadKg.Value,
                            Unidad     = "kg",
                            NumeroDocumento = accion.Documento,
                            Referencia = $"Cuadre saldos Excel — {accion.Descripcion ?? accion.TipoEvento}",
                            Anulado    = false,
                            CreatedAt  = DateTimeOffset.UtcNow
                        };
                        _ctx.LoteRegistroHistoricoUnificados.Add(nuevo);
                        nuevosParaFixOrigenId.Add(nuevo);
                        registrosInsertados++;
                    }
                    break;
            }
        }

        if (fechasAjustadas + registrosAnulados + registrosInsertados > 0)
        {
            // Primer save: AJUSTAR_FECHA y ANULAR se resuelven aquí;
            // los INSERTAR obtienen su Id autogenerado pero OrigenId=0 aún.
            // Guardamos por separado AJUSTAR/ANULAR primero si hay INSERTs para
            // evitar la violación del unique (origen_tabla, origen_id).
            if (nuevosParaFixOrigenId.Count > 0)
            {
                // Guardar solo las modificaciones existentes (AJUSTAR/ANULAR)
                // sin los nuevos todavía, para no violar el unique con OrigenId=0.
                var sinNuevos = nuevosParaFixOrigenId
                    .Select(n => _ctx.Entry(n))
                    .ToList();
                sinNuevos.ForEach(e => e.State = Microsoft.EntityFrameworkCore.EntityState.Detached);

                if (fechasAjustadas + registrosAnulados > 0)
                    await _ctx.SaveChangesAsync();

                // Volver a adjuntar y guardar los nuevos uno a uno para que
                // cada uno obtenga su Id antes del siguiente (el unique lo exige).
                foreach (var n in nuevosParaFixOrigenId)
                {
                    _ctx.LoteRegistroHistoricoUnificados.Add(n);
                    await _ctx.SaveChangesAsync();
                    n.OrigenId = (int)(n.Id & 0x7FFFFFFF); // Id cabe en int; siempre > 0
                    await _ctx.SaveChangesAsync();
                }
            }
            else
            {
                await _ctx.SaveChangesAsync();
            }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(loteId, companyId);

        var metadataLimpiados = filasExcel != null && filasExcel.Count > 0
            ? await ReconciliarMetadataDocumentoAsync(loteId, filasExcel)
            : 0;

        return new CuadrarSaldosAplicarResponseDto(
            loteId,
            fechasAjustadas,
            registrosAnulados,
            registrosInsertados,
            metadataLimpiados,
            $"Correcciones aplicadas: {fechasAjustadas} fecha(s) ajustada(s), " +
            $"{registrosAnulados} registro(s) anulado(s), " +
            $"{registrosInsertados} registro(s) insertado(s)" +
            (metadataLimpiados > 0 ? $", {metadataLimpiados} seguimiento(s) con metadata corregida." : "."));
    }

    /// <summary>
    /// Limpia las claves de documento (documento, documentoAlimento, nroDocumento, numeroDocumento)
    /// de la metadata de seguimientos cuyas fechas no tienen movimientos reales en el Excel.
    /// Esto elimina "fechas fantasma" donde el histórico fue corregido pero la metadata del
    /// seguimiento diario todavía referencia el documento anterior.
    /// </summary>
    private async Task<int> ReconciliarMetadataDocumentoAsync(
        int loteId,
        IReadOnlyList<FilaExcelCuadrarSaldosDto> filasExcel)
    {
        // Fechas del Excel donde hay al menos un movimiento real (ingreso, traslado o documento)
        var fechasValidasExcel = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fila in filasExcel)
        {
            var tieneMovimiento = (fila.IngresoAlimentoKg.HasValue && fila.IngresoAlimentoKg > 0)
                || (fila.TrasladoEntradaKg.HasValue && fila.TrasladoEntradaKg > 0)
                || (fila.TrasladoSalidaKg.HasValue && fila.TrasladoSalidaKg > 0)
                || !string.IsNullOrWhiteSpace(fila.Documento);
            if (tieneMovimiento)
                fechasValidasExcel.Add(fila.Fecha); // YYYY-MM-DD
        }

        if (fechasValidasExcel.Count == 0) return 0;

        var seguimientos = await _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId && s.Metadata != null)
            .ToListAsync();

        int limpiados = 0;
        foreach (var seg in seguimientos)
        {
            if (seg.Metadata is null) continue;

            var fechaSeg = seg.Fecha.ToString("yyyy-MM-dd");
            if (fechasValidasExcel.Contains(fechaSeg)) continue;

            try
            {
                var root = seg.Metadata.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;

                var tieneClaveDoc = _docMetadataKeys.Any(k => root.TryGetProperty(k, out _));
                if (!tieneClaveDoc) continue;

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())
                           ?? new Dictionary<string, JsonElement>();

                var modificado = false;
                foreach (var key in _docMetadataKeys)
                {
                    if (dict.Remove(key))
                        modificado = true;
                }

                if (modificado)
                {
                    seg.Metadata = dict.Count > 0
                        ? JsonDocument.Parse(JsonSerializer.Serialize(dict))
                        : null;
                    limpiados++;
                }
            }
            catch { /* metadata malformado: no modificar */ }
        }

        if (limpiados > 0)
            await _ctx.SaveChangesAsync();

        return limpiados;
    }
}
