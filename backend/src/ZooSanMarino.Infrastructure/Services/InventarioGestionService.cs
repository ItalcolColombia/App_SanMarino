// src/ZooSanMarino.Infrastructure/Services/InventarioGestionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class InventarioGestionService : IInventarioGestionService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IFarmService _farmService;
    private readonly INucleoService _nucleoService;
    private readonly IGalponService _galponService;

    public InventarioGestionService(
        ZooSanMarinoContext db,
        ICurrentUser? current,
        ICompanyResolver companyResolver,
        IFarmService farmService,
        INucleoService nucleoService,
        IGalponService galponService)
    {
        _db = db;
        _current = current;
        _companyResolver = companyResolver;
        _farmService = farmService;
        _nucleoService = nucleoService;
        _galponService = galponService;
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

    private async Task<int> GetEffectivePaisIdAsync(int? fromFarmId, CancellationToken ct = default)
    {
        if (_current?.PaisId > 0) return _current.PaisId.Value;
        if (fromFarmId.HasValue)
        {
            var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fromFarmId.Value, ct);
            if (farm != null)
            {
                var paisId = await _db.Set<Departamento>().Where(d => d.DepartamentoId == farm.DepartamentoId).Select(d => d.PaisId).FirstOrDefaultAsync(ct);
                if (paisId > 0) return paisId;
            }
        }
        return 0;
    }

    public async Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var farms = (await _farmService.GetAllAsync(userId: _current?.UserGuid ?? Guid.Empty, companyId: companyId).ConfigureAwait(false)).ToList();
        var allowedFarmIds = farms.Select(f => f.Id).ToHashSet();

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleos = allowedFarmIds.Count > 0 ? nucleosAll.Where(n => allowedFarmIds.Contains(n.GranjaId)).ToList() : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetail = allowedFarmIds.Count > 0 ? galponesDetailAll.Where(g => allowedFarmIds.Contains(g.GranjaId)).ToList() : new List<GalponDetailDto>();
        var galpones = galponesDetail.Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId)).ToList();

        return new InventarioGestionFilterDataDto(Farms: farms, Nucleos: nucleos, Galpones: galpones);
    }

    public async Task<List<InventarioGestionStockDto>> GetStockAsync(
        int? farmId = null,
        string? nucleoId = null,
        string? galponId = null,
        string? itemType = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var paisId = await GetEffectivePaisIdAsync(farmId, ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionStockDto>();

        var query = _db.InventarioGestionStock
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value);
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmId.HasValue) query = query.Where(x => x.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(nucleoId)) query = query.Where(x => x.NucleoId == nucleoId);
        if (!string.IsNullOrWhiteSpace(galponId)) query = query.Where(x => x.GalponId == galponId);
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            var it = itemType.Trim();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Concepto != null && x.ItemInventarioEcuador.Concepto == it) ||
                (x.ItemInventarioEcuador.TipoItem != null && x.ItemInventarioEcuador.TipoItem == it));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.ItemInventarioEcuador.Codigo.Contains(s) || x.ItemInventarioEcuador.Nombre.Contains(s));
        }

        var list = await query.OrderBy(x => x.Farm.Name).ThenBy(x => x.NucleoId).ThenBy(x => x.GalponId).ThenBy(x => x.ItemInventarioEcuador.Nombre).ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);

        return list.Select(x =>
        {
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemTypeOut = x.ItemInventarioEcuador.Concepto ?? x.ItemInventarioEcuador.TipoItem ?? "alimento";
            return new InventarioGestionStockDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventarioEcuador.Codigo, x.ItemInventarioEcuador.Nombre, itemTypeOut,
                x.Quantity, x.Unit, x.Farm.Name, nucleoNombre, galponNombre);
        }).ToList();
    }

    private static bool IsAlimento(ItemInventarioEcuador item)
    {
        var concept = item.Concepto;
        if (!string.IsNullOrWhiteSpace(concept))
            return string.Equals(concept.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
        return string.Equals(item.TipoItem?.Trim(), "alimento", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(int CompanyId, int PaisId)> GetFarmCompanyAndPaisAsync(int farmId, CancellationToken ct)
    {
        var farm = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == farmId, ct);
        if (farm == null) throw new InvalidOperationException($"La granja {farmId} no existe.");
        var departamento = await _db.Set<Departamento>().AsNoTracking().FirstOrDefaultAsync(d => d.DepartamentoId == farm.DepartamentoId, ct);
        if (departamento == null) throw new InvalidOperationException($"No se encontró el departamento de la granja {farmId}.");
        return (farm.CompanyId, departamento.PaisId);
    }

    public async Task<InventarioGestionStockDto> RegistrarIngresoAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventarioEcuador.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");
        var isAlimento = IsAlimento(item);
        if (isAlimento && (string.IsNullOrWhiteSpace(req.NucleoId) || string.IsNullOrWhiteSpace(req.GalponId)))
            throw new InvalidOperationException("Para ítem tipo alimento debe indicar Núcleo y Galpón.");
        if (!isAlimento && (!string.IsNullOrWhiteSpace(req.NucleoId) || !string.IsNullOrWhiteSpace(req.GalponId)))
            throw new InvalidOperationException("Para ítems que no son alimento el inventario es solo a nivel granja (no use Núcleo/Galpón).");

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");
        var effectivePais = await GetEffectivePaisIdAsync(req.FarmId, ct);
        if (effectivePais > 0 && paisId != effectivePais)
            throw new InvalidOperationException("La granja no pertenece al país activo.");

        var nucleoId = isAlimento ? req.NucleoId!.Trim() : null;
        var galponId = isAlimento ? req.GalponId!.Trim() : null;

        var existing = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.FarmId && x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == nucleoId && x.GalponId == galponId, ct);

        if (existing == null)
        {
            existing = new InventarioGestionStock
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = nucleoId,
                GalponId = galponId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.InventarioGestionStock.Add(existing);
        }
        else
        {
            existing.Quantity += req.Quantity;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var estadoIngreso = string.Equals(req.OrigenTipo?.Trim(), "planta", StringComparison.OrdinalIgnoreCase)
            ? "Entrada planta"
            : "Entrada granja";
        var mov = new InventarioGestionMovimiento
        {
            CompanyId = companyId,
            PaisId = paisId,
            FarmId = req.FarmId,
            NucleoId = nucleoId,
            GalponId = galponId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = existing.Unit,
            MovementType = "Ingreso",
            Estado = estadoIngreso,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        };
        _db.InventarioGestionMovimientos.Add(mov);
        await _db.SaveChangesAsync(ct);

        return (await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct))
            .FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == nucleoId && x.GalponId == galponId)
            ?? new InventarioGestionStockDto(existing.Id, existing.FarmId, existing.NucleoId, existing.GalponId, existing.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", existing.Quantity, existing.Unit, null, null, null);
    }

    public async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoAsync(InventarioGestionTrasladoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventarioEcuador.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");
        var isAlimento = IsAlimento(item);
        if (isAlimento)
        {
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId) || string.IsNullOrWhiteSpace(req.ToNucleoId) || string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para alimento debe indicar Núcleo y Galpón de origen y destino.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.FromNucleoId) || !string.IsNullOrWhiteSpace(req.FromGalponId) || !string.IsNullOrWhiteSpace(req.ToNucleoId) || !string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para ítems que no son alimento el traslado es solo entre granjas (sin Núcleo/Galpón).");
        }

        var fromNucleoId = isAlimento ? req.FromNucleoId!.Trim() : null;
        var fromGalponId = isAlimento ? req.FromGalponId!.Trim() : null;
        var toNucleoId = isAlimento ? req.ToNucleoId!.Trim() : null;
        var toGalponId = isAlimento ? req.ToGalponId!.Trim() : null;

        var stockOrigen = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.FromFarmId && x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == fromNucleoId && x.GalponId == fromGalponId, ct);
        if (stockOrigen == null || stockOrigen.Quantity < req.Quantity)
            throw new InvalidOperationException("No hay stock suficiente en el origen para el traslado.");

        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);
        var transferGroupId = Guid.NewGuid();

        stockOrigen.Quantity -= req.Quantity;
        stockOrigen.UpdatedAt = DateTimeOffset.UtcNow;

        var stockDestino = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.ToFarmId && x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == toNucleoId && x.GalponId == toGalponId, ct);
        if (stockDestino == null)
        {
            stockDestino = new InventarioGestionStock
            {
                CompanyId = companyIdTo,
                PaisId = paisIdTo,
                FarmId = req.ToFarmId,
                NucleoId = toNucleoId,
                GalponId = toGalponId,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                Quantity = req.Quantity,
                Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.InventarioGestionStock.Add(stockDestino);
        }
        else
        {
            stockDestino.Quantity += req.Quantity;
            stockDestino.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var estadoTraslado = string.Equals(req.DestinoTipo?.Trim(), "planta", StringComparison.OrdinalIgnoreCase)
            ? "Transferencia a planta"
            : "Transferencia a granja";
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = stockOrigen.CompanyId,
            PaisId = stockOrigen.PaisId,
            FarmId = req.FromFarmId,
            NucleoId = fromNucleoId,
            GalponId = fromGalponId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = stockOrigen.Unit,
            MovementType = "TrasladoSalida",
            Estado = estadoTraslado,
            FromFarmId = req.ToFarmId,
            FromNucleoId = toNucleoId,
            FromGalponId = toGalponId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        });
        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = companyIdTo,
            PaisId = paisIdTo,
            FarmId = req.ToFarmId,
            NucleoId = toNucleoId,
            GalponId = toGalponId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = stockDestino.Unit,
            MovementType = "TrasladoEntrada",
            Estado = estadoTraslado,
            FromFarmId = req.FromFarmId,
            FromNucleoId = fromNucleoId,
            FromGalponId = fromGalponId,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        });

        await _db.SaveChangesAsync(ct);

        var listOrigen = await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct);
        var listDestino = await GetStockAsync(req.ToFarmId, toNucleoId, toGalponId, null, null, ct);
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity, stockOrigen.Unit);
        var dtoDestino = listDestino.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit);
        return (dtoOrigen, dtoDestino);
    }

    public async Task<InventarioGestionStockDto> RegistrarConsumoAsync(InventarioGestionConsumoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad de consumo debe ser positiva.");
        var item = await _db.ItemInventarioEcuador.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");
        var isAlimento = IsAlimento(item);
        if (isAlimento && (string.IsNullOrWhiteSpace(req.NucleoId) || string.IsNullOrWhiteSpace(req.GalponId)))
            throw new InvalidOperationException("Para ítem tipo alimento debe indicar Núcleo y Galpón.");
        if (!isAlimento && (!string.IsNullOrWhiteSpace(req.NucleoId) || !string.IsNullOrWhiteSpace(req.GalponId)))
            req = req with { NucleoId = null, GalponId = null };

        var nucleoId = isAlimento ? req.NucleoId!.Trim() : null;
        var galponId = isAlimento ? req.GalponId!.Trim() : null;

        var stock = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.FarmId && x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == nucleoId && x.GalponId == galponId, ct);
        if (stock == null || stock.Quantity < req.Quantity)
            throw new InvalidOperationException("No hay stock suficiente para el consumo.");

        stock.Quantity -= req.Quantity;
        stock.UpdatedAt = DateTimeOffset.UtcNow;

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");

        _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
        {
            CompanyId = companyId,
            PaisId = paisId,
            FarmId = req.FarmId,
            NucleoId = nucleoId,
            GalponId = galponId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim(),
            MovementType = "Consumo",
            Estado = "Consumo",
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        });
        await _db.SaveChangesAsync(ct);

        var list = await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct);
        return list.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            ?? new InventarioGestionStockDto(stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stock.Quantity, stock.Unit);
    }

    public async Task<List<InventarioGestionMovimientoDto>> GetMovimientosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? estado = null,
        string? movementType = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var paisId = farmId.HasValue ? await GetEffectivePaisIdAsync(farmId, ct) : 0;
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionMovimientoDto>();

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value);
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmId.HasValue) query = query.Where(x => x.FarmId == farmId.Value);
        if (fechaDesde.HasValue) query = query.Where(x => x.CreatedAt >= fechaDesde.Value);
        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(x => x.Estado != null && x.Estado.Trim() == estado.Trim());
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(x => x.MovementType == movementType.Trim());

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);

        return list.Select(x =>
        {
            var estadoDisplay = x.Estado;
            if (string.IsNullOrWhiteSpace(estadoDisplay))
            {
                estadoDisplay = x.MovementType switch
                {
                    "Ingreso" => (x.Reason != null && x.Reason.Contains("Llegada a planta", StringComparison.OrdinalIgnoreCase)) ? "Entrada planta" : "Entrada granja",
                    "TrasladoEntrada" or "TrasladoSalida" => "Transferencia a granja",
                    "Consumo" => "Consumo",
                    _ => x.MovementType
                };
            }
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemType = x.ItemInventarioEcuador.Concepto ?? x.ItemInventarioEcuador.TipoItem ?? "alimento";
            return new InventarioGestionMovimientoDto(
                x.Id, x.FarmId, x.NucleoId, x.GalponId, x.ItemInventarioEcuadorId,
                x.ItemInventarioEcuador.Codigo, x.ItemInventarioEcuador.Nombre, itemType,
                x.Quantity, x.Unit, x.MovementType, estadoDisplay,
                x.FromFarmId, x.FromNucleoId, x.FromGalponId,
                x.Reference, x.Reason, x.CreatedAt,
                x.Farm.Name, nucleoNombre, galponNombre);
        }).ToList();
    }
}
