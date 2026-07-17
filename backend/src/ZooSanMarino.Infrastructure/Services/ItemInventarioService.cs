// src/ZooSanMarino.Infrastructure/Services/ItemInventarioService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ItemInventarioService : IItemInventarioService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;
    private readonly ICompanyResolver? _companyResolver;

    public ItemInventarioService(ZooSanMarinoContext db, ICurrentUser? current = null, ICompanyResolver? companyResolver = null)
    {
        _db = db;
        _current = current;
        _companyResolver = companyResolver;
    }

    /// <summary>
    /// Empresa efectiva de la sesión: resuelve el nombre del header X-Active-Company (ICompanyResolver)
    /// y cae al CompanyId del token. Mismo criterio que InventarioGestionService y el módulo de engorde,
    /// para que el catálogo de ítems y las granjas resuelvan SIEMPRE la misma empresa (evita ver granjas de
    /// una empresa e ítems de otra). Devuelve null si no hay empresa resoluble.
    /// </summary>
    private async Task<int?> GetEffectiveCompanyIdAsync()
    {
        if (_current == null) return null;
        int? byName = null;
        if (_companyResolver != null && !string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
            byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
        return InventarioCatalogoScopeCalculos.EmpresaEfectiva(byName, _current.CompanyId);
    }

    /// <summary>País efectivo: header X-Active-Pais (_current.PaisId) o, si falta, el país de la empresa (company_pais).</summary>
    private async Task<int?> GetEffectivePaisIdAsync(int companyId, CancellationToken ct = default)
    {
        if (_current?.PaisId is > 0) return _current.PaisId;
        var paisId = await _db.CompanyPaises.AsNoTracking()
            .Where(cp => cp.CompanyId == companyId)
            .Select(cp => (int?)cp.PaisId)
            .FirstOrDefaultAsync(ct);
        return paisId is > 0 ? paisId : null;
    }

    public async Task<List<ItemInventarioDto>> GetAllAsync(string? q = null, string? tipoItem = null, bool? activo = null, CancellationToken ct = default)
    {
        var query = _db.ItemInventario.AsNoTracking();

        var companyId = await GetEffectiveCompanyIdAsync();
        switch (InventarioCatalogoScopeCalculos.Decidir(_current != null, companyId))
        {
            // Fail-closed: hay sesión pero no se resuelve empresa → NO exponer todo el catálogo
            // (esto era la fuga: en Panamá caía a "sin filtro" y devolvía los ítems de Ecuador).
            case InventarioCatalogoScopeCalculos.ScopeDecision.FailClosed:
                return new List<ItemInventarioDto>();
            case InventarioCatalogoScopeCalculos.ScopeDecision.FilterByCompany:
                query = query.Where(x => x.CompanyId == companyId!.Value);
                if (_current!.PaisId is > 0)
                    query = query.Where(x => x.PaisId == _current.PaisId!.Value);
                break;
            // NoSession (uso interno/no-HTTP): sin filtro de empresa.
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
            .Select(x => new ItemInventarioDto(
                x.Id, x.Codigo, x.Nombre, x.TipoItem, x.Unidad, x.Descripcion, x.Activo,
                x.Grupo, x.TipoInventarioCodigo, x.DescripcionTipoInventario, x.Referencia, x.DescripcionItem, x.Concepto,
                x.CompanyId, x.PaisId, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
        return list;
    }

    public async Task<ItemInventarioDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var query = _db.ItemInventario.AsNoTracking().Where(x => x.Id == id);
        if (_current != null)
        {
            var companyId = await GetEffectiveCompanyIdAsync();
            if (companyId is null or <= 0) return null;
            query = query.Where(x => x.CompanyId == companyId.Value);
            if (_current.PaisId is > 0)
                query = query.Where(x => x.PaisId == _current.PaisId!.Value);
        }
        var e = await query.FirstOrDefaultAsync(ct);
        return e == null ? null : new ItemInventarioDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<ItemInventarioDto> CreateAsync(ItemInventarioCreateRequest req, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        if (companyId is null or <= 0)
            throw new InvalidOperationException("Se requiere empresa activa en la sesión.");
        var cid = companyId.Value;
        var paisId = await GetEffectivePaisIdAsync(cid, ct) ?? 0;
        if (paisId <= 0)
            throw new InvalidOperationException("Se requiere país activo en la sesión.");

        var codigo = req.Codigo.Trim();
        var exists = await _db.ItemInventario.AnyAsync(x => x.CompanyId == cid && x.PaisId == paisId && x.Codigo == codigo, ct);
        if (exists)
            throw new InvalidOperationException("Ya existe un ítem con el mismo código para esta empresa y país.");

        var e = new ItemInventario
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
            CompanyId = cid,
            PaisId = paisId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.ItemInventario.Add(e);
        await _db.SaveChangesAsync(ct);
        return new ItemInventarioDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<ItemInventarioDto?> UpdateAsync(int id, ItemInventarioUpdateRequest req, CancellationToken ct = default)
    {
        var query = _db.ItemInventario.Where(x => x.Id == id);
        if (_current != null)
        {
            var companyId = await GetEffectiveCompanyIdAsync();
            if (companyId is null or <= 0) return null;
            query = query.Where(x => x.CompanyId == companyId.Value);
            if (_current.PaisId is > 0)
                query = query.Where(x => x.PaisId == _current.PaisId!.Value);
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
        return new ItemInventarioDto(
            e.Id, e.Codigo, e.Nombre, e.TipoItem, e.Unidad, e.Descripcion, e.Activo,
            e.Grupo, e.TipoInventarioCodigo, e.DescripcionTipoInventario, e.Referencia, e.DescripcionItem, e.Concepto,
            e.CompanyId, e.PaisId, e.CreatedAt, e.UpdatedAt);
    }

    public async Task<bool> DeleteAsync(int id, bool hard = false, CancellationToken ct = default)
    {
        var query = _db.ItemInventario.Where(x => x.Id == id);
        if (_current != null)
        {
            var companyId = await GetEffectiveCompanyIdAsync();
            if (companyId is null or <= 0) return false;
            query = query.Where(x => x.CompanyId == companyId.Value);
            if (_current.PaisId is > 0)
                query = query.Where(x => x.PaisId == _current.PaisId!.Value);
        }
        var e = await query.FirstOrDefaultAsync(ct);
        if (e == null) return false;

        if (hard)
            _db.ItemInventario.Remove(e);
        else
        {
            e.Activo = false;
            e.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ItemInventarioCargaMasivaResult> CargaMasivaAsync(IReadOnlyList<ItemInventarioCargaMasivaRow> filas, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        if (companyId is null or <= 0)
            throw new InvalidOperationException("Se requiere empresa activa en la sesión.");
        var cid = companyId.Value;
        var paisId = await GetEffectivePaisIdAsync(cid, ct) ?? 0;
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

                var existente = await _db.ItemInventario
                    .FirstOrDefaultAsync(x => x.CompanyId == cid && x.PaisId == paisId && (x.Codigo == codigo || x.Referencia == codigo), ct);

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
                    var e = new ItemInventario
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
                        CompanyId = cid,
                        PaisId = paisId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.ItemInventario.Add(e);
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
        return new ItemInventarioCargaMasivaResult(filas.Count, creados, actualizados, errores, mensajes);
    }
}
