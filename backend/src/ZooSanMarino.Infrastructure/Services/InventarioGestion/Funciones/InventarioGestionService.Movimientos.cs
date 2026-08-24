// src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Movimientos.cs
// Listado de movimientos de inventario (ingreso/consumo/traslado/ajuste) con sus filtros.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Exceptions;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class InventarioGestionService
{
    public async Task<List<InventarioGestionMovimientoDto>> GetMovimientosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? estado = null,
        string? movementType = null,
        string? nucleoId = null,
        string? galponId = null,
        int? loteId = null,
        string? search = null,
        string? concepto = null,
        string? tipoItem = null,
        string? tipoOperacion = null,
        string? unit = null,
        string? referenceContains = null,
        string? reasonContains = null,
        string? transferGroupId = null,
        int? itemInventarioEcuadorId = null,
        int? fromFarmId = null,
        string? fromNucleoId = null,
        string? fromGalponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionMovimientoDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionMovimientoDto>();

        if (fromFarmId.HasValue && !allowedFarmIds.Contains(fromFarmId.Value))
            return new List<InventarioGestionMovimientoDto>();

        if (!fromFarmId.HasValue && (!string.IsNullOrWhiteSpace(fromNucleoId) || !string.IsNullOrWhiteSpace(fromGalponId)))
            return new List<InventarioGestionMovimientoDto>();

        int? farmFilter = farmId;
        string? nucleoFilter = string.IsNullOrWhiteSpace(nucleoId) ? null : nucleoId.Trim();
        string? galponFilter = string.IsNullOrWhiteSpace(galponId) ? null : galponId.Trim();

        if (loteId.HasValue && loteId.Value > 0)
        {
            var lote = await _db.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && l.CompanyId == companyId.Value && l.DeletedAt == null, ct);
            if (lote == null || !allowedFarmIds.Contains(lote.GranjaId))
                return new List<InventarioGestionMovimientoDto>();

            farmFilter = lote.GranjaId;
            nucleoFilter = string.IsNullOrWhiteSpace(lote.NucleoId) ? null : lote.NucleoId.Trim();
            galponFilter = string.IsNullOrWhiteSpace(lote.GalponId) ? null : lote.GalponId.Trim();
        }

        if (farmFilter.HasValue && !allowedFarmIds.Contains(farmFilter.Value))
            return new List<InventarioGestionMovimientoDto>();

        var ubicacionPorLote = loteId.HasValue && loteId.Value > 0;
        if (!ubicacionPorLote && !farmFilter.HasValue &&
            (!string.IsNullOrWhiteSpace(nucleoFilter) || !string.IsNullOrWhiteSpace(galponFilter)))
            return new List<InventarioGestionMovimientoDto>();

        var paisId = farmFilter.HasValue ? await GetEffectivePaisIdAsync(farmFilter, ct) : 0;

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && allowedFarmIds.Contains(x.FarmId));
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmFilter.HasValue) query = query.Where(x => x.FarmId == farmFilter.Value);

        if (loteId.HasValue && loteId.Value > 0)
            ApplyUbicacionMovimientoFilter(ref query, nucleoFilter, galponFilter);
        else
        {
            if (!string.IsNullOrWhiteSpace(nucleoFilter))
                query = query.Where(x => x.NucleoId == nucleoFilter);
            if (!string.IsNullOrWhiteSpace(galponFilter))
                query = query.Where(x => x.GalponId == galponFilter);
        }

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(x => x.Estado != null && x.Estado.Trim() == estado.Trim());
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(x => x.MovementType == movementType.Trim());

        if (!string.IsNullOrWhiteSpace(tipoOperacion))
        {
            var resolved = ResolveMovementTypeFromTipoOperacionLabel(tipoOperacion.Trim());
            if (resolved != null)
                query = query.Where(x => x.MovementType == resolved);
            else
                return new List<InventarioGestionMovimientoDto>();
        }

        if (!string.IsNullOrWhiteSpace(concepto))
        {
            var c = concepto.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemInventario.Concepto != null &&
                x.ItemInventario.Concepto.Trim().ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(tipoItem))
        {
            var t = tipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemInventario.TipoItem != null &&
                x.ItemInventario.TipoItem.Trim().ToLower() == t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            var u = unit.Trim().ToLowerInvariant();
            query = query.Where(x => x.Unit != null && x.Unit.Trim().ToLower() == u);
        }

        if (itemInventarioEcuadorId.HasValue && itemInventarioEcuadorId.Value > 0)
            query = query.Where(x => x.ItemInventarioEcuadorId == itemInventarioEcuadorId.Value);

        if (!string.IsNullOrWhiteSpace(referenceContains))
        {
            var r = referenceContains.Trim().ToLowerInvariant();
            query = query.Where(x => x.Reference != null && x.Reference.ToLower().Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(reasonContains))
        {
            var r = reasonContains.Trim().ToLowerInvariant();
            query = query.Where(x => x.Reason != null && x.Reason.ToLower().Contains(r));
        }

        if (!string.IsNullOrWhiteSpace(transferGroupId))
        {
            var tg = transferGroupId.Trim();
            if (!Guid.TryParse(tg, out var gid))
                return new List<InventarioGestionMovimientoDto>();
            query = query.Where(x => x.TransferGroupId == gid);
        }

        if (fromFarmId.HasValue)
        {
            query = query.Where(x => x.FromFarmId == fromFarmId.Value);
            if (!string.IsNullOrWhiteSpace(fromNucleoId))
            {
                var fn = fromNucleoId.Trim();
                query = query.Where(x => x.FromNucleoId == fn);
            }

            if (!string.IsNullOrWhiteSpace(fromGalponId))
            {
                var fg = fromGalponId.Trim();
                query = query.Where(x => x.FromGalponId == fg);
            }
        }

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(3000)
            .ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);
        var silosMov = await NombresDeSilosAsync(list.SelectMany(x => new[] { x.SiloId, x.FromSiloId }), ct);

        var fromFarmIds = list.Where(x => x.FromFarmId.HasValue).Select(x => x.FromFarmId!.Value).Distinct().ToList();
        var fromFarmNames = fromFarmIds.Count > 0
            ? await _db.Farms.AsNoTracking().Where(f => fromFarmIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct)
            : new Dictionary<int, string>();

        var fromNucleoIds = list.Where(x => x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromNucleoId)).Select(x => x.FromNucleoId!).Distinct().ToList();
        var fromNucleoRows = fromFarmIds.Count > 0 && fromNucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking().Where(n => fromFarmIds.Contains(n.GranjaId) && fromNucleoIds.Contains(n.NucleoId)).ToListAsync(ct)
            : new List<Nucleo>();
        var fromNucleoDict = fromNucleoRows.ToDictionary(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre);

        var fromGalponIds = list.Where(x => x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromGalponId)).Select(x => x.FromGalponId!).Distinct().ToList();
        var fromGalponRows = fromFarmIds.Count > 0 && fromGalponIds.Count > 0
            ? await _db.Galpones.AsNoTracking().Where(g => fromFarmIds.Contains(g.GranjaId) && fromGalponIds.Contains(g.GalponId)).ToListAsync(ct)
            : new List<Galpon>();
        var fromGalponDict = fromGalponRows.ToDictionary(g => (g.GalponId, g.GranjaId), g => g.GalponNombre);

        return list.Select(x =>
        {
            var estadoDisplay = x.Estado;
            if (string.IsNullOrWhiteSpace(estadoDisplay))
            {
                estadoDisplay = x.MovementType switch
                {
                    "Ingreso" => (x.Reason != null && x.Reason.Contains("Llegada a planta", StringComparison.OrdinalIgnoreCase)) ? "Entrada planta" : "Entrada granja",
                    "TrasladoEntrada" or "TrasladoSalida" => "Transferencia a granja",
                    "TrasladoInterGranjaPendiente" => "Pendiente destino",
                    "TrasladoInterGranjaSalida" => "Tránsito",
                    "TrasladoInterGranjaEntrada" => "Recibido desde tránsito",
                    "TrasladoInterGranjaRechazado" => "Rechazado destino",
                    "Consumo" => "Consumo",
                    "AjusteStock" => "Ajuste manual",
                    "EliminacionStock" => "Eliminación registro",
                    _ => x.MovementType
                };
            }
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemType = x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento";

            string? fromGranjaNombre = null;
            string? fromNucleoNombre = null;
            string? fromGalponNombre = null;
            if (x.FromFarmId.HasValue && fromFarmNames.TryGetValue(x.FromFarmId.Value, out var fName))
                fromGranjaNombre = fName;
            if (x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromNucleoId) && fromNucleoDict.TryGetValue((x.FromNucleoId, x.FromFarmId.Value), out var fnn))
                fromNucleoNombre = fnn;
            if (x.FromFarmId.HasValue && !string.IsNullOrWhiteSpace(x.FromGalponId) && fromGalponDict.TryGetValue((x.FromGalponId, x.FromFarmId.Value), out var fgn))
                fromGalponNombre = fgn;

            var tipoOp = MapTipoOperacionLabel(x.MovementType);

            return new InventarioGestionMovimientoDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo, x.ItemInventario.Nombre, itemType,
                x.Quantity, x.Unit, x.MovementType, estadoDisplay,
                x.FromFarmId, x.FromNucleoId, x.FromGalponId,
                x.Reference, x.Reason, x.CreatedAt,
                x.Farm.Name, nucleoNombre, galponNombre,
                x.TransferGroupId,
                fromGranjaNombre,
                fromNucleoNombre,
                fromGalponNombre,
                tipoOp,
                x.ItemInventario.Concepto,
                x.ItemInventario.TipoItem,
                x.ParaProximoCiclo,
                x.RegistradoAt,
                x.SiloId,
                x.SiloId.HasValue && silosMov.TryGetValue(x.SiloId.Value, out var sn) ? sn : null,
                x.FromSiloId,
                x.FromSiloId.HasValue && silosMov.TryGetValue(x.FromSiloId.Value, out var fsn) ? fsn : null);
        }).ToList();
    }
}
