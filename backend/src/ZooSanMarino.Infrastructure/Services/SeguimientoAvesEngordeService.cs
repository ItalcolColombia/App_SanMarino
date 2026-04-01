// Seguimiento Diario Aves de Engorde: persiste en tabla seguimiento_diario_aves_engorde (FK a lote_ave_engorde).
// Filtros del módulo muestran lotes de lote_ave_engorde. DTO mantiene LoteId = lote_ave_engorde_id para el front.
//
// Inventario nuevo (inventario-gestion / item_inventario_ecuador): este módulo es el único que aplica consumo
// y devolución sobre el inventario nuevo. El módulo Seguimiento diario postura (ProduccionService) no usa
// inventario-gestion; los dos módulos de inventario están divididos (postura → su inventario; pollo engorde → inventario-gestion).
using System.Text.Json;
using System.Globalization;
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
            SaldoAlimentoKg: e.SaldoAlimentoKg.HasValue ? (double)e.SaldoAlimentoKg.Value : null
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

        var h0 = lote.HembrasL ?? 0;
        var m0 = lote.MachosL ?? 0;
        var x0 = lote.Mixtas ?? 0;
        var total = h0 + m0 + x0;
        if (total == 0 && lote.AvesEncasetadas.HasValue && lote.AvesEncasetadas.Value > 0)
            total = lote.AvesEncasetadas.Value;

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
        var avesVivas = Math.Max(0, total - bajas - (vh + vm + vx));

        return new LiquidacionLoteEngordeResumenDto(
            lote.LoteAveEngordeId ?? loteId,
            lote.LoteNombre ?? "",
            lote.EstadoOperativoLote ?? "Abierto",
            lote.HembrasL,
            lote.MachosL,
            lote.Mixtas,
            total,
            vh,
            vm,
            vx,
            avesVivas,
            ventas.Count,
            saldo);
    }

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(int loteId, int companyId)
    {
        var rows = await _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.LoteAveEngordeId == loteId && h.CompanyId == companyId && !h.Anulado)
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync();

        return rows.Select(MapHistoricoUnificado).ToList();
    }

    /// <summary>
    /// Saldo al cierre de cada día = acumulado de movimientos de inventario (kg) en orden cronológico.
    /// INV_INGRESO / INV_TRASLADO_ENTRADA suman; INV_TRASLADO_SALIDA e INV_CONSUMO restan.
    /// </summary>
    private static decimal DeltaInventarioKg(LoteRegistroHistoricoUnificado h)
    {
        var kg = h.CantidadKg ?? 0;
        return h.TipoEvento switch
        {
            "INV_INGRESO" => kg,
            "INV_TRASLADO_ENTRADA" => kg,
            "INV_TRASLADO_SALIDA" => -kg,
            "INV_CONSUMO" => -kg,
            _ => 0m
        };
    }

    /// <summary>
    /// Recalcula y persiste <see cref="SeguimientoDiarioAvesEngorde.SaldoAlimentoKg"/> para todos los registros diarios del lote.
    /// </summary>
    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        var hist = await _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.LoteAveEngordeId == loteId && h.CompanyId == companyId && !h.Anulado)
            .OrderBy(h => h.FechaOperacion)
            .ThenBy(h => h.Id)
            .ToListAsync(ct);

        var segs = await _ctx.SeguimientoDiarioAvesEngorde
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ToListAsync(ct);

        if (segs.Count == 0)
            return;

        var minD = segs.Min(s => s.Fecha.Date);
        var maxD = segs.Max(s => s.Fecha.Date);
        if (hist.Count > 0)
        {
            minD = new[] { minD, hist.Min(h => h.FechaOperacion.Date) }.Min();
            maxD = new[] { maxD, hist.Max(h => h.FechaOperacion.Date) }.Max();
        }

        var balanceEndOfDay = new Dictionary<DateTime, decimal>();
        decimal running = 0;
        for (var d = minD.Date; d <= maxD.Date; d = d.AddDays(1))
        {
            foreach (var ev in hist.Where(h => h.FechaOperacion.Date == d).OrderBy(h => h.Id))
                running += DeltaInventarioKg(ev);
            balanceEndOfDay[d] = running;
        }

        foreach (var s in segs)
        {
            var d = s.Fecha.Date;
            s.SaldoAlimentoKg = balanceEndOfDay.TryGetValue(d, out var b) ? b : running;
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
            UpdatedAt = null
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

        // Patch de metadata (Ingreso/Traslado/Documento/Despacho) desde histórico unificado.
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForSave = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        // jsonb + JsonDocument: forzar persistencia; si no, EF puede no marcar Metadata como modificado y el inventario sí aplica el diff desde dto.Metadata.
        ent.Metadata = CloneJsonDocument(metadataForSave);
        ent.ItemsAdicionales = CloneJsonDocument(dto.ItemsAdicionales);
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
        // Reforzar persistencia de todas las columnas escalares (además de jsonb).
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
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
    /// </summary>
    private async Task<Dictionary<string, object?>> BuildStockMetadataPatchAsync(int loteId, DateTime fecha)
    {
        var day = fecha.Date;

        var agg = await _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == _current.CompanyId
                && x.LoteAveEngordeId == loteId
                && x.FechaOperacion == day
                && !x.Anulado
                && (x.TipoEvento == "INV_INGRESO"
                    || x.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || x.TipoEvento == "VENTA_AVES"))
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
}
