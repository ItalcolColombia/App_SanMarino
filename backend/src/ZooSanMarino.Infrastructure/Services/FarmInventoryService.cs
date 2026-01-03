// src/ZooSanMarino.Infrastructure/Services/FarmInventoryService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class FarmInventoryService : IFarmInventoryService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;

    public FarmInventoryService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<FarmInventoryDto>> GetByFarmAsync(int farmId, string? q, CancellationToken ct = default)
    {
        // Validar granja
        var farmExists = await _db.Set<Farm>().AnyAsync(f => f.Id == farmId, ct);
        if (!farmExists) return new List<FarmInventoryDto>();

        // 👇 Declarar como IQueryable para evitar el conflicto con Include/Where
        IQueryable<FarmProductInventory> query = _db.FarmProductInventory
            .AsNoTracking()
            .Where(x => x.FarmId == farmId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.CatalogItem.Nombre, $"%{q}%") ||
                EF.Functions.ILike(x.CatalogItem.Codigo, $"%{q}%"));
        }

        // NOTA: No es necesario Include si proyectamos propiedades del nav en Select;
        // EF genera el JOIN automático. Si igual quisieras Include: query = query.Include(x => x.CatalogItem);

        var items = await query
            .OrderBy(x => x.CatalogItem.Codigo)
            .Select(x => new FarmInventoryDto
            {
                Id = x.Id,
                FarmId = x.FarmId,
                CatalogItemId = x.CatalogItemId,
                Codigo = x.CatalogItem.Codigo,
                Nombre = x.CatalogItem.Nombre,
                Quantity = x.Quantity,
                Unit = x.Unit,
                Location = x.Location,
                LotNumber = x.LotNumber,
                ExpirationDate = x.ExpirationDate,
                UnitCost = x.UnitCost,
                Metadata = x.Metadata,
                CatalogItemMetadata = x.CatalogItem.Metadata,
                Active = x.Active,
                ResponsibleUserId = x.ResponsibleUserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<FarmInventoryDto?> GetByIdAsync(int farmId, int id, CancellationToken ct = default)
    {
        var x = await _db.FarmProductInventory
            .AsNoTracking()
            .Include(p => p.CatalogItem)
            .FirstOrDefaultAsync(p => p.Id == id && p.FarmId == farmId, ct);

        if (x == null) return null;

        return new FarmInventoryDto
        {
            Id = x.Id,
            FarmId = x.FarmId,
            CatalogItemId = x.CatalogItemId,
            Codigo = x.CatalogItem.Codigo,
            Nombre = x.CatalogItem.Nombre,
            Quantity = x.Quantity,
            Unit = x.Unit,
            Location = x.Location,
            LotNumber = x.LotNumber,
            ExpirationDate = x.ExpirationDate,
            UnitCost = x.UnitCost,
            Metadata = x.Metadata,
            CatalogItemMetadata = x.CatalogItem.Metadata,
            Active = x.Active,
            ResponsibleUserId = x.ResponsibleUserId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }

    public async Task<FarmInventoryDto?> GetByFarmAndCatalogItemAsync(int farmId, int catalogItemId, CancellationToken ct = default)
    {
        var x = await _db.FarmProductInventory
            .AsNoTracking()
            .Include(p => p.CatalogItem)
            .FirstOrDefaultAsync(p => p.FarmId == farmId && p.CatalogItemId == catalogItemId && p.Active, ct);

        if (x == null) return null;

        return new FarmInventoryDto
        {
            Id = x.Id,
            FarmId = x.FarmId,
            CatalogItemId = x.CatalogItemId,
            Codigo = x.CatalogItem.Codigo,
            Nombre = x.CatalogItem.Nombre,
            Quantity = x.Quantity,
            Unit = x.Unit,
            Location = x.Location,
            LotNumber = x.LotNumber,
            ExpirationDate = x.ExpirationDate,
            UnitCost = x.UnitCost,
            Metadata = x.Metadata,
            CatalogItemMetadata = x.CatalogItem.Metadata,
            Active = x.Active,
            ResponsibleUserId = x.ResponsibleUserId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }

    public async Task<FarmInventoryDto> CreateOrReplaceAsync(int farmId, FarmInventoryCreateRequest req, CancellationToken ct = default)
    {
        // 1) Validar granja
        var farm = await _db.Set<Farm>().FirstOrDefaultAsync(f => f.Id == farmId, ct)
                   ?? throw new InvalidOperationException("La granja no existe.");

        // 2) Resolver item
        int catalogItemId;
        if (req.CatalogItemId.HasValue)
        {
            catalogItemId = req.CatalogItemId.Value;
            var itemExists = await _db.CatalogItems.AnyAsync(c => c.Id == catalogItemId, ct);
            if (!itemExists) throw new InvalidOperationException("El producto no existe.");
        }
        else if (!string.IsNullOrWhiteSpace(req.Codigo))
        {
            var code = req.Codigo.Trim();
            var item = await _db.CatalogItems.FirstOrDefaultAsync(c => c.Codigo == code, ct)
                       ?? throw new InvalidOperationException("El producto (codigo) no existe.");
            catalogItemId = item.Id;
        }
        else
        {
            throw new InvalidOperationException("Debe especificar CatalogItemId o Codigo.");
        }

        if (req.Quantity < 0) throw new InvalidOperationException("Quantity no puede ser negativa.");
        var now = DateTimeOffset.UtcNow;

        // 3) Upsert por (FarmId, CatalogItemId)
        var existing = await _db.FarmProductInventory
            .FirstOrDefaultAsync(x => x.FarmId == farmId && x.CatalogItemId == catalogItemId, ct);

        // 👇 Convertir int (UserId) a string antes de usar con ?? (que opera con strings)
        string? responsible = _current != null ? _current.UserId.ToString() : req.ResponsibleUserId;

        if (existing is null)
        {
            var e = new FarmProductInventory
            {
                FarmId = farm.Id,
                CatalogItemId = catalogItemId,
                Quantity = req.Quantity,
                Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim(),
                Location = req.Location?.Trim(),
                LotNumber = req.LotNumber?.Trim(),
                ExpirationDate = req.ExpirationDate,
                UnitCost = req.UnitCost,
                Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
                Active = req.Active,
                ResponsibleUserId = responsible,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.FarmProductInventory.Add(e);
            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(farmId, e.Id, ct))!;
        }
        else
        {
            existing.Quantity = req.Quantity;
            existing.Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
            existing.Location = req.Location?.Trim();
            existing.LotNumber = req.LotNumber?.Trim();
            existing.ExpirationDate = req.ExpirationDate;
            existing.UnitCost = req.UnitCost;
            existing.Metadata = req.Metadata ?? existing.Metadata;
            existing.Active = req.Active;
            existing.ResponsibleUserId = responsible ?? existing.ResponsibleUserId;
            existing.UpdatedAt = now;

            await _db.SaveChangesAsync(ct);
            return (await GetByIdAsync(farmId, existing.Id, ct))!;
        }
    }

    public async Task<FarmInventoryDto?> UpdateAsync(int farmId, int id, FarmInventoryUpdateRequest req, CancellationToken ct = default)
    {
        var e = await _db.FarmProductInventory.FirstOrDefaultAsync(x => x.Id == id && x.FarmId == farmId, ct);
        if (e is null) return null;

        if (req.Quantity < 0) throw new InvalidOperationException("Quantity no puede ser negativa.");

        e.Quantity = req.Quantity;
        e.Unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        e.Location = req.Location?.Trim();
        e.LotNumber = req.LotNumber?.Trim();
        e.ExpirationDate = req.ExpirationDate;
        e.UnitCost = req.UnitCost;
        e.Metadata = req.Metadata ?? e.Metadata;
        e.Active = req.Active;
        e.ResponsibleUserId = req.ResponsibleUserId ?? e.ResponsibleUserId;
        e.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(farmId, id, ct);
    }

    public async Task<bool> DeleteAsync(int farmId, int id, bool hard = false, CancellationToken ct = default)
    {
        var e = await _db.FarmProductInventory.FirstOrDefaultAsync(x => x.Id == id && x.FarmId == farmId, ct);
        if (e is null) return false;

        if (hard)
            _db.FarmProductInventory.Remove(e);
        else
        {
            e.Active = false;
            e.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
