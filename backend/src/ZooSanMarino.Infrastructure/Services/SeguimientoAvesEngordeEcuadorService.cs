// Servicio de seguimiento diario de aves engorde para Ecuador.
//
// Comparte la tabla `seguimiento_diario_aves_engorde` con el servicio Colombia
// (SeguimientoAvesEngordeService) pero usa flujo de inventario propio para Ecuador
// (inventario-gestion / item_inventario_ecuador). La lógica de descuento de alimento,
// recálculo de saldo y retiro de aves está portada del servicio original; ver
// fase_de_desarrollo/11_fix_seguimiento_ecuador_descuento_inventario.md.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoAvesEngordeEcuadorService : ISeguimientoAvesEngordeEcuadorService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly IInventarioGestionService? _inventarioGestionService;

    public SeguimientoAvesEngordeEcuadorService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        IMovimientoAvesService movimientoAvesService,
        IInventarioGestionService? inventarioGestionService = null)
    {
        _ctx = ctx;
        _current = current;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _movimientoAvesService = movimientoAvesService;
        _inventarioGestionService = inventarioGestionService;
    }

    // ─── Consultas estándar ───────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var entity = await _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : MapToDto(entity);
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

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(x => x.LoteAveEngordeId == loteId)
            .OrderBy(x => x.Fecha)
            .ToListAsync();

        var seguimientos = list.Select(MapToDto).ToList();
        var historico    = await QueryHistoricoUnificadoDtosAsync(loteId, companyId);

        return new SeguimientoAvesEngordePorLoteResponseDto(seguimientos, historico);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(
        int? loteId, DateTime? desde, DateTime? hasta)
    {
        var query = _ctx.SeguimientoDiarioAvesEngorde.AsNoTracking();
        if (loteId.HasValue) query = query.Where(x => x.LoteAveEngordeId == loteId.Value);
        if (desde.HasValue)  query = query.Where(x => x.Fecha >= desde.Value);
        if (hasta.HasValue)  query = query.Where(x => x.Fecha <= hasta.Value);
        var entities = await query.OrderBy(x => x.Fecha).ToListAsync();
        return entities.Select(MapToDto);
    }

    // ─── Resumen liquidación ─────────────────────────────────────────────────

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

        var ini = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId &&
                h.LoteAveEngordeId == loteId &&
                h.TipoLote == "LoteAveEngorde" &&
                h.TipoRegistro == "Inicio")
            .OrderBy(h => h.Id)
            .FirstOrDefaultAsync();

        var (hInicio, mInicio, xInicio) = LiquidacionEngordeCalculos.CalcularAvesInicio(
            ini != null, ini?.AvesHembras ?? 0, ini?.AvesMachos ?? 0, ini?.AvesMixtas ?? 0,
            lote.HembrasL, lote.MachosL, lote.Mixtas, lote.AvesEncasetadas);

        var totalInicio = hInicio + mInicio + xInicio;

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
        var avesVivas = LiquidacionEngordeCalculos.CalcularAvesVivas(totalInicio, bajas, vh + vm + vx);

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

    // ─── Tabla diaria via función SQL ────────────────────────────────────────

    public async Task<IReadOnlyList<SeguimientoDiarioTablaFilaDto>> GetTablaDiariaAsync(int loteId)
    {
        return await _ctx.Database
            .SqlQueryRaw<SeguimientoDiarioTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_engorde({0}::int)", loteId)
            .ToListAsync();
    }

    // ─── CRUD con afectación de inventario y recálculo de saldo ──────────────

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l =>
                l.LoteAveEngordeId == dto.LoteId
                && l.CompanyId == companyId
                && l.DeletedAt == null);
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

        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForEntity = MergeMetadataWithPatch(dto.Metadata, stockPatch);
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
            CreatedByUserId = dto.CreatedByUserId ?? _current?.UserId.ToString(),
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
                            lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(),
                            kv.Key, kv.Value, "kg", refStr, null));
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar consumo inventario (aves engorde Ecuador): {ex.Message}"); }
        }

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos
            + dto.SelH + dto.SelM
            + dto.ErrorSexajeHembras + dto.ErrorSexajeMachos;
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
                    observaciones: $"Aves de Engorde (Ecuador) - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | Error sexaje H: {dto.ErrorSexajeHembras}, M: {dto.ErrorSexajeMachos} | {dto.Observaciones}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde Ecuador: {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, companyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l =>
                l.LoteAveEngordeId == dto.LoteId
                && l.CompanyId == companyId
                && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede editar el registro.");

        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == dto.Id && l.CompanyId == companyId && l.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;
        if (ent.OrigenCruce)
            throw new InvalidOperationException(
                "Este registro se genera automáticamente desde los lotes reproductora (primeros 7 días). Para corregirlo, edite el seguimiento del lote reproductora.");

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
        var oldByItemId = ent.Metadata != null
            ? ParseMetadataItemsToKg(ent.Metadata.RootElement)
            : new Dictionary<int, decimal>();

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

        var historicoConsumoUpdate = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId, oldByItemId);
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForSave = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        ent.Metadata = CloneJsonDocument(metadataForSave);
        ent.ItemsAdicionales = CloneJsonDocument(dto.ItemsAdicionales);
        ent.HistoricoConsumoAlimento = CloneJsonDocument(historicoConsumoUpdate);
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        _ctx.Entry(ent).Property(e => e.HistoricoConsumoAlimento).IsModified = true;
        await _ctx.SaveChangesAsync();

        if (_inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0))
        {
            try
            {
                var newByItemId = dto.Metadata != null
                    ? ParseMetadataItemsToKg(dto.Metadata.RootElement)
                    : new Dictionary<int, decimal>();
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
                            farmId, nucleoId, galponId, itemId, -diff, "kg",
                            refStr + " (devolución)", "Devolución desde seguimiento aves engorde Ecuador"));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al actualizar inventario (aves engorde Ecuador): {ex.Message}"); }
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
                        observaciones: $"Aves de Engorde Ecuador (actualización) - ajuste retiro H:{deltaHRet}, M:{deltaMRet}");
                }

                if (deltaHRet < 0 || deltaMRet < 0)
                {
                    await DevolverAvesAlInventarioAsync(
                        dto.LoteId,
                        Math.Abs(Math.Min(0, deltaHRet)),
                        Math.Abs(Math.Min(0, deltaMRet)));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde Ecuador (actualización): {ex.Message}"); }
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
        if (ent.Seguimiento.OrigenCruce)
            throw new InvalidOperationException(
                "Este registro se genera automáticamente desde los lotes reproductora (primeros 7 días). No se puede eliminar manualmente.");
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
                            ent.GranjaId, ent.NucleoId?.Trim(), ent.GalponId?.Trim(),
                            kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento aves engorde Ecuador"));
            }
            catch (Exception ex) { Console.WriteLine($"Error al devolver inventario al eliminar seguimiento aves engorde Ecuador: {ex.Message}"); }
        }

        // Anular INV_CONSUMO huérfanos: si se borra un seguimiento, su INV_CONSUMO en el
        // histórico unificado debe quedar anulado para que un nuevo seguimiento en la misma
        // fecha no duplique consumoBodegaKg. La devolución INV_INGRESO ya revierte el stock.
        try
        {
            var refPrefix = $"Seguimiento aves engorde #{id}";
            var farmIdDel = ent.GranjaId;
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
        catch (Exception ex) { Console.WriteLine($"Error al anular INV_CONSUMO al eliminar seguimiento aves engorde Ecuador: {ex.Message}"); }

        var retH = (ent.Seguimiento.MortalidadHembras ?? 0) + (ent.Seguimiento.SelH ?? 0) + (ent.Seguimiento.ErrorSexajeHembras ?? 0);
        var retM = (ent.Seguimiento.MortalidadMachos ?? 0) + (ent.Seguimiento.SelM ?? 0) + (ent.Seguimiento.ErrorSexajeMachos ?? 0);
        if (retH > 0 || retM > 0)
        {
            try { await DevolverAvesAlInventarioAsync(ent.Seguimiento.LoteAveEngordeId, retH, retM); }
            catch (Exception ex) { Console.WriteLine($"Error al devolver aves al eliminar seguimiento engorde Ecuador: {ex.Message}"); }
        }

        var loteIdSeg = ent.Seguimiento.LoteAveEngordeId;
        _ctx.SeguimientoDiarioAvesEngorde.Remove(ent.Seguimiento);
        await _ctx.SaveChangesAsync();
        await RecalcularSaldoAlimentoPorLoteAsync(loteIdSeg, companyId);
        return true;
    }

    // ─── Histórico unificado ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<LoteRegistroHistoricoUnificadoDto>> QueryHistoricoUnificadoDtosAsync(
        int loteId, int companyId)
    {
        var loteInfo = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId })
            .SingleOrDefaultAsync();

        if (loteInfo is null)
            return Array.Empty<LoteRegistroHistoricoUnificadoDto>();

        int    farmId   = loteInfo.GranjaId;
        string nucleoId = (loteInfo.NucleoId ?? "").Trim();
        string galponId = (loteInfo.GalponId ?? "").Trim();

        var (fechaMinSeg, fechaMaxSeg) = await CalcularRangoFechasLoteAsync(loteId);

        var query = _ctx.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId
                && !h.Anulado
                && !((h.Referencia != null && h.Referencia.Contains("devolución por eliminación"))
                     || (h.Referencia != null && h.Referencia.Contains("devolucion por eliminacion")))
                && !(h.TipoEvento == "INV_INGRESO"
                     && h.Referencia != null
                     && h.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (
                    (h.TipoEvento == "VENTA_AVES" && h.LoteAveEngordeId == loteId)
                    ||
                    (h.TipoEvento != "VENTA_AVES"
                        && h.FarmId == farmId
                        && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoId
                        && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponId)
                ));

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

    // ─── Helpers de inventario y saldo (portados de SeguimientoAvesEngordeService) ───

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = kcalAlH is null ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = protAlH is null ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
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

    private async Task DevolverAvesAlInventarioAsync(int loteId, int hembras, int machos)
    {
        if (hembras <= 0 && machos <= 0) return;
        var inv = await _ctx.InventarioAves
            .Where(i => i.LoteId == loteId
                && i.CompanyId == _current.CompanyId
                && i.DeletedAt == null
                && i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();
        if (inv == null) return;
        inv.CantidadHembras += Math.Max(0, hembras);
        inv.CantidadMachos += Math.Max(0, machos);
        inv.FechaActualizacion = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    private static string FormatKg(decimal kg) => kg.ToString("0.###", CultureInfo.InvariantCulture);

    private static decimal ToKg(double cantidad, string? unidad)
    {
        var u = (unidad ?? "kg").Trim().ToLowerInvariant();
        if (u == "g" || u == "gramos" || u == "gramo") return (decimal)(cantidad / 1000.0);
        return (decimal)cantidad;
    }

    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
    {
        var byItemId = new Dictionary<int, decimal>();
        void Acumular(string propName)
        {
            if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;
            foreach (var e in arr.EnumerateArray())
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
        }
        Acumular("itemsHembras");
        Acumular("itemsMachos");
        return byItemId;
    }

    private static JsonDocument? MergeMetadataWithPatch(JsonDocument? existing, Dictionary<string, object?> patch)
    {
        if ((patch is null || patch.Count == 0) && existing is null) return null;
        if (patch is null || patch.Count == 0) return existing;
        Dictionary<string, object?> dict;
        if (existing != null)
            dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(existing.RootElement.GetRawText())
                ?? new Dictionary<string, object?>();
        else
            dict = new Dictionary<string, object?>();
        foreach (var kv in patch) dict[kv.Key] = kv.Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private static JsonDocument? CloneJsonDocument(JsonDocument? doc)
    {
        if (doc is null) return null;
        return JsonDocument.Parse(doc.RootElement.GetRawText());
    }

    private async Task<Dictionary<string, object?>> BuildStockMetadataPatchAsync(int loteId, DateTime fecha)
    {
        var day = fecha.Date;
        var companyId = _current.CompanyId;
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
                && !(x.TipoEvento == "INV_INGRESO"
                     && x.Referencia != null
                     && x.Referencia.StartsWith("Seguimiento aves engorde #"))
                && (x.TipoEvento == "INV_INGRESO"
                    || x.TipoEvento == "INV_TRASLADO_ENTRADA"
                    || x.TipoEvento == "VENTA_AVES"));

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

    private async Task<JsonDocument?> BuildHistoricoConsumoAlimentoAsync(
        JsonDocument? metadata,
        int farmId, string? nucleoId, string? galponId,
        Dictionary<int, decimal>? oldByItemId = null)
    {
        if (metadata is null) return null;
        var newByItemId = ParseMetadataItemsToKg(metadata.RootElement);
        if (newByItemId.Count == 0) return null;

        var itemIds = newByItemId.Keys.ToList();
        var catalogItems = await _ctx.ItemInventarioEcuador.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre);

        var nucleoIdN = (nucleoId ?? "").Trim();
        var galponIdN = (galponId ?? "").Trim();
        var stockByItem = await _ctx.InventarioGestionStock.AsNoTracking()
            .Where(s =>
                s.FarmId == farmId
                && (s.NucleoId == null ? "" : s.NucleoId.Trim()) == nucleoIdN
                && (s.GalponId == null ? "" : s.GalponId.Trim()) == galponIdN
                && itemIds.Contains(s.ItemInventarioEcuadorId))
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

    // ─── Recálculo de saldo de alimento del lote ───────────────────────────────
    // Replica RecalcularSaldoAlimentoPorLoteAsync de SeguimientoAvesEngordeService.
    // No duplica INV_CONSUMO del histórico (ya descontado en seguimiento); aplica
    // piso 0 después de cada evento. La función SQL fn_seguimiento_diario_engorde
    // ahora calcula el saldo dinámicamente (fix #10), pero seguimos persistiendo
    // para consumidores que leen la columna directamente.

    private readonly record struct SaldoAlimentoEvent(string Ymd, int Ord, long Tie, long? SegId, decimal Delta);

    private static string FormatYmd(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static long TsSeguimiento(SeguimientoDiarioAvesEngorde s)
    {
        var t = new DateTimeOffset(s.Fecha.Year, s.Fecha.Month, s.Fecha.Day, 12, 0, 0, TimeSpan.Zero);
        return t.ToUnixTimeMilliseconds();
    }

    private static long TsHistorico(LoteRegistroHistoricoUnificado h) =>
        h.CreatedAt.ToUnixTimeMilliseconds();

    private static string? YmdHistoricoEfectivo(LoteRegistroHistoricoUnificado h)
        => FormatYmd(h.FechaOperacion);

    private static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
    {
        delta = 0;
        ord = 0;
        if (h.Anulado) return false;
        var kg = h.CantidadKg ?? 0;
        switch (h.TipoEvento)
        {
            case "INV_INGRESO":
                if (kg == 0) return false;
                delta = kg; ord = 0; return true;
            case "INV_TRASLADO_ENTRADA":
                if (kg == 0) return false;
                delta = kg; ord = 1; return true;
            case "INV_TRASLADO_SALIDA":
                if (kg == 0) return false;
                delta = -Math.Abs(kg); ord = 2; return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// ⚠️ FIX #12 (2026-05-28): si <paramref name="fechaEncaset"/> se proporciona, los movimientos
    /// anteriores al encaset se ignoran (galpón se considera "limpio"). Antes la apertura heredaba
    /// inventario residual del lote previo del mismo galpón.
    /// </summary>
    private static decimal ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
        IReadOnlyList<LoteRegistroHistoricoUnificado> hist,
        DateTime firstSegDate,
        DateTime? fechaEncaset = null)
    {
        var firstYmd = FormatYmd(firstSegDate.Date);
        var encasetYmd = fechaEncaset.HasValue ? FormatYmd(fechaEncaset.Value.Date) : null;
        var rows = new List<(string ymd, long ts, decimal delta)>();
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) >= 0) continue;
            if (encasetYmd is not null && string.Compare(ymd, encasetYmd, StringComparison.Ordinal) < 0) continue;
            if (!TryGetHistDeltaAndOrd(h, out var d, out _)) continue;
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

    private async Task RecalcularSaldoAlimentoPorLoteAsync(int loteId, int companyId, CancellationToken ct = default)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.FechaEncaset, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return;

        var farmId = lote.GranjaId;
        var nucleoId = (lote.NucleoId ?? "").Trim();
        var galponId = (lote.GalponId ?? "").Trim();

        var hist = await _ctx.LoteRegistroHistoricoUnificados.AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && !h.Anulado
                && h.TipoEvento != "VENTA_AVES"
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
        if (segs.Count == 0) return;

        var firstSegDate = segs.Min(s => s.Fecha.Date);
        var encYmd = lote.FechaEncaset.HasValue ? FormatYmd(lote.FechaEncaset.Value.Date) : null;
        var firstYmd = FormatYmd(firstSegDate);
        var opening = ComputeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegDate, lote.FechaEncaset);

        var events = new List<SaldoAlimentoEvent>(hist.Count + segs.Count);
        foreach (var h in hist)
        {
            var ymd = YmdHistoricoEfectivo(h);
            if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) < 0) continue;
            if (encYmd is not null && string.Compare(ymd, encYmd, StringComparison.Ordinal) < 0) continue;
            if (!TryGetHistDeltaAndOrd(h, out var delta, out var ord)) continue;
            events.Add(new SaldoAlimentoEvent(ymd, ord, TsHistorico(h), null, delta));
        }
        foreach (var s in segs)
        {
            var ymd = FormatYmd(s.Fecha.Date);
            var ch = s.ConsumoKgHembras ?? 0;
            var cm = s.ConsumoKgMachos ?? 0;
            events.Add(new SaldoAlimentoEvent(ymd, 3, TsSeguimiento(s), s.Id, -(ch + cm)));
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
            if (e.SegId.HasValue) saldoPorSegId[e.SegId.Value] = bal;
        }
        foreach (var s in segs)
            s.SaldoAlimentoKg = saldoPorSegId.TryGetValue(s.Id, out var sal) ? sal : bal;
        await _ctx.SaveChangesAsync(ct);
    }
}