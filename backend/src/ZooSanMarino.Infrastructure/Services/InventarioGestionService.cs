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

    /// <summary>Granjas del usuario (user_farms) que pertenecen a la empresa. Stock solo en estas granjas.</summary>
    private async Task<HashSet<int>> GetAssignedFarmIdsInCompanyAsync(int companyId, CancellationToken ct = default)
    {
        if (_current?.UserGuid is not { } uid || uid == Guid.Empty)
            return new HashSet<int>();

        var ids = await _farmService.GetAssignedFarmIdsForUserAsync(uid, ct).ConfigureAwait(false);
        var farms = await _farmService.GetFarmDtosByIdsInCompanyAsync(ids, companyId, ct).ConfigureAwait(false);
        return farms.Select(f => f.Id).ToHashSet();
    }

    public async Task<InventarioGestionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionFilterDataDto(
                Array.Empty<FarmDto>(),
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>(),
                Array.Empty<GalponLiteDto>());
        }

        var cid = companyId.Value;

        // Todas las granjas de la empresa (destino: traslado inter-granja, procedencia en ingreso "otra granja"/bodega, etc.)
        var farmsDestino = (await _farmService.GetAllAsync(userId: null, companyId: cid).ConfigureAwait(false)).ToList();
        var allowedDestinoIds = farmsDestino.Select(f => f.Id).ToHashSet();

        // Granjas asignadas al usuario (origen: stock, ingreso destino, traslado origen, filtros de gestión)
        List<FarmDto> farmsOrigen;
        if (_current?.UserGuid is { } userGuid && userGuid != Guid.Empty)
        {
            var idsOrigen = await _farmService.GetAssignedFarmIdsForUserAsync(userGuid, ct).ConfigureAwait(false);
            farmsOrigen = (await _farmService.GetFarmDtosByIdsInCompanyAsync(idsOrigen, cid, ct).ConfigureAwait(false)).ToList();
        }
        else
        {
            farmsOrigen = new List<FarmDto>();
        }

        var allowedOrigenIds = farmsOrigen.Select(f => f.Id).ToHashSet();

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosOrigen = allowedOrigenIds.Count > 0
            ? nucleosAll.Where(n => allowedOrigenIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();
        var nucleosDestino = allowedDestinoIds.Count > 0
            ? nucleosAll.Where(n => allowedDestinoIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetailOrigen = allowedOrigenIds.Count > 0
            ? galponesDetailAll.Where(g => allowedOrigenIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDetailDto>();
        var galponesDetailDestino = allowedDestinoIds.Count > 0
            ? galponesDetailAll.Where(g => allowedDestinoIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDetailDto>();

        var galponesOrigen = galponesDetailOrigen
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId)).ToList();
        var galponesDestino = galponesDetailDestino
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId)).ToList();

        return new InventarioGestionFilterDataDto(
            FarmsOrigen: farmsOrigen,
            FarmsDestino: farmsDestino,
            NucleosOrigen: nucleosOrigen,
            NucleosDestino: nucleosDestino,
            GalponesOrigen: galponesOrigen,
            GalponesDestino: galponesDestino);
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

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionStockDto>();

        var query = _db.InventarioGestionStock
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && allowedFarmIds.Contains(x.FarmId));
        if (paisId > 0) query = query.Where(x => x.PaisId == paisId);
        if (farmId.HasValue) query = query.Where(x => x.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(nucleoId)) query = query.Where(x => x.NucleoId == nucleoId);
        if (!string.IsNullOrWhiteSpace(galponId)) query = query.Where(x => x.GalponId == galponId);
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            var it = itemType.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Concepto != null &&
                 x.ItemInventarioEcuador.Concepto.Trim().ToLower() == it) ||
                (x.ItemInventarioEcuador.TipoItem != null &&
                 x.ItemInventarioEcuador.TipoItem.Trim().ToLower() == it));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventarioEcuador.Nombre ?? "").ToLower().Contains(s));
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
                x.Quantity, x.Unit, x.Farm.Name, nucleoNombre, galponNombre, x.CreatedAt);
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

        var origenTipoNorm = req.OrigenTipo?.Trim() ?? "";
        if (string.Equals(origenTipoNorm, "granja", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es otra granja, indique la granja de procedencia (OrigenFarmId).");
            if (req.OrigenFarmId.Value == req.FarmId)
                throw new InvalidOperationException("La granja de origen debe ser distinta a la granja de destino del ingreso.");
            var (origCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (origCompanyId != companyId)
                throw new InvalidOperationException("La granja de origen debe pertenecer a la misma empresa.");
        }
        if (string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es bodega, indique la granja a la que pertenece la bodega de procedencia (OrigenFarmId).");
            var (bodegaFarmCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (bodegaFarmCompanyId != companyId)
                throw new InvalidOperationException("La granja de la bodega de origen debe pertenecer a la misma empresa.");
        }

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

        var estadoIngreso = string.Equals(origenTipoNorm, "planta", StringComparison.OrdinalIgnoreCase)
            ? "Entrada planta"
            : string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase)
                ? "Entrada bodega"
                : "Entrada granja";
        var movCreatedAt = req.FechaMovimiento.HasValue
            ? new DateTimeOffset(
                req.FechaMovimiento.Value.Year,
                req.FechaMovimiento.Value.Month,
                req.FechaMovimiento.Value.Day,
                12, 0, 0,
                TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

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
            CreatedAt = movCreatedAt,
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

        var mismaGranja = req.FromFarmId == req.ToFarmId;

        if (mismaGranja)
        {
            if (!isAlimento)
                throw new InvalidOperationException("Para ítems que no son alimento no aplica traslado entre galpones en la misma granja (el stock es solo a nivel granja). Use traslado entre granjas distintas si aplica.");
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId) ||
                string.IsNullOrWhiteSpace(req.ToNucleoId) || string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para alimento en la misma granja debe indicar Núcleo y Galpón de origen y destino.");
            var fn = req.FromNucleoId.Trim();
            var fg = req.FromGalponId.Trim();
            var tn = req.ToNucleoId.Trim();
            var tg = req.ToGalponId.Trim();
            if (string.Equals(fg, tg, StringComparison.Ordinal) && string.Equals(fn, tn, StringComparison.Ordinal))
                throw new InvalidOperationException("El galpón de destino debe ser distinto al de origen.");
            return await RegistrarTrasladoMismaGranjaAsync(req, item, fn, fg, tn, tg, ct);
        }

        return await RegistrarTrasladoInterGranjaTransitoAsync(req, item, isAlimento, ct);
    }

    /// <summary>Traslado entre galpones de la misma granja: descuenta origen y suma destino en una sola operación (2 movimientos).</summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoMismaGranjaAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventarioEcuador item,
        string fromNucleoId,
        string fromGalponId,
        string toNucleoId,
        string toGalponId,
        CancellationToken ct)
    {
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
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var dtoDestino = listDestino.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId) ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt);
        return (dtoOrigen, dtoDestino);
    }

    /// <summary>
    /// Traslado entre granjas distintas: descuenta origen de inmediato y registra salida en tránsito.
    /// La recepción en destino solo suma stock (no vuelve a descontar origen).
    /// Registros antiguos con movement_type TrasladoInterGranjaPendiente siguen descontando origen al recibir.
    /// </summary>
    private async Task<(InventarioGestionStockDto Origen, InventarioGestionStockDto Destino)> RegistrarTrasladoInterGranjaTransitoAsync(
        InventarioGestionTrasladoRequest req,
        ItemInventarioEcuador item,
        bool isAlimento,
        CancellationToken ct)
    {
        string? fromNucleoId = null;
        string? fromGalponId = null;
        string? toNucleoHint = null;
        string? toGalponHint = null;

        if (isAlimento)
        {
            if (string.IsNullOrWhiteSpace(req.FromNucleoId) || string.IsNullOrWhiteSpace(req.FromGalponId))
                throw new InvalidOperationException("Para alimento debe indicar Núcleo y Galpón de origen.");
            fromNucleoId = req.FromNucleoId!.Trim();
            fromGalponId = req.FromGalponId!.Trim();
            toNucleoHint = string.IsNullOrWhiteSpace(req.ToNucleoId) ? null : req.ToNucleoId.Trim();
            toGalponHint = string.IsNullOrWhiteSpace(req.ToGalponId) ? null : req.ToGalponId.Trim();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.FromNucleoId) || !string.IsNullOrWhiteSpace(req.FromGalponId) ||
                !string.IsNullOrWhiteSpace(req.ToNucleoId) || !string.IsNullOrWhiteSpace(req.ToGalponId))
                throw new InvalidOperationException("Para ítems que no son alimento el traslado entre granjas es solo a nivel granja (sin Núcleo/Galpón).");
        }

        var stockOrigen = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.FromFarmId && x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId && x.NucleoId == fromNucleoId && x.GalponId == fromGalponId, ct);
        if (stockOrigen == null || stockOrigen.Quantity < req.Quantity)
            throw new InvalidOperationException("No hay stock suficiente en el origen para registrar el traslado a otra granja.");

        var transferGroupId = Guid.NewGuid();

        stockOrigen.Quantity -= req.Quantity;
        stockOrigen.UpdatedAt = DateTimeOffset.UtcNow;

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
            MovementType = "TrasladoInterGranjaSalida",
            Estado = "Tránsito",
            FromFarmId = req.ToFarmId,
            FromNucleoId = toNucleoHint,
            FromGalponId = toGalponHint,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            TransferGroupId = transferGroupId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        });

        await _db.SaveChangesAsync(ct);

        var listOrigen = await GetStockAsync(req.FromFarmId, fromNucleoId, fromGalponId, null, null, ct);
        var dtoOrigen = listOrigen.FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId)
            ?? new InventarioGestionStockDto(stockOrigen.Id, stockOrigen.FarmId, stockOrigen.NucleoId, stockOrigen.GalponId, stockOrigen.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stockOrigen.Quantity, stockOrigen.Unit, null, null, null, stockOrigen.CreatedAt);
        var itemTypeOut = item.Concepto ?? item.TipoItem ?? "alimento";
        var dtoDestinoPendiente = new InventarioGestionStockDto(
            0,
            req.ToFarmId,
            toNucleoHint,
            toGalponHint,
            req.ItemInventarioEcuadorId,
            item.Codigo,
            item.Nombre,
            itemTypeOut,
            0,
            string.IsNullOrWhiteSpace(req.Unit) ? stockOrigen.Unit : req.Unit.Trim(),
            null,
            null,
            null,
            null);
        return (dtoOrigen, dtoDestinoPendiente);
    }

    public async Task<List<InventarioGestionTransitoPendienteDto>> GetTransitosPendientesAsync(int? farmIdDestino = null, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTransitoPendienteDto>();

        var candidatos = await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value && x.TransferGroupId != null &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var gruposConEntrada = (await _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.MovementType == "TrasladoInterGranjaEntrada" && x.TransferGroupId != null)
            .Select(x => x.TransferGroupId!.Value)
            .ToListAsync(ct)).ToHashSet();

        var filtradas = candidatos.Where(s => s.TransferGroupId.HasValue && !gruposConEntrada.Contains(s.TransferGroupId.Value));
        if (farmIdDestino.HasValue)
            filtradas = filtradas.Where(s => s.FromFarmId == farmIdDestino.Value);

        var farmIds = filtradas.SelectMany(s => new[] { s.FarmId, s.FromFarmId ?? 0 }).Where(id => id > 0).Distinct().ToList();
        var farmNames = await _db.Farms.AsNoTracking().Where(f => farmIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return filtradas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromName);
            var toId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toId, out var toName);
            var pendienteDespachoOrigen = string.Equals(s.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal);
            return new InventarioGestionTransitoPendienteDto(
                s.TransferGroupId!.Value,
                s.Id,
                s.FarmId,
                fromName,
                toId,
                toName,
                s.NucleoId,
                s.GalponId,
                s.FromNucleoId,
                s.FromGalponId,
                s.ItemInventarioEcuadorId,
                s.ItemInventarioEcuador.Codigo,
                s.ItemInventarioEcuador.Nombre,
                s.Quantity,
                s.Unit,
                s.CreatedAt,
                pendienteDespachoOrigen);
        }).ToList();
    }

    public async Task<(InventarioGestionStockDto Destino, InventarioGestionMovimientoDto Movimiento)> RegistrarRecepcionTransitoAsync(InventarioGestionRecepcionTransitoRequest req, CancellationToken ct = default)
    {
        var salida = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId &&
                (x.MovementType == "TrasladoInterGranjaPendiente" || x.MovementType == "TrasladoInterGranjaSalida"), ct);
        if (salida == null)
            throw new InvalidOperationException("No se encontró el movimiento de traslado inter-granja para el grupo indicado.");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        if (salida.FromFarmId != req.ToFarmId)
            throw new InvalidOperationException("La granja de recepción debe ser la granja destino del traslado.");

        var item = salida.ItemInventarioEcuador;
        var isAlimento = IsAlimento(item);
        if (isAlimento && (string.IsNullOrWhiteSpace(req.ToNucleoId) || string.IsNullOrWhiteSpace(req.ToGalponId)))
            throw new InvalidOperationException("Para alimento debe indicar Núcleo y Galpón de recepción en la granja destino.");
        if (!isAlimento && (!string.IsNullOrWhiteSpace(req.ToNucleoId) || !string.IsNullOrWhiteSpace(req.ToGalponId)))
            throw new InvalidOperationException("Para ítems que no son alimento la recepción es solo a nivel granja.");

        var toNucleoId = isAlimento ? req.ToNucleoId!.Trim() : null;
        var toGalponId = isAlimento ? req.ToGalponId!.Trim() : null;

        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);
        if (salida.CompanyId != companyIdTo)
            throw new InvalidOperationException("La granja destino no pertenece a la misma empresa que la salida.");

        // Solicitud nueva: aquí se descuenta origen. Registro antiguo (Salida): el descuento ya se hizo al enviar.
        if (string.Equals(salida.MovementType, "TrasladoInterGranjaPendiente", StringComparison.Ordinal))
        {
            var stockOrigen = await _db.InventarioGestionStock
                .FirstOrDefaultAsync(x => x.FarmId == salida.FarmId && x.ItemInventarioEcuadorId == salida.ItemInventarioEcuadorId && x.NucleoId == salida.NucleoId && x.GalponId == salida.GalponId, ct);
            if (stockOrigen == null || stockOrigen.Quantity < salida.Quantity)
                throw new InvalidOperationException("No hay stock suficiente en origen para completar la recepción (verifique disponibilidad).");
            stockOrigen.Quantity -= salida.Quantity;
            stockOrigen.UpdatedAt = DateTimeOffset.UtcNow;
            salida.MovementType = "TrasladoInterGranjaSalida";
            salida.Estado = "Tránsito";
        }

        var qty = salida.Quantity;
        var stockDestino = await _db.InventarioGestionStock
            .FirstOrDefaultAsync(x => x.FarmId == req.ToFarmId && x.ItemInventarioEcuadorId == salida.ItemInventarioEcuadorId && x.NucleoId == toNucleoId && x.GalponId == toGalponId, ct);
        if (stockDestino == null)
        {
            stockDestino = new InventarioGestionStock
            {
                CompanyId = companyIdTo,
                PaisId = paisIdTo,
                FarmId = req.ToFarmId,
                NucleoId = toNucleoId,
                GalponId = toGalponId,
                ItemInventarioEcuadorId = salida.ItemInventarioEcuadorId,
                Quantity = qty,
                Unit = salida.Unit,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.InventarioGestionStock.Add(stockDestino);
        }
        else
        {
            stockDestino.Quantity += qty;
            stockDestino.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var movEntrada = new InventarioGestionMovimiento
        {
            CompanyId = companyIdTo,
            PaisId = paisIdTo,
            FarmId = req.ToFarmId,
            NucleoId = toNucleoId,
            GalponId = toGalponId,
            ItemInventarioEcuadorId = salida.ItemInventarioEcuadorId,
            Quantity = qty,
            Unit = stockDestino.Unit,
            MovementType = "TrasladoInterGranjaEntrada",
            Estado = "Recibido desde tránsito",
            FromFarmId = salida.FarmId,
            FromNucleoId = salida.NucleoId,
            FromGalponId = salida.GalponId,
            Reference = salida.Reference,
            Reason = "Recepción traslado inter-granja",
            TransferGroupId = req.TransferGroupId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = _current?.UserId.ToString()
        };
        _db.InventarioGestionMovimientos.Add(movEntrada);
        await _db.SaveChangesAsync(ct);

        var list = await GetStockAsync(req.ToFarmId, toNucleoId, toGalponId, null, null, ct);
        var dtoStock = list.FirstOrDefault(x => x.ItemInventarioEcuadorId == salida.ItemInventarioEcuadorId)
            ?? new InventarioGestionStockDto(stockDestino.Id, stockDestino.FarmId, stockDestino.NucleoId, stockDestino.GalponId, stockDestino.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento", stockDestino.Quantity, stockDestino.Unit, null, null, null, stockDestino.CreatedAt);

        var farmDest = await _db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == movEntrada.FarmId, ct);
        string? nn = null;
        string? gn = null;
        if (toNucleoId != null)
            nn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == toNucleoId && n.GranjaId == req.ToFarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
        if (toGalponId != null)
            gn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == toGalponId && g.GranjaId == req.ToFarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

        string? origenNn = null;
        string? origenGn = null;
        if (salida.NucleoId != null)
            origenNn = await _db.Nucleos.AsNoTracking().Where(n => n.NucleoId == salida.NucleoId && n.GranjaId == salida.FarmId).Select(n => n.NucleoNombre).FirstOrDefaultAsync(ct);
        if (salida.GalponId != null)
            origenGn = await _db.Galpones.AsNoTracking().Where(g => g.GalponId == salida.GalponId && g.GranjaId == salida.FarmId).Select(g => g.GalponNombre).FirstOrDefaultAsync(ct);

        var dtoMov = new InventarioGestionMovimientoDto(
            movEntrada.Id,
            movEntrada.FarmId,
            movEntrada.NucleoId,
            movEntrada.GalponId,
            movEntrada.ItemInventarioEcuadorId,
            item.Codigo,
            item.Nombre,
            item.Concepto ?? item.TipoItem ?? "alimento",
            movEntrada.Quantity,
            movEntrada.Unit,
            movEntrada.MovementType,
            movEntrada.Estado,
            movEntrada.FromFarmId,
            movEntrada.FromNucleoId,
            movEntrada.FromGalponId,
            movEntrada.Reference,
            movEntrada.Reason,
            movEntrada.CreatedAt,
            farmDest?.Name,
            nn,
            gn,
            movEntrada.TransferGroupId,
            salida.Farm.Name,
            origenNn,
            origenGn,
            "Traslado entre granjas (recepción)");

        return (dtoStock, dtoMov);
    }

    public async Task RechazarTransitoPendienteAsync(InventarioGestionRechazoTransitoRequest req, CancellationToken ct = default)
    {
        var pendiente = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaPendiente", ct);
        if (pendiente == null)
            throw new InvalidOperationException("No hay solicitud pendiente para rechazar (puede estar ya recibida o rechazada).");

        var yaEntrada = await _db.InventarioGestionMovimientos.AnyAsync(
            x => x.TransferGroupId == req.TransferGroupId && x.MovementType == "TrasladoInterGranjaEntrada", ct);
        if (yaEntrada)
            throw new InvalidOperationException("Este traslado ya fue recibido en destino.");

        pendiente.MovementType = "TrasladoInterGranjaRechazado";
        pendiente.Estado = "Rechazado destino";
        var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        pendiente.Reason = extra != null
            ? $"{pendiente.Reason ?? ""} | Rechazo destino: {extra}".Trim()
            : (pendiente.Reason ?? "Rechazado destino");
        await _db.SaveChangesAsync(ct);
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
            ?? new InventarioGestionStockDto(stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", stock.Quantity, stock.Unit, null, null, null, stock.CreatedAt);
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
            .Take(3000)
            .ToListAsync(ct);

        var nucleos = await _db.Nucleos.AsNoTracking().Where(n => list.Select(x => x.NucleoId).Contains(n.NucleoId)).ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct);
        var galpones = await _db.Galpones.AsNoTracking().Where(g => list.Select(x => x.GalponId).Contains(g.GalponId)).ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct);

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
                    _ => x.MovementType
                };
            }
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;
            var itemType = x.ItemInventarioEcuador.Concepto ?? x.ItemInventarioEcuador.TipoItem ?? "alimento";

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
                x.ItemInventarioEcuador.Codigo, x.ItemInventarioEcuador.Nombre, itemType,
                x.Quantity, x.Unit, x.MovementType, estadoDisplay,
                x.FromFarmId, x.FromNucleoId, x.FromGalponId,
                x.Reference, x.Reason, x.CreatedAt,
                x.Farm.Name, nucleoNombre, galponNombre,
                x.TransferGroupId,
                fromGranjaNombre,
                fromNucleoNombre,
                fromGalponNombre,
                tipoOp);
        }).ToList();
    }

    private static string MapTipoOperacionLabel(string movementType) => movementType switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "TrasladoSalida" => "Traslado (salida entre galpones)",
        "TrasladoEntrada" => "Traslado (entrada entre galpones)",
        "TrasladoInterGranjaPendiente" => "Traslado entre granjas (solicitud pendiente)",
        "TrasladoInterGranjaSalida" => "Traslado entre granjas (en tránsito)",
        "TrasladoInterGranjaEntrada" => "Traslado entre granjas (recepción)",
        "TrasladoInterGranjaRechazado" => "Traslado entre granjas (rechazado)",
        _ => movementType
    };
}
