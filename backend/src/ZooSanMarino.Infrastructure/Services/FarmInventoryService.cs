// src/ZooSanMarino.Infrastructure/Services/FarmInventoryService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;
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

    /// <summary>
    /// Helper para incluir ItemType en CatalogItemMetadata
    /// </summary>
    private JsonDocument MergeItemTypeIntoMetadata(JsonDocument? metadata, string itemType)
    {
        if (metadata != null && metadata.RootElement.ValueKind == JsonValueKind.Object)
        {
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in metadata.RootElement.EnumerateObject())
                {
                    prop.WriteTo(writer);
                }
                writer.WriteString("itemType", itemType);
                writer.WriteEndObject();
            }
            return JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        }
        else
        {
            return JsonDocument.Parse($"{{\"itemType\":\"{itemType}\"}}");
        }
    }

    public async Task<List<FarmInventoryDto>> GetByFarmAsync(int farmId, string? q, string? itemType = null, CancellationToken ct = default)
    {
        // Validar granja y que pertenezca a la empresa del usuario
        var farm = await _db.Set<Farm>()
            .FirstOrDefaultAsync(f => f.Id == farmId, ct);
        
        if (farm == null) return new List<FarmInventoryDto>();

        // Filtrar por empresa del usuario actual (si está disponible)
        if (_current != null && _current.CompanyId > 0)
        {
            if (farm.CompanyId != _current.CompanyId)
            {
                // La granja no pertenece a la empresa del usuario
                return new List<FarmInventoryDto>();
            }
        }

        // 👇 Declarar como IQueryable para evitar el conflicto con Include/Where
        IQueryable<FarmProductInventory> query = _db.FarmProductInventory
            .AsNoTracking()
            .Where(x => x.FarmId == farmId);

        // Filtrar por empresa y país del usuario actual (si está disponible)
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }

        // Incluir CatalogItem para poder filtrar por itemType
        query = query.Include(x => x.CatalogItem);

        // Filtrar por tipo de item del catálogo si se proporciona
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            query = query.Where(x => x.CatalogItem.ItemType == itemType);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.CatalogItem.Nombre, $"%{q}%") ||
                EF.Functions.ILike(x.CatalogItem.Codigo, $"%{q}%"));
        }

        var items = await query
            .OrderBy(x => x.CatalogItem.Codigo)
            .Select(x => new
            {
                Inventory = x,
                CatalogItemType = x.CatalogItem.ItemType
            })
            .ToListAsync(ct);

        // Construir el DTO incluyendo ItemType en CatalogItemMetadata
        var result = items.Select(x => new FarmInventoryDto
        {
            Id = x.Inventory.Id,
            FarmId = x.Inventory.FarmId,
            CatalogItemId = x.Inventory.CatalogItemId,
            Codigo = x.Inventory.CatalogItem.Codigo,
            Nombre = x.Inventory.CatalogItem.Nombre,
            Quantity = x.Inventory.Quantity,
            Unit = x.Inventory.Unit,
            Location = x.Inventory.Location,
            LotNumber = x.Inventory.LotNumber,
            ExpirationDate = x.Inventory.ExpirationDate,
            UnitCost = x.Inventory.UnitCost,
            Metadata = x.Inventory.Metadata,
            CatalogItemMetadata = MergeItemTypeIntoMetadata(x.Inventory.CatalogItem.Metadata, x.CatalogItemType),
            Active = x.Inventory.Active,
            ResponsibleUserId = x.Inventory.ResponsibleUserId,
            CreatedAt = x.Inventory.CreatedAt,
            UpdatedAt = x.Inventory.UpdatedAt
        }).ToList();

        return result;
    }

    public async Task<FarmInventoryDto?> GetByIdAsync(int farmId, int id, CancellationToken ct = default)
    {
        // Validar que la granja pertenezca a la empresa del usuario
        var farm = await _db.Set<Farm>()
            .FirstOrDefaultAsync(f => f.Id == farmId, ct);
        
        if (farm == null) return null;

        // Filtrar por empresa del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            if (farm.CompanyId != _current.CompanyId)
            {
                return null;
            }
        }

        var query = _db.FarmProductInventory
            .AsNoTracking()
            .Include(p => p.CatalogItem)
            .Where(p => p.Id == id && p.FarmId == farmId);

        // Filtrar por empresa y país del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }

        var x = await query.FirstOrDefaultAsync(ct);

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
            CatalogItemMetadata = MergeItemTypeIntoMetadata(x.CatalogItem.Metadata, x.CatalogItem.ItemType),
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
            CatalogItemMetadata = MergeItemTypeIntoMetadata(x.CatalogItem.Metadata, x.CatalogItem.ItemType),
            Active = x.Active,
            ResponsibleUserId = x.ResponsibleUserId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };
    }

    public async Task<FarmInventoryDto> CreateOrReplaceAsync(int farmId, FarmInventoryCreateRequest req, CancellationToken ct = default)
    {
        // 1) Validar granja y que pertenezca a la empresa del usuario
        var farm = await _db.Set<Farm>().FirstOrDefaultAsync(f => f.Id == farmId, ct)
                   ?? throw new InvalidOperationException("La granja no existe.");

        // Validar que la granja pertenezca a la empresa del usuario
        if (_current != null && _current.CompanyId > 0)
        {
            if (farm.CompanyId != _current.CompanyId)
            {
                throw new UnauthorizedAccessException("La granja no pertenece a su empresa.");
            }
        }

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
            // Obtener CompanyId y PaisId de la granja
            var companyId = farm.CompanyId;
            var paisId = await _db.Set<Departamento>()
                .Where(d => d.DepartamentoId == farm.DepartamentoId)
                .Select(d => d.PaisId)
                .FirstOrDefaultAsync(ct);

            var e = new FarmProductInventory
            {
                FarmId = farm.Id,
                CatalogItemId = catalogItemId,
                CompanyId = companyId,
                PaisId = paisId > 0 ? paisId : (_current?.PaisId ?? 0),
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
