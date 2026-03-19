// src/ZooSanMarino.Infrastructure/Services/ItemInventarioEcuadorService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ItemInventarioEcuadorService : IItemInventarioEcuadorService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;

    public ItemInventarioEcuadorService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<ItemInventarioEcuadorDto>> GetAllAsync(string? q = null, string? tipoItem = null, bool? activo = null, CancellationToken ct = default)
    {
        var query = _db.ItemInventarioEcuador.AsNoTracking();

        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Codigo, $"%{term}%") ||
                EF.Functions.ILike(x.Nombre, $"%{term}%") ||
                (x.Descripcion != null && EF.Functions.ILike(x.Descripcion, $"%{term}%")));
        }
        if (!string.IsNullOrWhiteSpace(tipoItem))
            query = query.Where(x => x.TipoItem == tipoItem.Trim());
        if (activo.HasValue)
            query = query.Where(x => x.Activo == activo.Value);

        var list = await query
            .OrderBy(x => x.TipoItem)
            .ThenBy(x => x.Nombre)
            .Select(x => new ItemInventarioEcuadorDto(
                x.Id, x.Codigo, x.Nombre, x.TipoItem, x.Unidad, x.Descripcion, x.Activo,
                x.Grupo, x.TipoInventarioCodigo, x.DescripcionTipoInventario, x.Referencia, x.DescripcionItem, x.Concepto,
                x.CompanyId, x.PaisId, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
        return list;
    }

    public async Task<ItemInventarioEcuadorDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var query = _db.ItemInventarioEcuador.AsNoTracking().Where(x => x.Id == id);
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
        }
        var e = await query.FirstOrDefaultAsync(ct);
        return e == null ? null : new ItemInventarioEcuadorDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<ItemInventarioEcuadorDto> CreateAsync(ItemInventarioEcuadorCreateRequest req, CancellationToken ct = default)
    {
        if (_current == null || _current.CompanyId <= 0)
            throw new InvalidOperationException("Se requiere empresa activa en la sesión.");
        var companyId = _current.CompanyId;
        var paisId = _current.PaisId ?? 0;
        if (paisId <= 0)
            throw new InvalidOperationException("Se requiere país activo en la sesión.");

        var codigo = req.Codigo.Trim();
        var exists = await _db.ItemInventarioEcuador.AnyAsync(x => x.CompanyId == companyId && x.PaisId == paisId && x.Codigo == codigo, ct);
        if (exists)
            throw new InvalidOperationException("Ya existe un ítem con el mismo código para esta empresa y país.");

        var e = new ItemInventarioEcuador
        {
            Codigo = codigo,
            Nombre = req.Nombre.Trim(),
            TipoItem = string.IsNullOrWhiteSpace(req.TipoItem) ? "alimento" : req.TipoItem.Trim(),
            Unidad = string.IsNullOrWhiteSpace(req.Unidad) ? "kg" : req.Unidad.Trim(),
            Descripcion = req.Descripcion?.Trim(),
            Activo = req.Activo,
            Grupo = req.Grupo?.Trim(),
            TipoInventarioCodigo = req.TipoInventarioCodigo?.Trim(),
            DescripcionTipoInventario = req.DescripcionTipoInventario?.Trim(),
            Referencia = req.Referencia?.Trim(),
            DescripcionItem = req.DescripcionItem?.Trim(),
            Concepto = req.Concepto?.Trim(),
            CompanyId = companyId,
            PaisId = paisId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.ItemInventarioEcuador.Add(e);
        await _db.SaveChangesAsync(ct);
        return new ItemInventarioEcuadorDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<ItemInventarioEcuadorDto?> UpdateAsync(int id, ItemInventarioEcuadorUpdateRequest req, CancellationToken ct = default)
    {
        var query = _db.ItemInventarioEcuador.Where(x => x.Id == id);
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
        }
        var e = await query.FirstOrDefaultAsync(ct);
        if (e == null) return null;

        e.Nombre = req.Nombre.Trim();
        e.TipoItem = string.IsNullOrWhiteSpace(req.TipoItem) ? "alimento" : req.TipoItem.Trim();
        e.Unidad = string.IsNullOrWhiteSpace(req.Unidad) ? "kg" : req.Unidad.Trim();
        e.Descripcion = req.Descripcion?.Trim();
        e.Activo = req.Activo;
        e.Grupo = req.Grupo?.Trim();
        e.TipoInventarioCodigo = req.TipoInventarioCodigo?.Trim();
        e.DescripcionTipoInventario = req.DescripcionTipoInventario?.Trim();
        e.Referencia = req.Referencia?.Trim();
        e.DescripcionItem = req.DescripcionItem?.Trim();
        e.Concepto = req.Concepto?.Trim();
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new ItemInventarioEcuadorDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default)
    {
        var query = _db.ItemInventarioEcuador.Where(x => x.Id == id);
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
            if (_current.PaisId.HasValue && _current.PaisId.Value > 0)
                query = query.Where(x => x.PaisId == _current.PaisId.Value);
        }
        var e = await query.FirstOrDefaultAsync(ct);
        if (e == null) return false;

        if (hard)
            _db.ItemInventarioEcuador.Remove(e);
        else
        {
            e.Activo = false;
            e.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ItemInventarioEcuadorCargaMasivaResult> CargaMasivaAsync(IReadOnlyList<ItemInventarioEcuadorCargaMasivaRow> filas, CancellationToken ct = default)
    {
        if (_current == null || _current.CompanyId <= 0)
            throw new InvalidOperationException("Se requiere empresa activa en la sesión.");
        var companyId = _current.CompanyId;
        var paisId = _current.PaisId ?? 0;
        if (paisId <= 0)
            throw new InvalidOperationException("Se requiere país activo en la sesión.");

        var creados = 0;
        var actualizados = 0;
        var errores = 0;
        var mensajes = new List<string>();

        foreach (var (fila, idx) in filas.Select((f, i) => (f, i + 1)))
        {
            try
            {
                var codigo = !string.IsNullOrWhiteSpace(fila.Referencia) ? fila.Referencia!.Trim() : $"ITEM-{idx}";
                var nombre = fila.DescripcionItem ?? fila.Concepto ?? codigo;
                var tipoItem = string.IsNullOrWhiteSpace(fila.TipoItem) ? "alimento" : fila.TipoItem.Trim();
                var unidad = string.IsNullOrWhiteSpace(fila.Unidad) ? "kg" : fila.Unidad.Trim();

                var existente = await _db.ItemInventarioEcuador
                    .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.PaisId == paisId && (x.Codigo == codigo || x.Referencia == codigo), ct);

                if (existente != null)
                {
                    existente.Nombre = nombre;
                    existente.TipoItem = tipoItem;
                    existente.Unidad = unidad;
                    existente.Grupo = fila.Grupo?.Trim();
                    existente.TipoInventarioCodigo = fila.TipoInventarioCodigo?.Trim();
                    existente.DescripcionTipoInventario = fila.DescripcionTipoInventario?.Trim();
                    existente.Referencia = fila.Referencia?.Trim();
                    existente.DescripcionItem = fila.DescripcionItem?.Trim();
                    existente.Descripcion = fila.DescripcionItem?.Trim();
                    existente.Concepto = fila.Concepto?.Trim();
                    existente.UpdatedAt = DateTimeOffset.UtcNow;
                    actualizados++;
                }
                else
                {
                    var e = new ItemInventarioEcuador
                    {
                        Codigo = codigo,
                        Nombre = nombre,
                        TipoItem = tipoItem,
                        Unidad = unidad,
                        Descripcion = fila.DescripcionItem?.Trim(),
                        Activo = true,
                        Grupo = fila.Grupo?.Trim(),
                        TipoInventarioCodigo = fila.TipoInventarioCodigo?.Trim(),
                        DescripcionTipoInventario = fila.DescripcionTipoInventario?.Trim(),
                        Referencia = fila.Referencia?.Trim(),
                        DescripcionItem = fila.DescripcionItem?.Trim(),
                        Concepto = fila.Concepto?.Trim(),
                        CompanyId = companyId,
                        PaisId = paisId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.ItemInventarioEcuador.Add(e);
                    creados++;
                }
            }
            catch (Exception ex)
            {
                errores++;
                mensajes.Add($"Fila {idx}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ItemInventarioEcuadorCargaMasivaResult(filas.Count, creados, actualizados, errores, mensajes);
    }
}
