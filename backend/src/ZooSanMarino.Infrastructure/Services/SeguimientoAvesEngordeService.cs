// Partial 'ancla' del seguimiento diario de aves de engorde (Colombia): usings, campos, ctor,
// constantes, helpers estáticos compartidos y la interfaz. La implementación vive repartida por
// responsabilidad en 'SeguimientoAvesEngorde/Funciones/' (Consultas, Crud, SaldoAlimento, Metadata,
// CuadrarSaldos). Namespace plano → misma DI, misma interfaz, mismo comportamiento. La aritmética
// pura vive en Application/Calculos/SeguimientoAvesEngordeCalculos.cs.
//
// Seguimiento Diario Aves de Engorde: persiste en tabla seguimiento_diario_aves_engorde (FK a lote_ave_engorde).
// Filtros del módulo muestran lotes de lote_ave_engorde. DTO mantiene LoteId = lote_ave_engorde_id para el front.
//
// Inventario nuevo (inventario-gestion / item_inventario_ecuador): este módulo es el único que aplica consumo
// y devolución sobre el inventario nuevo. El módulo Seguimiento diario postura (ProduccionService) no usa
// inventario-gestion; los dos módulos de inventario están divididos (postura → su inventario; pollo engorde → inventario-gestion).
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService : ISeguimientoAvesEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly IInventarioGestionService? _inventarioGestionService;
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;  // Fase 3 paso 2: modelo B nivel granja (Colombia)
    private readonly ILogger<SeguimientoAvesEngordeService>? _logger;

    public SeguimientoAvesEngordeService(
        ZooSanMarinoContext ctx,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService,
        IInventarioGestionService? inventarioGestionService = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null,
        ILogger<SeguimientoAvesEngordeService>? logger = null)
    {
        _ctx = ctx;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
        _inventarioGestionService = inventarioGestionService;
        _colombiaConsumoB = colombiaConsumoB;
        _logger = logger;
    }

    private static readonly string[] _docMetadataKeys =
        ["documento", "documentoAlimento", "nroDocumento", "numeroDocumento"];

    // ─── Delegadores thin a la aritmética pura (Application/Calculos) ────────────
    private static string FormatYmd(DateTime d)
        => SeguimientoAvesEngordeCalculos.FormatYmd(d);

    private static string FormatKg(decimal kg)
        => SeguimientoAvesEngordeCalculos.FormatKg(kg);

    private static string? YmdHistoricoEfectivo(LoteRegistroHistoricoUnificado h)
        => SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h);

    private static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
        => SaldoAlimentoEngordeCalculos.TryGetHistDeltaAndOrd(h, out delta, out ord);

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
        => SeguimientoEngordeCalculos.CalcularDerivados(consumoKgHembras, kcalAlH, protAlH);

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
        => SeguimientoEngordeCalculos.CalcularSemana(fechaEncaset, fechaRegistro);

    private static JsonDocument? MergeMetadataWithPatch(JsonDocument? existing, Dictionary<string, object?> patch)
        => MetadataEngordeCalculos.MergeMetadataWithPatch(existing, patch);

    private static decimal ToKg(double cantidad, string? unidad)
        => MetadataEngordeCalculos.ToKg(cantidad, unidad);

    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
        => MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

    private static bool MetadataYaTieneCamposKardex(JsonDocument? metadata)
        => SeguimientoAvesEngordeCalculos.MetadataYaTieneCamposKardex(metadata);

    private static LoteRegistroHistoricoUnificado? BuscarCandidatoHistorico(
        IEnumerable<LoteRegistroHistoricoUnificado> hist,
        HashSet<long> idsUsados,
        string tipoEvento,
        decimal montoKg,
        string? documento)
        => SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(hist, idsUsados, tipoEvento, montoKg, documento);

    /// <summary>Clona JsonDocument para que EF Core persista cambios en columnas jsonb (evita comparador que ignora actualizaciones).</summary>
    private static JsonDocument? CloneJsonDocument(JsonDocument? doc)
    {
        if (doc is null) return null;
        return JsonDocument.Parse(doc.RootElement.GetRawText());
    }

    /// <summary>
    /// País efectivo del lote (lote_ave_engorde) para gatear el descuento del inventario modelo B.
    /// Este servicio atiende engorde Colombia (el caso peligroso: inyecta el inventario Ecuador),
    /// por eso el gate es crítico. Fuente: <c>lote.PaisId</c> si está poblado; si no, derivado desde
    /// la granja (farm.DepartamentoId → departamentos.PaisId), la misma cadena que usa el inventario.
    /// </summary>
    private async Task<int?> ResolverPaisIdLoteAsync(int granjaId, int? paisIdLote)
    {
        if (paisIdLote is > 0) return paisIdLote;
        var paisId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Departamentos.AsNoTracking(),
                f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
            .FirstOrDefaultAsync();
        return paisId;
    }

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
}
