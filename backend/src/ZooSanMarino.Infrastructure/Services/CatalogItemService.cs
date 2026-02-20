using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class CatalogItemService : ICatalogItemService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    
    public CatalogItemService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<CatalogItemDto>> GetAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var query = _db.CatalogItems.AsNoTracking();

        // Filtrar por empresa y país del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }

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
                ItemType = x.ItemType,
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
        var query = _db.CatalogItems.AsNoTracking().Where(i => i.Id == id);
        
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
        if (x is null) return null;

        return new CatalogItemDto
        {
            Id = x.Id,
            Codigo = x.Codigo,
            Nombre = x.Nombre,
            ItemType = x.ItemType,
            Metadata = x.Metadata,
            Activo = x.Activo
        };
    }

    public async Task<CatalogItemDto?> CreateAsync(CatalogItemCreateRequest dto, CancellationToken ct = default)
    {
        var codigo = dto.Codigo.Trim();
        var nombre = dto.Nombre.Trim();

        // Obtener CompanyId y PaisId de la sesión actual
        if (_current == null || _current.CompanyId <= 0)
        {
            throw new InvalidOperationException("No se puede crear un producto sin empresa activa en la sesión.");
        }

        var companyId = _current.CompanyId;
        var paisId = _current.PaisId ?? 0;
        
        if (paisId <= 0)
        {
            throw new InvalidOperationException("No se puede crear un producto sin país activo en la sesión.");
        }

        // Verificar que el código no exista para esta empresa y país
        var exists = await _db.CatalogItems.AnyAsync(x => x.Codigo == codigo && x.CompanyId == companyId && x.PaisId == paisId, ct);
        if (exists) return null; // conflicto de código duplicado

        var e = new CatalogItem
        {
            Codigo = codigo,
            Nombre = nombre,
            ItemType = !string.IsNullOrWhiteSpace(dto.ItemType) ? dto.ItemType.Trim() : "alimento",
            Metadata = dto.Metadata ?? System.Text.Json.JsonDocument.Parse("{}"),
            Activo = dto.Activo,
            CompanyId = companyId,
            PaisId = paisId
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
        // Buscar el item sin filtrar por empresa primero (para poder actualizar items sin empresa)
        var e = await _db.CatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;

        // Si el item ya tiene empresa, validar que pertenezca a la empresa del usuario
        if (_current != null && _current.CompanyId > 0)
        {
            if (e.CompanyId > 0 && e.CompanyId != _current.CompanyId)
            {
                // El item pertenece a otra empresa, no se puede actualizar
                return null;
            }
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                if (e.PaisId > 0 && e.PaisId != _current.PaisId.Value)
                {
                    // El item pertenece a otro país, no se puede actualizar
                    return null;
                }
            }
        }

        e.Nombre = dto.Nombre.Trim();
        e.Activo = dto.Activo;
        e.Metadata = dto.Metadata ?? e.Metadata;
        
        // Actualizar ItemType si se proporciona
        if (!string.IsNullOrWhiteSpace(dto.ItemType))
        {
            e.ItemType = dto.ItemType.Trim();
        }

        // Si el item no tiene empresa o país asignado, asignarlos desde la sesión
        if (_current != null && _current.CompanyId > 0)
        {
            if (e.CompanyId == 0)
            {
                e.CompanyId = _current.CompanyId;
            }
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                if (e.PaisId == 0)
                {
                    e.PaisId = _current.PaisId.Value;
                }
            }
        }

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
        var query = _db.CatalogItems.Where(x => x.Id == id);
        
        // Filtrar por empresa y país del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }
        
        var e = await query.FirstOrDefaultAsync(ct);
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

        // Filtrar por empresa y país del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }

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
                ItemType = x.ItemType,
                Metadata = x.Metadata,
                Activo   = x.Activo
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<List<CatalogItemDto>> GetByTypeAsync(string? typeItem, string? search, CancellationToken ct = default)
    {
        var query = _db.CatalogItems.AsNoTracking().Where(x => x.Activo);

        // Filtrar por empresa y país del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
            {
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
            }
        }

        // Filtrar por búsqueda (código o nombre) - esto sí se puede hacer en la query
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Nombre, $"%{search}%") ||
                EF.Functions.ILike(x.Codigo, $"%{search}%"));
        }

        // Obtener todos los items activos (con filtro de búsqueda si aplica)
        // Filtrar por tipo de item en la query (ahora que ItemType es una columna)
        if (!string.IsNullOrWhiteSpace(typeItem))
        {
            query = query.Where(x => x.ItemType == typeItem);
        }

        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Codigo = x.Codigo,
                Nombre = x.Nombre,
                ItemType = x.ItemType,
                Metadata = x.Metadata,
                Activo = x.Activo
            })
            .ToListAsync(ct);

        return items;
    }

}
