using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CatalogItemService : ICatalogItemService
{
    private readonly ZooSanMarinoContext _db;
    public CatalogItemService(ZooSanMarinoContext db) => _db = db;

    public async Task<PagedResult<CatalogItemDto>> GetAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var query = _db.CatalogItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Nombre, $"%{q}%") ||
                EF.Functions.ILike(x.Codigo, $"%{q}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Metadata = x.Metadata,
                Activo = x.Activo
            })
            .ToListAsync(ct);

        return new PagedResult<CatalogItemDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CatalogItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var x = await _db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (x is null) return null;

        return new CatalogItemDto
        {
            Id = x.Id,
            Codigo = x.Codigo,
            Nombre = x.Nombre,
            Metadata = x.Metadata,
            Activo = x.Activo
        };
    }

    public async Task<CatalogItemDto?> CreateAsync(CatalogItemCreateRequest dto, CancellationToken ct = default)
    {
        var codigo = dto.Codigo.Trim();
        var nombre = dto.Nombre.Trim();

        var exists = await _db.CatalogItems.AnyAsync(x => x.Codigo == codigo, ct);
        if (exists) return null; // conflicto de código duplicado

        var e = new CatalogItem
        {
            Codigo = codigo,
            Nombre = nombre,
            Metadata = dto.Metadata ?? System.Text.Json.JsonDocument.Parse("{}"),
            Activo = dto.Activo
        };

        _db.CatalogItems.Add(e);
        await _db.SaveChangesAsync(ct);

        return new CatalogItemDto
        {
            Id = e.Id,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            Metadata = e.Metadata,
            Activo = e.Activo
        };
    }

    public async Task<CatalogItemDto?> UpdateAsync(int id, CatalogItemUpdateRequest dto, CancellationToken ct = default)
    {
        var e = await _db.CatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;

        e.Nombre = dto.Nombre.Trim();
        e.Activo = dto.Activo;
        e.Metadata = dto.Metadata ?? e.Metadata;

        await _db.SaveChangesAsync(ct);

        return new CatalogItemDto
        {
            Id = e.Id,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            Metadata = e.Metadata,
            Activo = e.Activo
        };
    }

    public async Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default)
    {
        var e = await _db.CatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;

        if (hard)
        {
            _db.CatalogItems.Remove(e);
        }
        else
        {
            e.Activo = false;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
    
    // src/ZooSanMarino.Infrastructure/Services/CatalogItemService.cs
    public async Task<List<CatalogItemDto>> GetAllAsync(string? q, CancellationToken ct = default)
    {
        var query = _db.CatalogItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Nombre, $"%{q}%") ||
                EF.Functions.ILike(x.Codigo, $"%{q}%"));
        }

        var items = await query
            .OrderBy(x => x.Id)
            .Select(x => new CatalogItemDto
            {
                Id       = x.Id,
                Codigo   = x.Codigo,
                Nombre   = x.Nombre,
                Metadata = x.Metadata,
                Activo   = x.Activo
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<List<CatalogItemDto>> GetByTypeAsync(string? typeItem, string? search, CancellationToken ct = default)
    {
        var query = _db.CatalogItems.AsNoTracking().Where(x => x.Activo);

        // Filtrar por búsqueda (código o nombre) - esto sí se puede hacer en la query
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Nombre, $"%{search}%") ||
                EF.Functions.ILike(x.Codigo, $"%{search}%"));
        }

        // Obtener todos los items activos (con filtro de búsqueda si aplica)
        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                Metadata = x.Metadata,
                Activo = x.Activo
            })
            .ToListAsync(ct);

        // Filtrar por tipo de item en memoria (porque TryGetProperty no se puede usar en expression trees)
        if (!string.IsNullOrWhiteSpace(typeItem))
        {
            items = items.Where(x =>
            {
                if (x.Metadata == null) return false;
                if (!x.Metadata.RootElement.TryGetProperty("type_item", out var typeItemProp))
                    return false;
                return typeItemProp.GetString() == typeItem;
            }).ToList();
        }

        return items;
    }

}
