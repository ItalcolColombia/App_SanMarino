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
    /// <summary>Etiquetas de operación (coinciden con <see cref="MapTipoOperacionLabel"/>).</summary>
    private static readonly IReadOnlyList<string> TiposOperacionFiltroLabels =
    [
        "Ingreso",
        "Consumo",
        "Traslado (salida entre galpones)",
        "Traslado (entrada entre galpones)",
        "Traslado entre granjas (solicitud pendiente)",
        "Traslado entre granjas (en tránsito)",
        "Traslado entre granjas (recepción)",
        "Traslado entre granjas (rechazado)",
        "Ajuste manual de stock",
        "Eliminación de registro de stock"
    ];

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

    /// <summary>Fecha en histórico: día elegido a mediodía UTC; si no hay fecha, hora actual del servidor (mismo criterio que ingresos).</summary>
    private static DateTimeOffset ResolveMovimientoCreatedAt(DateTime? fechaMovimiento)
    {
        if (!fechaMovimiento.HasValue)
            return DateTimeOffset.UtcNow;
        var d = fechaMovimiento.Value.Date;
        return new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
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

    /// <summary>Granjas asignadas al usuario en la empresa + núcleos y galpones asociados (misma regla que filtros de stock/histórico).</summary>
    private async Task<(List<FarmDto> FarmsOrigen, List<NucleoDto> NucleosOrigen, List<GalponLiteDto> GalponesOrigen)> LoadOrigenUbicacionUsuarioEnEmpresaAsync(
        int companyId,
        CancellationToken ct = default)
    {
        if (_current?.UserGuid is not { } userGuid || userGuid == Guid.Empty)
            return ([], [], []);

        var idsOrigen = await _farmService.GetAssignedFarmIdsForUserAsync(userGuid, ct).ConfigureAwait(false);
        var farmsOrigen = (await _farmService.GetFarmDtosByIdsInCompanyAsync(idsOrigen, companyId, ct).ConfigureAwait(false)).ToList();
        var allowedOrigenIds = farmsOrigen.Select(f => f.Id).ToHashSet();
        if (allowedOrigenIds.Count == 0)
            return (farmsOrigen, [], []);

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosOrigen = nucleosAll.Where(n => allowedOrigenIds.Contains(n.GranjaId)).ToList();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesOrigen = galponesDetailAll
            .Where(g => allowedOrigenIds.Contains(g.GranjaId))
            .Select(g => new GalponLiteDto(g.GalponId, g.GalponNombre, g.NucleoId, g.GranjaId))
            .ToList();

        return (farmsOrigen, nucleosOrigen, galponesOrigen);
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

        var (farmsOrigen, nucleosOrigen, galponesOrigen) = await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);

        var nucleosAll = (await _nucleoService.GetAllAsync().ConfigureAwait(false)).ToList();
        var nucleosDestino = allowedDestinoIds.Count > 0
            ? nucleosAll.Where(n => allowedDestinoIds.Contains(n.GranjaId)).ToList()
            : new List<NucleoDto>();

        var galponesDetailAll = (await _galponService.GetAllAsync().ConfigureAwait(false)).ToList();
        var galponesDetailDestino = allowedDestinoIds.Count > 0
            ? galponesDetailAll.Where(g => allowedDestinoIds.Contains(g.GranjaId)).ToList()
            : new List<GalponDetailDto>();

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

    public async Task<InventarioGestionHistoricoFiltrosDto> GetHistoricoFiltrosAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                Array.Empty<FarmDto>(),
                Array.Empty<NucleoDto>(),
                Array.Empty<GalponLiteDto>());
        }

        var cid = companyId.Value;
        var (farmsOrigenHist, nucleosOrigenHist, galponesOrigenHist) =
            await LoadOrigenUbicacionUsuarioEnEmpresaAsync(cid, ct).ConfigureAwait(false);
        var allowedFarmIds = farmsOrigenHist.Select(f => f.Id).ToHashSet();
        var paisId = await GetEffectivePaisIdAsync(null, ct);

        if (allowedFarmIds.Count == 0)
        {
            return new InventarioGestionHistoricoFiltrosDto(
                Array.Empty<InventarioGestionLoteFiltroDto>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                TiposOperacionFiltroLabels,
                farmsOrigenHist,
                nucleosOrigenHist,
                galponesOrigenHist);
        }

        // Lotes por granja asignada: la empresa se toma de farms.company_id (muchas filas legacy tienen lotes.company_id desalineado).
        var lotesQuery = _db.Lotes.AsNoTracking()
            .Join(_db.Farms.AsNoTracking(),
                l => l.GranjaId,
                f => f.Id,
                (l, f) => new { l, f })
            .Where(x => x.l.DeletedAt == null
                        && allowedFarmIds.Contains(x.l.GranjaId)
                        && x.f.CompanyId == cid
                        && x.f.DeletedAt == null);
        if (paisId > 0)
            lotesQuery = lotesQuery.Where(x => x.l.PaisId == null || x.l.PaisId == paisId);

        var lotes = await lotesQuery
            .OrderByDescending(x => x.l.FechaEncaset)
            .ThenBy(x => x.l.LoteNombre)
            .Select(x => new InventarioGestionLoteFiltroDto(
                x.l.LoteId!.Value,
                x.l.LoteNombre,
                x.l.Fase,
                x.l.GranjaId,
                x.l.NucleoId,
                x.l.GalponId))
            .ToListAsync(ct);

        var movBase = _db.InventarioGestionMovimientos.AsNoTracking()
            .Where(m => m.CompanyId == cid && allowedFarmIds.Contains(m.FarmId));
        if (paisId > 0)
            movBase = movBase.Where(m => m.PaisId == paisId);

        var itemsJoin = _db.ItemInventarioEcuador.AsNoTracking();

        var conceptos = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.Concepto != null && i.Concepto != ""
            select i.Concepto!.Trim()
        ).Distinct().ToListAsync(ct);
        conceptos.Sort(StringComparer.OrdinalIgnoreCase);

        var tiposItem = await (
            from m in movBase
            join i in itemsJoin on m.ItemInventarioEcuadorId equals i.Id
            where i.TipoItem != null && i.TipoItem != ""
            select i.TipoItem.Trim()
        ).Distinct().ToListAsync(ct);
        tiposItem.Sort(StringComparer.OrdinalIgnoreCase);

        var estados = await movBase
            .Where(m => m.Estado != null && m.Estado != "")
            .Select(m => m.Estado!.Trim())
            .Distinct()
            .ToListAsync(ct);
        estados.Sort(StringComparer.OrdinalIgnoreCase);

        var movementTypes = await movBase
            .Where(m => m.MovementType != null && m.MovementType != "")
            .Select(m => m.MovementType.Trim())
            .Distinct()
            .ToListAsync(ct);
        movementTypes.Sort(StringComparer.OrdinalIgnoreCase);

        var unidades = await movBase
            .Where(m => m.Unit != null && m.Unit != "")
            .Select(m => m.Unit.Trim())
            .Distinct()
            .ToListAsync(ct);
        unidades.Sort(StringComparer.OrdinalIgnoreCase);

        return new InventarioGestionHistoricoFiltrosDto(
            lotes,
            conceptos,
            tiposItem,
            estados,
            movementTypes,
            unidades,
            TiposOperacionFiltroLabels,
            farmsOrigenHist,
            nucleosOrigenHist,
            galponesOrigenHist);
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
        var movCreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);

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
        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
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
            CreatedAt = movAt,
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
            CreatedAt = movAt,
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

        var movAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
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
            CreatedAt = movAt,
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
            "Traslado entre granjas (recepción)",
            item.Concepto,
            item.TipoItem);

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

    /// <summary>Valida empresa, país y granjas asignadas; carga ítem de catálogo.</summary>
    private async Task<InventarioGestionStock> GetStockForMutationAsync(int stockId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var stock = await _db.InventarioGestionStock
            .Include(x => x.ItemInventarioEcuador)
            .FirstOrDefaultAsync(x => x.Id == stockId, ct);
        if (stock == null)
            throw new InvalidOperationException("El registro de stock no existe.");
        if (stock.CompanyId != companyId.Value)
            throw new InvalidOperationException("No autorizado.");
        var effectivePais = await GetEffectivePaisIdAsync(stock.FarmId, ct);
        if (effectivePais > 0 && stock.PaisId != effectivePais)
            throw new InvalidOperationException("El registro no corresponde al país activo.");
        if (!allowedFarmIds.Contains(stock.FarmId))
            throw new InvalidOperationException("No tiene acceso a esta granja.");
        return stock;
    }

    public async Task<InventarioGestionStockDto> ActualizarStockAsync(int stockId, InventarioGestionStockUpdateRequest req, CancellationToken ct = default)
    {
        if (req.Quantity < 0)
            throw new InvalidOperationException("La cantidad no puede ser negativa.");

        var stock = await GetStockForMutationAsync(stockId, ct);
        var item = stock.ItemInventarioEcuador;
        var oldQty = stock.Quantity;
        var oldUnit = stock.Unit;
        var newUnit = string.IsNullOrWhiteSpace(req.Unit) ? stock.Unit : req.Unit.Trim();
        if (string.IsNullOrWhiteSpace(newUnit))
            newUnit = "kg";

        DateTimeOffset? newCreated = null;
        if (req.FechaIngreso.HasValue)
        {
            var d = req.FechaIngreso.Value.Date;
            newCreated = new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
        }

        var qtyChanged = oldQty != req.Quantity;
        var unitChanged = !string.Equals(oldUnit.Trim(), newUnit.Trim(), StringComparison.OrdinalIgnoreCase);
        var fechaChanged = newCreated.HasValue && stock.CreatedAt.Date != newCreated.Value.Date;

        if (!qtyChanged && !unitChanged && !fechaChanged)
            throw new InvalidOperationException("No hay cambios.");

        if (newCreated.HasValue)
            stock.CreatedAt = newCreated.Value;

        stock.UpdatedAt = DateTimeOffset.UtcNow;

        if (qtyChanged || unitChanged)
        {
            var delta = req.Quantity - oldQty;
            stock.Quantity = req.Quantity;
            stock.Unit = newUnit;

            var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            var reasonFull = $"Ajuste manual. Anterior: {oldQty} {oldUnit}. Nuevo: {req.Quantity} {newUnit}.";
            if (fechaChanged && newCreated.HasValue)
                reasonFull += $" Fecha ingreso: {newCreated.Value:yyyy-MM-dd}.";
            if (extra != null)
                reasonFull += $" Motivo: {extra}";

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = delta != 0 ? Math.Abs(delta) : 0m,
                Unit = newUnit,
                MovementType = "AjusteStock",
                Estado = "Ajuste manual",
                Reference = null,
                Reason = reasonFull,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        await _db.SaveChangesAsync(ct);

        var list = await GetStockAsync(stock.FarmId, stock.NucleoId, stock.GalponId, null, null, ct);
        return list.FirstOrDefault(x => x.Id == stockId)
            ?? new InventarioGestionStockDto(
                stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId,
                item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento",
                stock.Quantity, stock.Unit, null, null, null, stock.CreatedAt);
    }

    public async Task EliminarStockAsync(int stockId, CancellationToken ct = default)
    {
        var stock = await GetStockForMutationAsync(stockId, ct);
        if (stock.Quantity > 0)
        {
            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = stock.Quantity,
                Unit = stock.Unit,
                MovementType = "EliminacionStock",
                Estado = "Eliminación registro",
                Reference = null,
                Reason = "Eliminación del registro de stock desde gestión de inventario.",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        _db.InventarioGestionStock.Remove(stock);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AnularMovimientoHistoricoAsync(int movimientoId, string? motivo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);
        if (mov == null)
            throw new InvalidOperationException("El movimiento no existe o no pertenece a su empresa.");
        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a la granja de este movimiento.");

        var mt = (mov.MovementType ?? "").Trim();
        if (string.Equals(mt, "Consumo", StringComparison.OrdinalIgnoreCase))
        {
            var stock = await _db.InventarioGestionStock
                .FirstOrDefaultAsync(x =>
                    x.FarmId == mov.FarmId &&
                    x.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    x.NucleoId == mov.NucleoId &&
                    x.GalponId == mov.GalponId, ct);
            if (stock == null)
            {
                var (cId, pId) = await GetFarmCompanyAndPaisAsync(mov.FarmId, ct);
                stock = new InventarioGestionStock
                {
                    CompanyId = cId,
                    PaisId = pId,
                    FarmId = mov.FarmId,
                    NucleoId = mov.NucleoId,
                    GalponId = mov.GalponId,
                    ItemInventarioEcuadorId = mov.ItemInventarioEcuadorId,
                    Quantity = mov.Quantity,
                    Unit = mov.Unit,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.InventarioGestionStock.Add(stock);
            }
            else
            {
                stock.Quantity += mov.Quantity;
                stock.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else if (string.Equals(mt, "Ingreso", StringComparison.OrdinalIgnoreCase))
        {
            var stock = await _db.InventarioGestionStock
                .FirstOrDefaultAsync(x =>
                    x.FarmId == mov.FarmId &&
                    x.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    x.NucleoId == mov.NucleoId &&
                    x.GalponId == mov.GalponId, ct);
            if (stock == null || stock.Quantity < mov.Quantity)
                throw new InvalidOperationException(
                    "No se puede anular este ingreso: no hay stock suficiente en la ubicación para revertir la cantidad.");
            stock.Quantity -= mov.Quantity;
            stock.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
            throw new InvalidOperationException(
                "Solo se pueden anular movimientos de tipo Consumo o Ingreso. Use los flujos de traslado/tránsito para corregir otros casos.");

        _db.InventarioGestionMovimientos.Remove(mov);
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

    private static void ApplyUbicacionMovimientoFilter(
        ref IQueryable<InventarioGestionMovimiento> query,
        string? nucleoId,
        string? galponId)
    {
        if (string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == null || x.NucleoId == "");
        else
        {
            var n = nucleoId.Trim();
            query = query.Where(x => x.NucleoId == n);
        }

        if (string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == null || x.GalponId == "");
        else
        {
            var g = galponId.Trim();
            query = query.Where(x => x.GalponId == g);
        }
    }

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
            .Include(x => x.ItemInventarioEcuador)
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
                x.ItemInventarioEcuador.Concepto != null &&
                x.ItemInventarioEcuador.Concepto.Trim().ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(tipoItem))
        {
            var t = tipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemInventarioEcuador.TipoItem != null &&
                x.ItemInventarioEcuador.TipoItem.Trim().ToLower() == t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventarioEcuador.Nombre ?? "").ToLower().Contains(s));
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
                tipoOp,
                x.ItemInventarioEcuador.Concepto,
                x.ItemInventarioEcuador.TipoItem);
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
        "AjusteStock" => "Ajuste manual de stock",
        "EliminacionStock" => "Eliminación de registro de stock",
        _ => movementType
    };

    /// <summary>Inverso de <see cref="MapTipoOperacionLabel"/> para filtro por etiqueta.</summary>
    private static string? ResolveMovementTypeFromTipoOperacionLabel(string label) => label switch
    {
        "Ingreso" => "Ingreso",
        "Consumo" => "Consumo",
        "Traslado (salida entre galpones)" => "TrasladoSalida",
        "Traslado (entrada entre galpones)" => "TrasladoEntrada",
        "Traslado entre granjas (solicitud pendiente)" => "TrasladoInterGranjaPendiente",
        "Traslado entre granjas (en tránsito)" => "TrasladoInterGranjaSalida",
        "Traslado entre granjas (recepción)" => "TrasladoInterGranjaEntrada",
        "Traslado entre granjas (rechazado)" => "TrasladoInterGranjaRechazado",
        "Ajuste manual de stock" => "AjusteStock",
        "Eliminación de registro de stock" => "EliminacionStock",
        _ => null
    };

    // ─── TRASLADOS: LISTADO Y EDICIÓN ────────────────────────────────────────

    /// <summary>
    /// Tipos de movimiento que representan la "salida" de un traslado (son el registro primario del par/grupo).
    /// Para misma-granja: TrasladoSalida. Para inter-granja: TrasladoInterGranjaSalida | TrasladoInterGranjaPendiente | TrasladoInterGranjaRechazado.
    /// </summary>
    private static readonly HashSet<string> TrasladoSalidaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoSalida",
        "TrasladoInterGranjaSalida",
        "TrasladoInterGranjaPendiente",
        "TrasladoInterGranjaRechazado"
    };

    private static readonly HashSet<string> TrasladoEntradaTypes = new(StringComparer.Ordinal)
    {
        "TrasladoEntrada",
        "TrasladoInterGranjaEntrada"
    };

    private static string MapEstadoTraslado(string movementType) => movementType switch
    {
        "TrasladoSalida" or "TrasladoEntrada" => "Completado",
        "TrasladoInterGranjaSalida" => "En tránsito",
        "TrasladoInterGranjaPendiente" => "Pendiente despacho",
        "TrasladoInterGranjaEntrada" => "Completado",
        "TrasladoInterGranjaRechazado" => "Rechazado",
        _ => movementType
    };

    public async Task<List<InventarioGestionTrasladoListDto>> GetTrasladosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionTrasladoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        var salidaTypes = TrasladoSalidaTypes.ToList();

        // Movimientos "salida" (registro primario del traslado)
        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && salidaTypes.Contains(x.MovementType)
                        && (allowedFarmIds.Contains(x.FarmId) || (x.FromFarmId.HasValue && allowedFarmIds.Contains(x.FromFarmId.Value))));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value || x.FromFarmId == farmId.Value);

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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventarioEcuador.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Concepto != null && x.ItemInventarioEcuador.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventarioEcuador.TipoItem != null && x.ItemInventarioEcuador.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId || x.FromNucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId || x.FromGalponId == galponId);

        var salidas = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        if (salidas.Count == 0)
            return new List<InventarioGestionTrasladoListDto>();

        // Cargar entradas correspondientes por TransferGroupId
        var groupIds = salidas
            .Where(x => x.TransferGroupId.HasValue)
            .Select(x => x.TransferGroupId!.Value)
            .Distinct()
            .ToList();

        var entradaTypes = TrasladoEntradaTypes.ToList();
        var entradas = groupIds.Count > 0
            ? await _db.InventarioGestionMovimientos
                .AsNoTracking()
                .Where(x => x.TransferGroupId.HasValue && groupIds.Contains(x.TransferGroupId!.Value) && entradaTypes.Contains(x.MovementType))
                .ToDictionaryAsync(x => x.TransferGroupId!.Value, ct)
            : new Dictionary<Guid, InventarioGestionMovimiento>();

        // Cargar nombres de granjas (origen + destino)
        var allFarmIds = salidas
            .SelectMany(x => new[] { x.FarmId, x.FromFarmId ?? 0 })
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var farmNames = await _db.Farms.AsNoTracking()
            .Where(f => allFarmIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        // Cargar nombres de núcleos y galpones
        var nucleoIds = salidas
            .SelectMany(x => new[] { x.NucleoId, x.FromNucleoId }.Where(n => !string.IsNullOrWhiteSpace(n)))
            .Distinct()
            .ToList();
        var nucleoRows = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && allFarmIds.Contains(n.GranjaId))
                .ToListAsync(ct)
            : new List<Nucleo>();
        var nucleoDict = nucleoRows.ToDictionary(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre);

        var galponIds = salidas
            .SelectMany(x => new[] { x.GalponId, x.FromGalponId }.Where(g => !string.IsNullOrWhiteSpace(g)))
            .Distinct()
            .ToList();
        var galponRows = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && allFarmIds.Contains(g.GranjaId))
                .ToListAsync(ct)
            : new List<Galpon>();
        var galponDict = galponRows.ToDictionary(g => (g.GalponId, g.GranjaId), g => g.GalponNombre);

        return salidas.Select(s =>
        {
            farmNames.TryGetValue(s.FarmId, out var fromGranjaName);
            var toFarmId = s.FromFarmId ?? 0;
            farmNames.TryGetValue(toFarmId, out var toGranjaName);

            string? fromNucleoNombre = s.NucleoId != null && nucleoDict.TryGetValue((s.NucleoId, s.FarmId), out var fnn) ? fnn : null;
            string? fromGalponNombre = s.GalponId != null && galponDict.TryGetValue((s.GalponId, s.FarmId), out var fgn) ? fgn : null;
            string? toNucleoNombre = s.FromNucleoId != null && nucleoDict.TryGetValue((s.FromNucleoId, toFarmId), out var tnn) ? tnn : null;
            string? toGalponNombre = s.FromGalponId != null && galponDict.TryGetValue((s.FromGalponId, toFarmId), out var tgn) ? tgn : null;

            int? entradaId = s.TransferGroupId.HasValue && entradas.TryGetValue(s.TransferGroupId.Value, out var entrada) ? entrada.Id : null;
            var estado = MapEstadoTraslado(s.MovementType);

            return new InventarioGestionTrasladoListDto(
                s.TransferGroupId ?? Guid.Empty,
                s.Id,
                entradaId,
                s.FarmId,
                fromGranjaName,
                s.NucleoId,
                fromNucleoNombre,
                s.GalponId,
                fromGalponNombre,
                toFarmId,
                toGranjaName,
                s.FromNucleoId,
                toNucleoNombre,
                s.FromGalponId,
                toGalponNombre,
                s.ItemInventarioEcuadorId,
                s.ItemInventarioEcuador.Codigo,
                s.ItemInventarioEcuador.Nombre,
                s.ItemInventarioEcuador.Concepto ?? s.ItemInventarioEcuador.TipoItem ?? "alimento",
                s.ItemInventarioEcuador.TipoItem ?? "alimento",
                s.Quantity,
                s.Unit,
                s.Reference,
                s.Reason,
                estado,
                s.CreatedAt,
                s.CreatedAt);
        }).ToList();
    }

    public async Task<InventarioGestionTrasladoListDto> ActualizarFechaTrasladoAsync(
        Guid transferGroupId,
        InventarioGestionActualizarFechaTrasladoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);

        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) && !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        var nuevaFecha = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        foreach (var mov in movimientos)
            mov.CreatedAt = nuevaFecha;

        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        if (histTraslado.Count > 0)
        {
            var fechaDate = nuevaFecha.UtcDateTime.Date;
            foreach (var h in histTraslado)
                h.FechaOperacion = fechaDate;
            await _db.SaveChangesAsync(ct);
        }

        // Recargar y retornar el DTO actualizado
        var result = await GetTrasladosAsync(farmId: salida.FarmId, ct: ct);
        return result.FirstOrDefault(x => x.TransferGroupId == transferGroupId)
            ?? throw new InvalidOperationException("Error al recargar el traslado actualizado.");
    }

    // ─── INGRESOS: LISTADO Y EDICIÓN ─────────────────────────────────────────

    public async Task<List<InventarioGestionIngresoListDto>> GetIngresosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionIngresoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var ingresoTypes = new[] { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && ingresoTypes.Contains(x.MovementType)
                        && allowedFarmIds.Contains(x.FarmId));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventarioEcuador.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventarioEcuador.Concepto != null && x.ItemInventarioEcuador.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventarioEcuador.TipoItem != null && x.ItemInventarioEcuador.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        if (list.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var farmIds = list.Select(x => x.FarmId).Distinct().ToList();
        var nucleoIds = list.Where(x => !string.IsNullOrWhiteSpace(x.NucleoId)).Select(x => x.NucleoId!).Distinct().ToList();
        var galponIds = list.Where(x => !string.IsNullOrWhiteSpace(x.GalponId)).Select(x => x.GalponId!).Distinct().ToList();

        var nucleos = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && farmIds.Contains(n.GranjaId))
                .ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct)
            : new Dictionary<(string, int), string>();

        var galpones = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && farmIds.Contains(g.GranjaId))
                .ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct)
            : new Dictionary<(string, int), string>();

        return list.Select(x =>
        {
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;

            return new InventarioGestionIngresoListDto(
                x.Id,
                x.FarmId,
                x.Farm.Name,
                x.NucleoId,
                nucleoNombre,
                x.GalponId,
                galponNombre,
                x.ItemInventarioEcuadorId,
                x.ItemInventarioEcuador.Codigo,
                x.ItemInventarioEcuador.Nombre,
                x.ItemInventarioEcuador.Concepto ?? x.ItemInventarioEcuador.TipoItem ?? "alimento",
                x.ItemInventarioEcuador.TipoItem ?? "alimento",
                x.Quantity,
                x.Unit,
                x.Reference,
                x.Reason,
                x.Estado,
                x.CreatedAt,
                x.CreatedAt);
        }).ToList();
    }

    public async Task<InventarioGestionIngresoListDto> ActualizarFechaIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarFechaIngresoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventarioEcuador)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposEntradaEditables = new HashSet<string>(StringComparer.Ordinal) { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposEntradaEditables.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se puede editar la fecha de movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        mov.CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var fechaDateIngreso = mov.CreatedAt.UtcDateTime.Date;
        var histIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histIngreso != null)
        {
            histIngreso.FechaOperacion = fechaDateIngreso;
        }
        else
        {
            // Fallback: identificar por granja + nucleo + galpon + item + cantidad sin estar anulado
            var histFallback = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
            if (histFallback != null)
                histFallback.FechaOperacion = fechaDateIngreso;
        }
        await _db.SaveChangesAsync(ct);

        string? nucleoNombre = null;
        string? galponNombre = null;
        if (mov.NucleoId != null)
            nucleoNombre = await _db.Nucleos.AsNoTracking()
                .Where(n => n.NucleoId == mov.NucleoId && n.GranjaId == mov.FarmId)
                .Select(n => n.NucleoNombre)
                .FirstOrDefaultAsync(ct);
        if (mov.GalponId != null)
            galponNombre = await _db.Galpones.AsNoTracking()
                .Where(g => g.GalponId == mov.GalponId && g.GranjaId == mov.FarmId)
                .Select(g => g.GalponNombre)
                .FirstOrDefaultAsync(ct);

        return new InventarioGestionIngresoListDto(
            mov.Id,
            mov.FarmId,
            mov.Farm.Name,
            mov.NucleoId,
            nucleoNombre,
            mov.GalponId,
            galponNombre,
            mov.ItemInventarioEcuadorId,
            mov.ItemInventarioEcuador.Codigo,
            mov.ItemInventarioEcuador.Nombre,
            mov.ItemInventarioEcuador.Concepto ?? mov.ItemInventarioEcuador.TipoItem ?? "alimento",
            mov.ItemInventarioEcuador.TipoItem ?? "alimento",
            mov.Quantity,
            mov.Unit,
            mov.Reference,
            mov.Reason,
            mov.Estado,
            mov.CreatedAt,
            mov.CreatedAt);
    }

    // ─── ELIMINAR INGRESO ─────────────────────────────────────────────────────

    /// <summary>
    /// Elimina un movimiento de tipo Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente el registro de inventario_gestion_movimiento.
    /// </summary>
    public async Task EliminarIngresoAsync(int movimientoId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);
        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposIngreso = new HashSet<string>(StringComparer.Ordinal)
            { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposIngreso.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se pueden eliminar movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        // Marcar anulado en tabla espejo (auditoría)
        var histElimIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h =>
                h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histElimIngreso == null)
        {
            // Fallback: buscar por granja + nucleo + galpon + item + cantidad sin estar anulado
            histElimIngreso = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
        }
        if (histElimIngreso != null)
            histElimIngreso.Anulado = true;

        _db.InventarioGestionMovimientos.Remove(mov);
        await _db.SaveChangesAsync(ct);
    }

    // ─── ELIMINAR TRASLADO ────────────────────────────────────────────────────

    /// <summary>
    /// Elimina todos los movimientos de un TransferGroupId.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente todos los registros de inventario_gestion_movimiento del grupo.
    /// </summary>
    public async Task EliminarTrasladoAsync(Guid transferGroupId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var movimientos = await _db.InventarioGestionMovimientos
            .Where(x => x.TransferGroupId == transferGroupId && x.CompanyId == companyId.Value)
            .ToListAsync(ct);
        if (movimientos.Count == 0)
            throw new InvalidOperationException("No se encontró el traslado indicado.");

        var salida = movimientos.FirstOrDefault(x => TrasladoSalidaTypes.Contains(x.MovementType));
        if (salida == null)
            throw new InvalidOperationException("El TransferGroupId no corresponde a un traslado.");

        if (!allowedFarmIds.Contains(salida.FarmId) &&
            !(salida.FromFarmId.HasValue && allowedFarmIds.Contains(salida.FromFarmId.Value)))
            throw new InvalidOperationException("No tiene acceso a este traslado.");

        // Marcar anulado en tabla espejo para todos los movimientos del grupo
        var movIds = movimientos.Select(m => m.Id).ToList();
        var histElimTraslado = await _db.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == "inventario_gestion_movimiento" && movIds.Contains(h.OrigenId))
            .ToListAsync(ct);
        foreach (var h in histElimTraslado)
            h.Anulado = true;

        _db.InventarioGestionMovimientos.RemoveRange(movimientos);
        await _db.SaveChangesAsync(ct);
    }
}
