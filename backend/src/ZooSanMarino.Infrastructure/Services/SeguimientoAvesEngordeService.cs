// Seguimiento Diario Aves de Engorde: persiste en tabla seguimiento_diario_aves_engorde (FK a lote_ave_engorde).
// Filtros del módulo muestran lotes de lote_ave_engorde. DTO mantiene LoteId = lote_ave_engorde_id para el front.
//
// Inventario nuevo (inventario-gestion / item_inventario_ecuador): este módulo es el único que aplica consumo
// y devolución sobre el inventario nuevo. El módulo Seguimiento diario postura (ProduccionService) no usa
// inventario-gestion; los dos módulos de inventario están divididos (postura → su inventario; pollo engorde → inventario-gestion).
using System.Text.Json;
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
            CreatedByUserId: e.CreatedByUserId
        );
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId)
    {
        var companyId = _current.CompanyId;
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists) return Array.Empty<SeguimientoLoteLevanteDto>();

        var list = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        return list.Select(MapToDto);
    }

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
            Metadata = dto.Metadata,
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

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                    machosRetirados: dto.MortalidadMachos + dto.SelM,
                    mixtasRetiradas: 0,
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Engorde",
                    observaciones: $"Aves de Engorde - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | {dto.Observaciones}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde: {ex.Message}"); }
        }

        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == dto.LoteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");

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

        ent.Metadata = dto.Metadata;
        ent.ItemsAdicionales = dto.ItemsAdicionales;
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
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

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                    machosRetirados: dto.MortalidadMachos + dto.SelM,
                    mixtasRetiradas: 0,
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Engorde",
                    observaciones: $"Aves de Engorde (actualización) - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde (actualización): {ex.Message}"); }
        }

        return MapToDto(ent);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                         select new { Seguimiento = s, l.GranjaId, l.NucleoId, l.GalponId }).SingleOrDefaultAsync();
        if (ent is null) return false;

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

        _ctx.SeguimientoDiarioAvesEngorde.Remove(ent.Seguimiento);
        await _ctx.SaveChangesAsync();
        return true;
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
}
