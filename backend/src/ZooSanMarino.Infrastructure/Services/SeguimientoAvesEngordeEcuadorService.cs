// Partial 'ancla' del seguimiento diario de aves engorde para Ecuador: usings, campos, ctor,
// helpers compartidos (país del lote, mappers, delegadores a la aritmética pura, hembras vivas y
// rango de fechas del lote) y la interfaz. La implementación vive repartida por responsabilidad en
// 'SeguimientoAvesEngordeEcuador/Funciones/' (Consultas, Crud, SaldoAlimento). Namespace plano →
// misma DI, misma interfaz, mismo comportamiento.
//
// Comparte la tabla `seguimiento_diario_aves_engorde` con el servicio Colombia
// (SeguimientoAvesEngordeService) pero usa flujo de inventario propio para Ecuador
// (inventario-gestion / item_inventario_ecuador). La lógica de descuento de alimento,
// recálculo de saldo y retiro de aves está portada del servicio original; ver
// fase_de_desarrollo/11_fix_seguimiento_ecuador_descuento_inventario.md.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeEcuadorService : ISeguimientoAvesEngordeEcuadorService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly IInventarioGestionService? _inventarioGestionService;
    private readonly IColombiaInventarioConsumoService? _colombiaConsumoB;  // Fase 3 paso 2: modelo B nivel granja (Colombia) — defensivo si un lote Colombia entra por este servicio
    private readonly ILogger<SeguimientoAvesEngordeEcuadorService>? _logger;

    public SeguimientoAvesEngordeEcuadorService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        IMovimientoAvesService movimientoAvesService,
        IInventarioGestionService? inventarioGestionService = null,
        IColombiaInventarioConsumoService? colombiaConsumoB = null,
        ILogger<SeguimientoAvesEngordeEcuadorService>? logger = null)
    {
        _ctx = ctx;
        _current = current;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _movimientoAvesService = movimientoAvesService;
        _inventarioGestionService = inventarioGestionService;
        _colombiaConsumoB = colombiaConsumoB;
        _logger = logger;
    }

    /// <summary>
    /// País efectivo del lote (lote_ave_engorde) para gatear el descuento del inventario modelo B.
    /// Fuente: <c>lote.PaisId</c> si está poblado; si no, derivado desde la granja
    /// (farm.DepartamentoId → departamentos.PaisId), la misma cadena que usa el inventario.
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

    // ─── Mappers ─────────────────────────────────────────────────────────────

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

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioAvesEngorde e) =>
        new(
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
            HistoricoConsumoAlimento: e.HistoricoConsumoAlimento,
            OrigenCruce: e.OrigenCruce);

    // ─── Delegadores a la aritmética pura y helpers compartidos ──────────────

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
        => SeguimientoEngordeCalculos.CalcularSemana(fechaEncaset, fechaRegistro);

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
        => SeguimientoEngordeCalculos.CalcularDerivados(consumoKgHembras, kcalAlH, protAlH);

    private static string FormatKg(decimal kg) => kg.ToString("0.###", CultureInfo.InvariantCulture);

    private static decimal ToKg(double cantidad, string? unidad)
        => MetadataEngordeCalculos.ToKg(cantidad, unidad);

    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
        => MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

    /// <summary>
    /// Variante TIPADA del parseo (conserva el origen del id — camino 1/2) para las ramas
    /// Colombia (IColombiaInventarioConsumoService), donde catalogItemId e
    /// itemInventarioEcuadorId conviven y sus rangos numéricos colisionan.
    /// </summary>
    private static Dictionary<ItemConsumoKey, decimal> ParseMetadataItemsToKgPorOrigen(JsonElement root)
        => MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(root);

    private static JsonDocument? MergeMetadataWithPatch(JsonDocument? existing, Dictionary<string, object?> patch)
        => MetadataEngordeCalculos.MergeMetadataWithPatch(existing, patch);

    private static JsonDocument? CloneJsonDocument(JsonDocument? doc)
    {
        if (doc is null) return null;
        return JsonDocument.Parse(doc.RootElement.GetRawText());
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
        return Math.Max(0, baseH - mortCajaH - mort - sel - err);
    }

    private async Task<(DateTime?, DateTime?)> CalcularRangoFechasLoteAsync(int loteId)
    {
        var segFechas = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .Select(s => s.Fecha)
            .ToListAsync();

        return segFechas.Count == 0
            ? (null, null)
            : (segFechas.Min(), segFechas.Max());
    }
}
