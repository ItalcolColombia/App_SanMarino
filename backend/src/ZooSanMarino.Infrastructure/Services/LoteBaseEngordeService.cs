// src/ZooSanMarino.Infrastructure/Services/LoteBaseEngordeService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.LoteBaseEngorde;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// CRUD del catálogo lote_base_engorde. Empresa efectiva por nombre activo (mismo criterio
/// que LoteAveEngordeService); nombre único por empresa entre vivos; delete bloqueado si
/// hay lotes de engorde vivos amarrados.
/// </summary>
public class LoteBaseEngordeService : ILoteBaseEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteBaseEngordeService(ZooSanMarinoContext ctx, ICurrentUser current, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    public async Task<IReadOnlyList<LoteBaseEngordeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await _ctx.LoteBaseEngorde
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.DeletedAt == null)
            .OrderBy(b => b.Nombre)
            .Select(b => new LoteBaseEngordeDto(
                b.Id,
                b.Nombre,
                b.Descripcion,
                b.CodigoErp,
                b.LineaGenetica,
                _ctx.LoteAveEngorde.Count(l =>
                    l.LoteBaseEngordeId == b.Id && l.CompanyId == companyId && l.DeletedAt == null),
                b.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<LoteBaseEngordeDto> CreateAsync(CreateLoteBaseEngordeDto dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var nombre = NormalizarNombre(dto.Nombre);

        await EnsureNombreUnicoAsync(companyId, nombre, excluirId: null, ct);

        var ent = new LoteBaseEngorde
        {
            Nombre = nombre,
            Descripcion = Trimmed(dto.Descripcion),
            CodigoErp = Trimmed(dto.CodigoErp),
            LineaGenetica = Trimmed(dto.LineaGenetica),
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.LoteBaseEngorde.Add(ent);
        await _ctx.SaveChangesAsync(ct);

        return new LoteBaseEngordeDto(ent.Id, ent.Nombre, ent.Descripcion, ent.CodigoErp, ent.LineaGenetica, 0, ent.CreatedAt);
    }

    public async Task<LoteBaseEngordeDto?> UpdateAsync(UpdateLoteBaseEngordeDto dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.LoteBaseEngorde
            .SingleOrDefaultAsync(b => b.Id == dto.Id && b.CompanyId == companyId && b.DeletedAt == null, ct);
        if (ent is null) return null;

        var nombre = NormalizarNombre(dto.Nombre);
        await EnsureNombreUnicoAsync(companyId, nombre, excluirId: ent.Id, ct);

        ent.Nombre = nombre;
        ent.Descripcion = Trimmed(dto.Descripcion);
        ent.CodigoErp = Trimmed(dto.CodigoErp);
        ent.LineaGenetica = Trimmed(dto.LineaGenetica);
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        var asignados = await _ctx.LoteAveEngorde
            .CountAsync(l => l.LoteBaseEngordeId == ent.Id && l.CompanyId == companyId && l.DeletedAt == null, ct);
        return new LoteBaseEngordeDto(ent.Id, ent.Nombre, ent.Descripcion, ent.CodigoErp, ent.LineaGenetica, asignados, ent.CreatedAt);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.LoteBaseEngorde
            .SingleOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId && b.DeletedAt == null, ct);
        if (ent is null) return false;

        var amarrados = await _ctx.LoteAveEngorde
            .CountAsync(l => l.LoteBaseEngordeId == id && l.CompanyId == companyId && l.DeletedAt == null, ct);
        if (amarrados > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar: el lote base '{ent.Nombre}' tiene {amarrados} lote(s) de engorde amarrado(s). Desasígnelos primero.");

        ent.DeletedAt = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureNombreUnicoAsync(int companyId, string nombre, int? excluirId, CancellationToken ct)
    {
        var lower = nombre.ToLowerInvariant();
        var duplicado = await _ctx.LoteBaseEngorde
            .AsNoTracking()
            .AnyAsync(b => b.CompanyId == companyId && b.DeletedAt == null
                        && b.Nombre.ToLower() == lower
                        && (!excluirId.HasValue || b.Id != excluirId.Value), ct);
        if (duplicado)
            throw new InvalidOperationException($"Ya existe un lote base con el nombre '{nombre}' en la compañía.");
    }

    private static string NormalizarNombre(string? nombre)
    {
        var n = (nombre ?? string.Empty).Trim();
        if (n.Length == 0)
            throw new InvalidOperationException("El nombre del lote base es requerido.");
        if (n.Length > 80)
            throw new InvalidOperationException("El nombre del lote base no puede superar 80 caracteres.");
        return n;
    }

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
