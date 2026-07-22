// Seguimiento diario por lote reproductora aves de engorde. Persiste en seguimiento_diario_lote_reproductora_aves_engorde.
// DTO reutiliza SeguimientoLoteLevanteDto con LoteId = lote_reproductora_ave_engorde_id.
// Inventario: mismo patrón que SeguimientoAvesEngordeService — descuenta al crear, ajusta al editar, restituye al eliminar.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoDiarioLoteReproductoraService : ISeguimientoDiarioLoteReproductoraService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IInventarioGestionService? _inventarioGestionService;

    public SeguimientoDiarioLoteReproductoraService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        IInventarioGestionService? inventarioGestionService = null)
    {
        _ctx = ctx;
        _current = current;
        _inventarioGestionService = inventarioGestionService;
    }

    private static SeguimientoLoteLevanteDto MapToDto(SeguimientoDiarioLoteReproductoraAvesEngorde e)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)e.Id,
            LoteId: e.LoteReproductoraAveEngordeId,
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
            SaldoAlimentoKg: null,
            QqMixtas: e.QqMixtas,
            QqHembras: e.QqHembras,
            QqMachos: e.QqMachos
        );
    }

    // ─── Helpers de inventario ────────────────────────────────────────────────

    /// <summary>
    /// Parsea itemsHembras/Machos/Generales de la metadata → mapa itemId → kg total.
    /// Delega en el cálculo puro central compartido (un solo lugar → un solo test).
    /// Antes había una copia idéntica acá + su propio ToKg.
    /// </summary>
    private static Dictionary<int, decimal> ParseMetadataItemsToKg(JsonElement root)
        => ZooSanMarino.Application.Calculos.MetadataEngordeCalculos.ParseMetadataItemsToKg(root);

    /// <summary>
    /// Obtiene farmId, nucleoId y galponId trazando LoteReproductora → LoteAveEngorde.
    /// </summary>
    private async Task<(int FarmId, string? NucleoId, string? GalponId)?> GetLoteUbicacionAsync(int loteReproductoraId)
    {
        var row = await (
            from lr in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            join lae in _ctx.LoteAveEngorde.AsNoTracking() on lr.LoteAveEngordeId equals lae.LoteAveEngordeId
            where lr.Id == loteReproductoraId
            select new { lae.GranjaId, lae.NucleoId, lae.GalponId }
        ).FirstOrDefaultAsync();

        if (row is null) return null;
        return (row.GranjaId, row.NucleoId, row.GalponId);
    }

    /// <summary>
    /// País efectivo para gatear el descuento del inventario modelo B (S1). Aquí el origen es la
    /// GRANJA del lote reproductora (no hay lote.PaisId): farm.DepartamentoId → departamentos.PaisId,
    /// la misma cadena que usa el inventario. Devuelve null si no se puede resolver.
    /// </summary>
    private async Task<int?> ResolverPaisIdPorGranjaAsync(int granjaId)
        => await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Join(_ctx.Departamentos.AsNoTracking(),
                f => f.DepartamentoId, d => d.DepartamentoId, (f, d) => (int?)d.PaisId)
            .FirstOrDefaultAsync();

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteReproductoraAsync(int loteReproductoraId)
    {
        var companyId = _current.CompanyId;
        var exists = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                           join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                           where l.Id == loteReproductoraId && lae.CompanyId == companyId && lae.DeletedAt == null
                           select 1).AnyAsync();
        if (!exists) return Array.Empty<SeguimientoLoteLevanteDto>();

        var list = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteReproductoraAveEngordeId == loteReproductoraId)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
        return list.Select(MapToDto);
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var companyId = _current.CompanyId;
        var e = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                       join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                       join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                       where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                       select s).SingleOrDefaultAsync();
        return e is null ? null : MapToDto(e);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteReproductoraId, DateTime? desde, DateTime? hasta)
    {
        var companyId = _current.CompanyId;
        // Rango por DÍA completo en UTC: las fechas se guardan ancladas a mediodía UTC
        // (FechasPuras), así que un "hasta" a medianoche excluiría los registros de ese día.
        var desdeUtc = FechasPuras.AnclarMediodiaUtc(desde)?.AddHours(-12);
        var hastaExcl = FechasPuras.AnclarMediodiaUtc(hasta)?.AddHours(12);
        var q = from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                where lae.CompanyId == companyId && lae.DeletedAt == null
                   && (!loteReproductoraId.HasValue || s.LoteReproductoraAveEngordeId == loteReproductoraId.Value)
                   && (!desdeUtc.HasValue || s.Fecha >= desdeUtc.Value)
                   && (!hastaExcl.HasValue || s.Fecha < hastaExcl.Value)
                orderby s.Fecha
                select s;
        var list = await q.ToListAsync();
        return list.Select(MapToDto);
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var loteRep = await (from l in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                             join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                             where l.Id == dto.LoteId && lae.CompanyId == companyId && lae.DeletedAt == null
                             select l).SingleOrDefaultAsync();
        if (loteRep is null)
            throw new InvalidOperationException($"Lote reproductora aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Regla: máximo 7 días de seguimiento por lote reproductora
        const int MaxDiasSeguimiento = 7;
        var totalRegistros = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
            .CountAsync(s => s.LoteReproductoraAveEngordeId == dto.LoteId);
        if (totalRegistros >= MaxDiasSeguimiento)
            throw new InvalidOperationException(
                $"Este lote reproductora ya tiene {totalRegistros} días de seguimiento registrados. El máximo permitido es {MaxDiasSeguimiento}.");

        var ent = new SeguimientoDiarioLoteReproductoraAvesEngorde
        {
            LoteReproductoraAveEngordeId = dto.LoteId,
            Fecha = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro),
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = (decimal)dto.ConsumoKgHembras,
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
            KcalAlH = dto.KcalAlH,
            ProtAlH = dto.ProtAlH,
            KcalAveH = dto.KcalAveH,
            ProtAveH = dto.ProtAveH,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            // Panamá: quintales por categoría (el DTO ya los traía; antes se descartaban al persistir)
            QqMixtas = dto.QqMixtas,
            QqHembras = dto.QqHembras,
            QqMachos = dto.QqMachos
        };
        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Add(ent);
        await _ctx.SaveChangesAsync();

        // Descontar inventario por ítems consumidos
        if (_inventarioGestionService != null && dto.Metadata != null)
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(dto.LoteId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá descuentan del modelo B; para lotes Colombia
                // NO se invoca (evita el descuento cross-país silencioso por el fallback catalogItemId).
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                    var refStr = $"Seguimiento reproductora #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al registrar consumo inventario (reproductora): {ex.Message}");
            }
        }

        return MapToDto(ent);
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == dto.Id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;

        // Capturar ítems anteriores antes de actualizar
        var oldByItemId = ent.Metadata != null
            ? ParseMetadataItemsToKg(ent.Metadata.RootElement)
            : new Dictionary<int, decimal>();

        ent.Fecha = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro);
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = (decimal)dto.ConsumoKgHembras;
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
        // Panamá: quintales por categoría (espejo del Create).
        ent.QqMixtas = dto.QqMixtas;
        ent.QqHembras = dto.QqHembras;
        ent.QqMachos = dto.QqMachos;
        ent.Metadata = dto.Metadata;
        ent.ItemsAdicionales = dto.ItemsAdicionales;
        ent.KcalAlH = dto.KcalAlH;
        ent.ProtAlH = dto.ProtAlH;
        ent.KcalAveH = dto.KcalAveH;
        ent.ProtAveH = dto.ProtAveH;
        ent.UpdatedAt = DateTime.UtcNow;
        // La entidad se cargó con una query con joins AsNoTracking → NO queda rastreada,
        // por lo que asignar propiedades no emite UPDATE. Forzar el estado Modified para
        // persistir TODAS las columnas (incl. fecha y jsonb) y disparar el trigger de cruce.
        // Mismo patrón que SeguimientoAvesEngordeService.UpdateAsync.
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        await _ctx.SaveChangesAsync();

        // Ajustar inventario: consumir diferencia positiva, devolver diferencia negativa
        if (_inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0))
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(dto.LoteId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá ajustan el modelo B.
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var newByItemId = dto.Metadata != null
                        ? ParseMetadataItemsToKg(dto.Metadata.RootElement)
                        : new Dictionary<int, decimal>();
                    var allItemIds = new HashSet<int>(oldByItemId.Keys);
                    foreach (var k in newByItemId.Keys) allItemIds.Add(k);
                    var refStr = $"Seguimiento reproductora #{dto.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var itemId in allItemIds)
                    {
                        var newQty = newByItemId.GetValueOrDefault(itemId);
                        var oldQty = oldByItemId.GetValueOrDefault(itemId);
                        var diff = newQty - oldQty;
                        if (diff > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), itemId, diff, "kg", refStr + " (ajuste)", null));
                        else if (diff < 0)
                            await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento reproductora"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar inventario (reproductora): {ex.Message}");
            }
        }

        return MapToDto(ent);
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                         join l in _ctx.LoteReproductoraAveEngorde.AsNoTracking() on s.LoteReproductoraAveEngordeId equals l.Id
                         join lae in _ctx.LoteAveEngorde.AsNoTracking() on l.LoteAveEngordeId equals lae.LoteAveEngordeId
                         where s.Id == id && lae.CompanyId == companyId && lae.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return false;

        // ── Guard de cierre: un lote cerrado solo permite eliminar si fue reabierto con novedad ──
        const int MaxDiasSeguimiento = 7;
        var loteRep = await _ctx.LoteReproductoraAveEngorde
            .SingleOrDefaultAsync(l => l.Id == ent.LoteReproductoraAveEngordeId);
        if (loteRep is not null)
        {
            var numRegistros = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                .CountAsync(s => s.LoteReproductoraAveEngordeId == loteRep.Id);
            var bajas = await _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                .Where(s => s.LoteReproductoraAveEngordeId == loteRep.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Mort = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0)),
                    Sel = g.Sum(s => (s.SelH ?? 0) + (s.SelM ?? 0)),
                    Err = g.Sum(s => (s.ErrorSexajeHembras ?? 0) + (s.ErrorSexajeMachos ?? 0))
                })
                .SingleOrDefaultAsync();
            var ventas = await _ctx.MovimientoPolloEngorde
                .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null
                    && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                    && m.LoteReproductoraAveEngordeOrigenId == loteRep.Id)
                .SumAsync(m => (int?)(m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas)) ?? 0;

            var encaset = (loteRep.AvesInicioHembras ?? loteRep.H ?? 0)
                        + (loteRep.AvesInicioMachos ?? loteRep.M ?? 0)
                        + (loteRep.Mixtas ?? 0);
            var avesActuales = Math.Max(0, encaset - (bajas?.Mort ?? 0) - (bajas?.Sel ?? 0) - (bajas?.Err ?? 0) - ventas);
            var cerrado = avesActuales <= 0 || numRegistros >= MaxDiasSeguimiento;

            if (cerrado && !loteRep.Reabierto)
                throw new InvalidOperationException(
                    "El lote reproductora está cerrado. Reábralo con una novedad para poder eliminar registros.");
        }

        // Restituir stock antes de eliminar
        if (_inventarioGestionService != null && ent.Metadata != null)
        {
            try
            {
                var ubicacion = await GetLoteUbicacionAsync(ent.LoteReproductoraAveEngordeId);
                // Gate por PAÍS (S1): solo Ecuador/Panamá devuelven al modelo B.
                if (ubicacion.HasValue &&
                    InventarioConsumoGate.DebeDescontarModeloB(await ResolverPaisIdPorGranjaAsync(ubicacion.Value.FarmId)))
                {
                    var (farmId, nucleoId, galponId) = ubicacion.Value;
                    var byItem = ParseMetadataItemsToKg(ent.Metadata.RootElement);
                    var refStr = $"Seguimiento reproductora #{id} (devolución por eliminación)";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                                farmId, nucleoId?.Trim(), galponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento reproductora"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al devolver inventario al eliminar seguimiento reproductora: {ex.Message}");
            }
        }

        _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.Remove(ent);

        // "Recierra solo": al eliminar se consume la reapertura; el estado se recalcula.
        // Se conserva novedad_apertura / reabierto_at como histórico del último motivo.
        if (loteRep is not null && loteRep.Reabierto)
        {
            loteRep.Reabierto = false;
            loteRep.UpdatedAt = DateTime.UtcNow;
        }

        await _ctx.SaveChangesAsync();
        return true;
    }
}
