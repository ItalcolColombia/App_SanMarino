using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Domain.Enums;
using ZooSanMarino.Infrastructure.Persistence;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;


namespace ZooSanMarino.Infrastructure.Services;

public class FarmInventoryMovementService : IFarmInventoryMovementService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;

    public FarmInventoryMovementService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db; _current = current;
    }

    private async Task<int> ResolveItemIdAsync(int? catalogItemId, string? codigo, CancellationToken ct)
    {
        if (catalogItemId.HasValue)
        {
            var exists = await _db.CatalogItems.AnyAsync(c => c.Id == catalogItemId.Value, ct);
            if (!exists) throw new InvalidOperationException("El producto no existe.");
            return catalogItemId.Value;
        }
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var item = await _db.CatalogItems.FirstOrDefaultAsync(c => c.Codigo == codigo.Trim(), ct);
            if (item == null) throw new InvalidOperationException("El producto (codigo) no existe.");
            return item.Id;
        }
        throw new InvalidOperationException("Debe especificar CatalogItemId o Codigo.");
    }

    private static void EnsurePositive(decimal qty)
    {
        if (qty <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
    }

    private async Task<FarmProductInventory> GetOrCreateInventoryAsync(int farmId, int itemId, string unit, CancellationToken ct)
    {
        var inv = await _db.FarmProductInventory.FirstOrDefaultAsync(x => x.FarmId == farmId && x.CatalogItemId == itemId, ct);
        if (inv != null) return inv;

        inv = new FarmProductInventory
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            Quantity = 0,
            Unit = string.IsNullOrWhiteSpace(unit) ? "kg" : unit.Trim(),
            Metadata = JsonDocument.Parse("{}"),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.FarmProductInventory.Add(inv);
        await _db.SaveChangesAsync(ct);
        return inv;
    }

    private async Task<InventoryMovementDto> MapMovementAsync(FarmInventoryMovement m, CancellationToken ct)
    {
        var item = await _db.CatalogItems.AsNoTracking().FirstAsync(x => x.Id == m.CatalogItemId, ct);
        return new InventoryMovementDto
        {
            Id = m.Id,
            FarmId = m.FarmId,
            CatalogItemId = m.CatalogItemId,
            Codigo = item.Codigo,
            Nombre = item.Nombre,
            Quantity = m.Quantity,
            MovementType = m.MovementType.ToString(),
            Unit = m.Unit,
            Reference = m.Reference,
            Reason = m.Reason,
            TransferGroupId = m.TransferGroupId,
            Metadata = m.Metadata,
            ResponsibleUserId = m.ResponsibleUserId,
            CreatedAt = m.CreatedAt
        };
    }

    public async Task<InventoryMovementDto> PostEntryAsync(int farmId, InventoryEntryRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        inv.Quantity += req.Quantity;
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.Entry,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapMovementAsync(mov, ct);
    }

    public async Task<InventoryMovementDto> PostExitAsync(int farmId, InventoryExitRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        if (inv.Quantity < req.Quantity) throw new InvalidOperationException("Stock insuficiente para la salida.");
        inv.Quantity -= req.Quantity;
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.Exit,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapMovementAsync(mov, ct);
    }

    public async Task<(InventoryMovementDto Out, InventoryMovementDto In)> PostTransferAsync(int fromFarmId, InventoryTransferRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        if (req.ToFarmId == fromFarmId) throw new InvalidOperationException("La granja destino debe ser diferente.");
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;
        var group = Guid.NewGuid();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // OUT (origen)
        var invFrom = await GetOrCreateInventoryAsync(fromFarmId, itemId, unit, ct);
        if (invFrom.Quantity < req.Quantity) throw new InvalidOperationException("Stock insuficiente en la granja origen.");
        invFrom.Quantity -= req.Quantity;
        invFrom.Unit = unit;
        invFrom.UpdatedAt = DateTimeOffset.UtcNow;

        var movOut = new FarmInventoryMovement
        {
            FarmId = fromFarmId,
            CatalogItemId = itemId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.TransferOut,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            TransferGroupId = group,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(movOut);

        // IN (destino)
        var invTo = await GetOrCreateInventoryAsync(req.ToFarmId, itemId, unit, ct);
        invTo.Quantity += req.Quantity;
        invTo.Unit = unit;
        invTo.UpdatedAt = DateTimeOffset.UtcNow;

        var movIn = new FarmInventoryMovement
        {
            FarmId = req.ToFarmId,
            CatalogItemId = itemId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.TransferIn,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            TransferGroupId = group,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(movIn);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (await MapMovementAsync(movOut, ct), await MapMovementAsync(movIn, ct));
    }


    public async Task<InventoryMovementDto> PostAdjustAsync(int farmId, InventoryAdjustRequest req, CancellationToken ct = default)
    {
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        inv.Quantity += req.Quantity; // puede ser + o −
        if (inv.Quantity < 0) throw new InvalidOperationException("El saldo no puede ser negativo.");
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            Quantity = Math.Abs(req.Quantity),
            MovementType = req.Quantity >= 0 ? InventoryMovementType.Adjust : InventoryMovementType.Adjust,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await MapMovementAsync(mov, ct);
    }

  public async Task<CommonDtos.PagedResult<InventoryMovementDto>> GetPagedAsync(
        int farmId, MovementQuery q, CancellationToken ct = default)
    {
        var query = _db.FarmInventoryMovements.AsNoTracking().Where(m => m.FarmId == farmId);

        if (q.From.HasValue)               query = query.Where(m => m.CreatedAt >= q.From.Value);
        if (q.To.HasValue)                 query = query.Where(m => m.CreatedAt <= q.To.Value);
        if (q.CatalogItemId.HasValue)      query = query.Where(m => m.CatalogItemId == q.CatalogItemId.Value);
        if (!string.IsNullOrWhiteSpace(q.Codigo))
                                          query = query.Where(m => m.CatalogItem.Codigo == q.Codigo);
        if (!string.IsNullOrWhiteSpace(q.Type) &&
            Enum.TryParse<Domain.Enums.InventoryMovementType>(q.Type, out var mt))
                                          query = query.Where(m => m.MovementType == mt);

        var total = await query.LongCountAsync(ct);
        var page  = q.Page <= 0 ? 1 : q.Page;
        var size  = (q.PageSize <= 0 || q.PageSize > 200) ? 20 : q.PageSize;

        var list = await query.OrderByDescending(m => m.CreatedAt)
                              .Skip((page - 1) * size)
                              .Take(size)
                              .Select(m => new InventoryMovementDto
                              {
                                  Id = m.Id,
                                  FarmId = m.FarmId,
                                  CatalogItemId = m.CatalogItemId,
                                  Codigo = m.CatalogItem.Codigo,
                                  Nombre = m.CatalogItem.Nombre,
                                  Quantity = m.Quantity,
                                  MovementType = m.MovementType.ToString(),
                                  Unit = m.Unit,
                                  Reference = m.Reference,
                                  Reason = m.Reason,
                                  TransferGroupId = m.TransferGroupId,
                                  Metadata = m.Metadata,
                                  ResponsibleUserId = m.ResponsibleUserId,
                                  CreatedAt = m.CreatedAt
                              })
                              .ToListAsync(ct);

        return new CommonDtos.PagedResult<InventoryMovementDto>
        {
            Items = list,
            Total = total,
            Page = page,
            PageSize = size
        };
    }
    public async Task<InventoryMovementDto?> GetByIdAsync(int farmId, int movementId, CancellationToken ct = default)
    {
        var m = await _db.FarmInventoryMovements
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.Id == movementId && x.FarmId == farmId, ct);
        if (m is null) return null;

        return new InventoryMovementDto {
            Id = m.Id, FarmId = m.FarmId, CatalogItemId = m.CatalogItemId,
            Codigo = m.CatalogItem.Codigo, Nombre = m.CatalogItem.Nombre,
            Quantity = m.Quantity, MovementType = m.MovementType.ToString(),
            Unit = m.Unit, Reference = m.Reference, Reason = m.Reason,
            TransferGroupId = m.TransferGroupId, Metadata = m.Metadata,
            ResponsibleUserId = m.ResponsibleUserId, CreatedAt = m.CreatedAt
        };
    }

    }
