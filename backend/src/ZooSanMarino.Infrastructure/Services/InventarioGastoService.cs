using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class InventarioGastoService : IInventarioGastoService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IInventarioGestionService _inventario;
    private readonly ISeguimientoAvesEngordeFilterDataService _filterDataSvc;

    public InventarioGastoService(
        ZooSanMarinoContext db,
        ICurrentUser? current,
        ICompanyResolver companyResolver,
        IInventarioGestionService inventario,
        ISeguimientoAvesEngordeFilterDataService filterDataSvc)
    {
        _db = db;
        _current = current;
        _companyResolver = companyResolver;
        _inventario = inventario;
        _filterDataSvc = filterDataSvc;
    }

    private async Task<int?> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (_current == null) return null;
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId > 0 ? _current.CompanyId : null;
    }

    private string? CurrentUserIdString() => _current?.UserId.ToString();

    public Task<LoteReproductoraFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
        => _filterDataSvc.GetFilterDataAsync(ct);

    public async Task<List<string>> GetConceptosAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) return new List<string>();

        return await _db.ItemInventarioEcuador.AsNoTracking()
            .Where(i => i.CompanyId == companyId.Value && i.Activo)
            .Where(i => i.TipoItem.ToLower() != "alimento")
            .Where(i => i.Concepto != null && i.Concepto.Trim() != "")
            .Select(i => i.Concepto!.Trim())
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<List<InventarioGastoItemStockDto>> GetItemsWithStockAsync(int farmId, string concepto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) return new List<InventarioGastoItemStockDto>();
        if (string.IsNullOrWhiteSpace(concepto)) return new List<InventarioGastoItemStockDto>();

        var farmOk = await _db.Farms.AsNoTracking().AnyAsync(f => f.Id == farmId && f.CompanyId == companyId.Value, ct);
        if (!farmOk) throw new InvalidOperationException("La granja no pertenece a su empresa.");

        // Normaliza concepto para comparar sin fallar por mayúsculas/espacios.
        var cNorm = concepto.Trim().ToLower();

        // Solo ítems con stock > 0 (stock por granja; nucleo/galpon null para no-alimento).
        var rows = await _db.InventarioGestionStock.AsNoTracking()
            .Where(s => s.FarmId == farmId && s.NucleoId == null && s.GalponId == null)
            .Where(s => s.Quantity > 0)
            .Join(_db.ItemInventarioEcuador.AsNoTracking(),
                s => s.ItemInventarioEcuadorId,
                i => i.Id,
                (s, i) => new { s, i })
            .Where(x => x.i.CompanyId == companyId.Value && x.i.Activo)
            .Where(x => x.i.TipoItem.ToLower() != "alimento")
            .Where(x => (x.i.Concepto ?? "").Trim().ToLower() == cNorm)
            .OrderBy(x => x.i.Nombre)
            .Select(x => new InventarioGastoItemStockDto(
                x.i.Id,
                x.i.Codigo,
                x.i.Nombre,
                x.i.TipoItem,
                x.i.Unidad,
                x.i.Concepto,
                x.s.Quantity
            ))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<List<InventarioGastoListItemDto>> SearchAsync(InventarioGastoSearchRequest req, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) return new List<InventarioGastoListItemDto>();

        var q = _db.InventarioGastos.AsNoTracking()
            .Where(g => g.CompanyId == companyId.Value);

        if (req.FarmId.HasValue) q = q.Where(g => g.FarmId == req.FarmId.Value);
        if (!string.IsNullOrWhiteSpace(req.NucleoId)) q = q.Where(g => g.NucleoId == req.NucleoId!.Trim());
        if (!string.IsNullOrWhiteSpace(req.GalponId)) q = q.Where(g => g.GalponId == req.GalponId!.Trim());
        if (req.LoteAveEngordeId.HasValue) q = q.Where(g => g.LoteAveEngordeId == req.LoteAveEngordeId.Value);
        if (req.FechaDesde.HasValue) q = q.Where(g => g.Fecha >= req.FechaDesde.Value.Date);
        if (req.FechaHasta.HasValue) q = q.Where(g => g.Fecha <= req.FechaHasta.Value.Date);
        if (!string.IsNullOrWhiteSpace(req.Estado)) q = q.Where(g => g.Estado == req.Estado!.Trim());

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim().ToLower();
            q = q.Where(g => (g.Observaciones ?? "").ToLower().Contains(s));
        }

        var list = await q
            .OrderByDescending(g => g.Fecha)
            .ThenByDescending(g => g.Id)
            .Select(g => new
            {
                g.Id,
                g.Fecha,
                g.FarmId,
                g.NucleoId,
                g.GalponId,
                g.LoteAveEngordeId,
                g.Observaciones,
                g.Estado,
                g.CreatedAt,
                g.CreatedByUserId,
                LoteNombre = g.LoteAveEngordeId != null
                    ? _db.LoteAveEngorde.Where(l => l.LoteAveEngordeId == g.LoteAveEngordeId).Select(l => l.LoteNombre).FirstOrDefault()
                    : null,
                Lineas = _db.InventarioGastoDetalles.Count(d => d.InventarioGastoId == g.Id),
                TotalCantidad = _db.InventarioGastoDetalles.Where(d => d.InventarioGastoId == g.Id).Sum(d => (decimal?)d.Cantidad) ?? 0,
                Unidad = _db.InventarioGastoDetalles.Where(d => d.InventarioGastoId == g.Id).Select(d => d.Unidad).FirstOrDefault()
            })
            .ToListAsync(ct);

        // Concepto filter (por detalle)
        if (!string.IsNullOrWhiteSpace(req.Concepto))
        {
            var concepto = req.Concepto.Trim();
            var ids = await _db.InventarioGastoDetalles.AsNoTracking()
                .Where(d => d.Concepto == concepto)
                .Select(d => d.InventarioGastoId)
                .Distinct()
                .ToListAsync(ct);
            var idSet = ids.ToHashSet();
            list = list.Where(x => idSet.Contains(x.Id)).ToList();
        }

        var farmIds = list.Select(x => x.FarmId).Distinct().ToList();
        var farmDict = await _db.Farms.AsNoTracking()
            .Where(f => farmIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        var nucleoDict = new Dictionary<(string NucleoId, int FarmId), string>();
        var nucleoKeys = list
            .Where(x => !string.IsNullOrEmpty(x.NucleoId))
            .Select(x => (x.NucleoId!, x.FarmId))
            .Distinct()
            .ToList();
        if (nucleoKeys.Count > 0)
        {
            var nIds = nucleoKeys.Select(k => k.Item1).Distinct().ToList();
            var farmIdsN = nucleoKeys.Select(k => k.Item2).Distinct().ToList();
            var nucleos = await _db.Nucleos.AsNoTracking()
                .Where(n => nIds.Contains(n.NucleoId) && farmIdsN.Contains(n.GranjaId))
                .Select(n => new { n.NucleoId, n.GranjaId, n.NucleoNombre })
                .ToListAsync(ct);
            foreach (var n in nucleos)
                nucleoDict[(n.NucleoId, n.GranjaId)] = n.NucleoNombre;
        }

        var galponDict = new Dictionary<(string GalponId, int FarmId), string>();
        var galponKeys = list
            .Where(x => !string.IsNullOrEmpty(x.GalponId))
            .Select(x => (x.GalponId!, x.FarmId))
            .Distinct()
            .ToList();
        if (galponKeys.Count > 0)
        {
            var gIds = galponKeys.Select(k => k.Item1).Distinct().ToList();
            var farmIdsG = galponKeys.Select(k => k.Item2).Distinct().ToList();
            var galpones = await _db.Galpones.AsNoTracking()
                .Where(gp => gIds.Contains(gp.GalponId) && farmIdsG.Contains(gp.GranjaId))
                .Select(gp => new { gp.GalponId, gp.GranjaId, gp.GalponNombre })
                .ToListAsync(ct);
            foreach (var gp in galpones)
                galponDict[(gp.GalponId, gp.GranjaId)] = gp.GalponNombre;
        }

        return list.Select(x =>
        {
            string? nucleoNombre = null;
            if (!string.IsNullOrEmpty(x.NucleoId))
                nucleoDict.TryGetValue((x.NucleoId, x.FarmId), out nucleoNombre);

            string? galponNombre = null;
            if (!string.IsNullOrEmpty(x.GalponId))
                galponDict.TryGetValue((x.GalponId, x.FarmId), out galponNombre);

            farmDict.TryGetValue(x.FarmId, out var granjaNombre);

            return new InventarioGastoListItemDto(
                x.Id,
                x.Fecha,
                x.FarmId,
                granjaNombre,
                x.NucleoId,
                nucleoNombre,
                x.GalponId,
                galponNombre,
                x.LoteAveEngordeId,
                x.LoteNombre,
                x.Observaciones,
                x.Estado,
                x.Lineas,
                x.TotalCantidad,
                x.Unidad,
                x.CreatedAt,
                x.CreatedByUserId
            );
        }).ToList();
    }

    public async Task<List<InventarioGastoExportRowDto>> ExportAsync(InventarioGastoSearchRequest req, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) return new List<InventarioGastoExportRowDto>();

        var q = _db.InventarioGastos.AsNoTracking()
            .Where(g => g.CompanyId == companyId.Value);

        if (req.FarmId.HasValue) q = q.Where(g => g.FarmId == req.FarmId.Value);
        if (!string.IsNullOrWhiteSpace(req.NucleoId)) q = q.Where(g => g.NucleoId == req.NucleoId!.Trim());
        if (!string.IsNullOrWhiteSpace(req.GalponId)) q = q.Where(g => g.GalponId == req.GalponId!.Trim());
        if (req.LoteAveEngordeId.HasValue) q = q.Where(g => g.LoteAveEngordeId == req.LoteAveEngordeId.Value);
        if (req.FechaDesde.HasValue) q = q.Where(g => g.Fecha >= req.FechaDesde.Value.Date);
        if (req.FechaHasta.HasValue) q = q.Where(g => g.Fecha <= req.FechaHasta.Value.Date);
        if (!string.IsNullOrWhiteSpace(req.Estado)) q = q.Where(g => g.Estado == req.Estado!.Trim());

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim().ToLower();
            q = q.Where(g => (g.Observaciones ?? "").ToLower().Contains(s));
        }

        var idList = await q.Select(g => g.Id).ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(req.Concepto))
        {
            var concepto = req.Concepto.Trim();
            idList = await _db.InventarioGastoDetalles.AsNoTracking()
                .Where(d => idList.Contains(d.InventarioGastoId) && d.Concepto == concepto)
                .Select(d => d.InventarioGastoId)
                .Distinct()
                .ToListAsync(ct);
        }

        if (idList.Count == 0) return new List<InventarioGastoExportRowDto>();

        var raw = await (
            from d in _db.InventarioGastoDetalles.AsNoTracking()
            join g in _db.InventarioGastos.AsNoTracking() on d.InventarioGastoId equals g.Id
            join i in _db.ItemInventarioEcuador.AsNoTracking() on d.ItemInventarioEcuadorId equals i.Id
            join f in _db.Farms.AsNoTracking() on g.FarmId equals f.Id
            where idList.Contains(g.Id)
            orderby g.Fecha descending, g.Id descending, i.Nombre
            select new { d, g, i, GranjaNombre = f.Name }
        ).ToListAsync(ct);

        var nucleoDict = new Dictionary<(string NucleoId, int FarmId), string>();
        var nucleoKeys = raw
            .Where(x => !string.IsNullOrEmpty(x.g.NucleoId))
            .Select(x => (x.g.NucleoId!, x.g.FarmId))
            .Distinct()
            .ToList();
        if (nucleoKeys.Count > 0)
        {
            var nIds = nucleoKeys.Select(k => k.Item1).Distinct().ToList();
            var farmIds = nucleoKeys.Select(k => k.Item2).Distinct().ToList();
            var nucleos = await _db.Nucleos.AsNoTracking()
                .Where(n => nIds.Contains(n.NucleoId) && farmIds.Contains(n.GranjaId))
                .Select(n => new { n.NucleoId, n.GranjaId, n.NucleoNombre })
                .ToListAsync(ct);
            foreach (var n in nucleos)
                nucleoDict[(n.NucleoId, n.GranjaId)] = n.NucleoNombre;
        }

        var galponDict = new Dictionary<(string GalponId, int FarmId), string>();
        var galponKeys = raw
            .Where(x => !string.IsNullOrEmpty(x.g.GalponId))
            .Select(x => (x.g.GalponId!, x.g.FarmId))
            .Distinct()
            .ToList();
        if (galponKeys.Count > 0)
        {
            var gIds = galponKeys.Select(k => k.Item1).Distinct().ToList();
            var farmIdsG = galponKeys.Select(k => k.Item2).Distinct().ToList();
            var galpones = await _db.Galpones.AsNoTracking()
                .Where(gp => gIds.Contains(gp.GalponId) && farmIdsG.Contains(gp.GranjaId))
                .Select(gp => new { gp.GalponId, gp.GranjaId, gp.GalponNombre })
                .ToListAsync(ct);
            foreach (var gp in galpones)
                galponDict[(gp.GalponId, gp.GranjaId)] = gp.GalponNombre;
        }

        var loteIds = raw
            .Select(x => x.g.LoteAveEngordeId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var loteDict = new Dictionary<int, string?>();
        if (loteIds.Count > 0)
        {
            var lotes = await _db.LoteAveEngorde.AsNoTracking()
                .Where(l => l.LoteAveEngordeId.HasValue && loteIds.Contains(l.LoteAveEngordeId.Value))
                .Select(l => new { l.LoteAveEngordeId, l.LoteNombre })
                .ToListAsync(ct);
            foreach (var l in lotes)
                if (l.LoteAveEngordeId.HasValue)
                    loteDict[l.LoteAveEngordeId.Value] = l.LoteNombre;
        }

        return raw.Select(x =>
        {
            string? nucleoNombre = null;
            if (!string.IsNullOrEmpty(x.g.NucleoId))
                nucleoDict.TryGetValue((x.g.NucleoId, x.g.FarmId), out nucleoNombre);

            string? galponNombre = null;
            if (!string.IsNullOrEmpty(x.g.GalponId))
                galponDict.TryGetValue((x.g.GalponId, x.g.FarmId), out galponNombre);

            string? loteNombre = null;
            if (x.g.LoteAveEngordeId.HasValue)
                loteDict.TryGetValue(x.g.LoteAveEngordeId.Value, out loteNombre);

            return new InventarioGastoExportRowDto(
                x.g.Id,
                x.g.Fecha,
                x.g.Estado,
                x.g.Observaciones,
                x.g.FarmId,
                x.GranjaNombre,
                x.g.NucleoId,
                nucleoNombre,
                x.g.GalponId,
                galponNombre,
                x.g.LoteAveEngordeId,
                loteNombre,
                x.d.Id,
                x.d.ItemInventarioEcuadorId,
                x.i.Codigo,
                x.i.Nombre,
                x.i.TipoItem,
                x.d.Concepto,
                x.d.Cantidad,
                x.d.Unidad,
                x.d.StockAntes,
                x.d.StockDespues,
                x.g.CreatedAt,
                x.g.CreatedByUserId,
                x.g.DeletedAt,
                x.g.DeletedByUserId
            );
        }).ToList();
    }

    public async Task<InventarioGastoDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) throw new UnauthorizedAccessException("Empresa activa inválida.");

        var g = await _db.InventarioGastos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId.Value, ct);
        if (g == null) throw new InvalidOperationException("El gasto no existe.");

        string? loteNombre = null;
        if (g.LoteAveEngordeId.HasValue)
        {
            loteNombre = await _db.LoteAveEngorde.AsNoTracking()
                .Where(l => l.LoteAveEngordeId == g.LoteAveEngordeId.Value)
                .Select(l => l.LoteNombre)
                .FirstOrDefaultAsync(ct);
        }

        var det = await _db.InventarioGastoDetalles.AsNoTracking()
            .Where(d => d.InventarioGastoId == g.Id)
            .Join(_db.ItemInventarioEcuador.AsNoTracking(),
                d => d.ItemInventarioEcuadorId,
                i => i.Id,
                (d, i) => new { d, i })
            .OrderBy(x => x.i.Nombre)
            .Select(x => new InventarioGastoDetalleDto(
                x.d.Id,
                x.d.ItemInventarioEcuadorId,
                x.i.Codigo,
                x.i.Nombre,
                x.i.TipoItem,
                x.d.Concepto,
                x.d.Cantidad,
                x.d.Unidad,
                x.d.StockAntes,
                x.d.StockDespues
            ))
            .ToListAsync(ct);

        return new InventarioGastoDto(
            g.Id,
            g.Fecha,
            g.FarmId,
            g.NucleoId,
            g.GalponId,
            g.LoteAveEngordeId,
            loteNombre,
            g.Observaciones,
            g.Estado,
            g.CreatedAt,
            g.CreatedByUserId,
            g.DeletedAt,
            g.DeletedByUserId,
            det
        );
    }

    public async Task<InventarioGastoDto> CreateAsync(CreateInventarioGastoRequest req, CancellationToken ct = default)
    {
        if (req.Lineas == null || req.Lineas.Count == 0)
            throw new InvalidOperationException("Debe agregar al menos una línea de gasto.");
        if (string.IsNullOrWhiteSpace(req.Concepto))
            throw new InvalidOperationException("El concepto es obligatorio.");

        foreach (var l in req.Lineas)
        {
            if (l.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor que 0.");
        }

        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0) throw new UnauthorizedAccessException("Empresa activa inválida.");

        var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == req.FarmId, ct);
        if (farm == null) throw new InvalidOperationException("La granja no existe.");
        if (farm.CompanyId != companyId.Value) throw new InvalidOperationException("La granja no pertenece a su empresa.");

        string? loteNombre = null;
        if (req.LoteAveEngordeId.HasValue)
        {
            var lote = await _db.LoteAveEngorde.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteAveEngordeId == req.LoteAveEngordeId.Value, ct);
            if (lote == null) throw new InvalidOperationException("El lote no existe.");
            loteNombre = lote.LoteNombre;
        }

        var conceptoNorm = req.Concepto.Trim().ToLower();
        var itemIds = req.Lineas.Select(x => x.ItemInventarioEcuadorId).Distinct().ToList();

        var items = await _db.ItemInventarioEcuador.AsNoTracking()
            .Where(i => i.CompanyId == companyId.Value && itemIds.Contains(i.Id))
            .ToListAsync(ct);

        if (items.Count != itemIds.Count)
            throw new InvalidOperationException("Uno o más ítems no existen en el catálogo de la compañía.");

        foreach (var it in items)
        {
            if (!it.Activo) throw new InvalidOperationException($"El ítem '{it.Codigo}' está inactivo.");
            if (string.Equals(it.TipoItem, "alimento", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Este módulo no permite consumir ítems tipo alimento.");
            if (((it.Concepto ?? "").Trim().ToLower()) != conceptoNorm)
                throw new InvalidOperationException($"El ítem '{it.Codigo}' no pertenece al concepto seleccionado.");
        }

        var now = DateTimeOffset.UtcNow;
        var uid = CurrentUserIdString();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var gasto = new InventarioGasto
        {
            CompanyId = companyId.Value,
            PaisId = items.First().PaisId,
            FarmId = req.FarmId,
            NucleoId = string.IsNullOrWhiteSpace(req.NucleoId) ? null : req.NucleoId.Trim(),
            GalponId = string.IsNullOrWhiteSpace(req.GalponId) ? null : req.GalponId.Trim(),
            LoteAveEngordeId = req.LoteAveEngordeId,
            Fecha = req.Fecha.Date,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim(),
            Estado = "Activo",
            CreatedAt = now,
            CreatedByUserId = uid
        };

        _db.InventarioGastos.Add(gasto);
        await _db.SaveChangesAsync(ct);

        var detalles = new List<InventarioGastoDetalle>();

        foreach (var linea in req.Lineas)
        {
            var item = items.First(i => i.Id == linea.ItemInventarioEcuadorId);
            var stockAntes = await _db.InventarioGestionStock.AsNoTracking()
                .Where(s => s.FarmId == req.FarmId && s.NucleoId == null && s.GalponId == null && s.ItemInventarioEcuadorId == item.Id)
                .Select(s => (decimal?)s.Quantity)
                .FirstOrDefaultAsync(ct);

            var refStr = $"Gasto inventario #{gasto.Id} {gasto.Fecha:yyyy-MM-dd}" + (loteNombre != null ? $" · Lote {loteNombre}" : "");
            var reason = gasto.Observaciones;

            var stockDto = await _inventario.RegistrarConsumoAsync(
                new InventarioGestionConsumoRequest(req.FarmId, null, null, item.Id, linea.Cantidad, item.Unidad, refStr, reason),
                ct);

            detalles.Add(new InventarioGastoDetalle
            {
                InventarioGastoId = gasto.Id,
                ItemInventarioEcuadorId = item.Id,
                Concepto = item.Concepto,
                Cantidad = linea.Cantidad,
                Unidad = item.Unidad,
                StockAntes = stockAntes,
                StockDespues = stockDto.Quantity
            });
        }

        _db.InventarioGastoDetalles.AddRange(detalles);
        await _db.SaveChangesAsync(ct);

        var auditPayload = JsonSerializer.Serialize(new
        {
            gastoId = gasto.Id,
            farmId = gasto.FarmId,
            loteAveEngordeId = gasto.LoteAveEngordeId,
            concepto = req.Concepto.Trim(),
            lineas = detalles.Select(d => new { d.ItemInventarioEcuadorId, d.Cantidad, d.Unidad })
        });
        _db.InventarioGastoAuditorias.Add(new InventarioGastoAuditoria
        {
            InventarioGastoId = gasto.Id,
            Accion = "Crear",
            Fecha = now,
            UserId = uid ?? "unknown",
            Detalle = auditPayload
        });
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return await GetByIdAsync(gasto.Id, ct);
    }

    public async Task DeleteAsync(int id, string? motivo, CancellationToken ct = default)
    {
        var uid = CurrentUserIdString();
        var now = DateTimeOffset.UtcNow;

        var gasto = await _db.InventarioGastos
            .Include(g => g.Detalles)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gasto == null) throw new InvalidOperationException("El gasto no existe.");
        if (string.Equals(gasto.Estado, "Eliminado", StringComparison.OrdinalIgnoreCase))
            return;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        gasto.Estado = "Eliminado";
        gasto.DeletedAt = now;
        gasto.DeletedByUserId = uid;
        _db.InventarioGastos.Update(gasto);
        await _db.SaveChangesAsync(ct);

        var refStr = $"Eliminación gasto inventario #{gasto.Id} {gasto.Fecha:yyyy-MM-dd}";
        var reason = string.IsNullOrWhiteSpace(motivo) ? "Devolución por eliminación de gasto" : motivo.Trim();

        foreach (var d in gasto.Detalles)
        {
            await _inventario.RegistrarIngresoAsync(
                new InventarioGestionIngresoRequest(
                    gasto.FarmId,
                    null,
                    null,
                    d.ItemInventarioEcuadorId,
                    d.Cantidad,
                    d.Unidad,
                    refStr,
                    reason,
                    OrigenTipo: null,
                    OrigenFarmId: null,
                    OrigenBodegaDescripcion: null,
                    FechaMovimiento: gasto.Fecha),
                ct);
        }

        var auditPayload = JsonSerializer.Serialize(new
        {
            gastoId = gasto.Id,
            motivo = reason,
            lineas = gasto.Detalles.Select(d => new { d.ItemInventarioEcuadorId, d.Cantidad, d.Unidad })
        });
        _db.InventarioGastoAuditorias.Add(new InventarioGastoAuditoria
        {
            InventarioGastoId = gasto.Id,
            Accion = "Eliminar",
            Fecha = now,
            UserId = uid ?? "unknown",
            Detalle = auditPayload
        });
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }
}

