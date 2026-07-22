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
/// hay lotes de engorde vivos amarrados. La visibilidad al crear lote se parametriza por
/// granja vía lote_base_engorde_granja (M:N).
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

        var bases = await _ctx.LoteBaseEngorde
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.DeletedAt == null)
            .OrderBy(b => b.Nombre)
            .ToListAsync(ct);

        if (bases.Count == 0) return Array.Empty<LoteBaseEngordeDto>();

        var ids = bases.Select(b => b.Id).ToList();

        // Granjas asignadas por lote base (filtro de visibilidad al crear lote).
        var asignaciones = await _ctx.LoteBaseEngordeGranja
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && ids.Contains(g.LoteBaseEngordeId))
            .Select(g => new { g.LoteBaseEngordeId, g.FarmId })
            .ToListAsync(ct);
        var granjasPorBase = asignaciones
            .GroupBy(x => x.LoteBaseEngordeId)
            .ToDictionary(gr => gr.Key, gr => (IReadOnlyList<int>)gr.Select(x => x.FarmId).ToList());

        // Conteo de lotes de engorde vivos amarrados.
        var conteos = await _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null
                     && l.LoteBaseEngordeId != null && ids.Contains(l.LoteBaseEngordeId.Value))
            .GroupBy(l => l.LoteBaseEngordeId!.Value)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToListAsync(ct);
        var conteoPorBase = conteos.ToDictionary(x => x.Id, x => x.C);

        // Nombre del creador: CreatedByUserId (int) == users.cedula (patrón de TicketService).
        var nombrePorCedula = await ResolverCreadoresAsync(bases.Select(b => b.CreatedByUserId), ct);

        return bases.Select(b => new LoteBaseEngordeDto(
            b.Id, b.Nombre, b.Descripcion, b.CodigoErp, b.LineaGenetica,
            b.FechaActivacion, b.Activo,
            conteoPorBase.TryGetValue(b.Id, out var c) ? c : 0,
            granjasPorBase.TryGetValue(b.Id, out var gs) ? gs : Array.Empty<int>(),
            nombrePorCedula.TryGetValue(b.CreatedByUserId, out var nm) ? nm : null,
            b.CreatedAt)).ToList();
    }

    public async Task<LoteBaseEngordeDto> CreateAsync(CreateLoteBaseEngordeDto dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var nombre = NormalizarNombre(dto.Nombre);

        await EnsureNombreUnicoAsync(companyId, nombre, excluirId: null, ct);

        var ent = new LoteBaseEngorde
        {
            Nombre = nombre,
            // Fecha de activación automática (hoy). Ya no controla vigencia por año.
            FechaActivacion = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified),
            Activo = true,
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.LoteBaseEngorde.Add(ent);
        await _ctx.SaveChangesAsync(ct);

        var creador = await ResolverCreadorAsync(ent.CreatedByUserId, ct);
        return ToDto(ent, 0, Array.Empty<int>(), creador);
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
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return ToDto(ent,
            await ContarAsignadosAsync(ent.Id, companyId, ct),
            await GetGranjaIdsAsync(ent.Id, companyId, ct),
            await ResolverCreadorAsync(ent.CreatedByUserId, ct));
    }

    public async Task<LoteBaseEngordeDto?> SetActivoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await _ctx.LoteBaseEngorde
            .SingleOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId && b.DeletedAt == null, ct);
        if (ent is null) return null;

        ent.Activo = activo;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return ToDto(ent,
            await ContarAsignadosAsync(ent.Id, companyId, ct),
            await GetGranjaIdsAsync(ent.Id, companyId, ct),
            await ResolverCreadorAsync(ent.CreatedByUserId, ct));
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

        // Limpia las asignaciones de granja del lote base.
        var asignaciones = await _ctx.LoteBaseEngordeGranja
            .Where(g => g.LoteBaseEngordeId == id && g.CompanyId == companyId)
            .ToListAsync(ct);
        if (asignaciones.Count > 0)
            _ctx.LoteBaseEngordeGranja.RemoveRange(asignaciones);

        ent.DeletedAt = DateTime.UtcNow;
        ent.UpdatedByUserId = _current.UserId;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ── Asignación de granjas (visibilidad al crear lote) ────────────────────

    public async Task<IReadOnlyList<LoteBaseEngordeGranjaDto>> GetGranjasAsync(int loteBaseId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        await EnsureLoteBaseAsync(companyId, loteBaseId, ct);

        return await _ctx.LoteBaseEngordeGranja
            .AsNoTracking()
            .Where(g => g.LoteBaseEngordeId == loteBaseId && g.CompanyId == companyId)
            .Join(_ctx.Farms.AsNoTracking(), g => g.FarmId, f => f.Id, (g, f) => new { f.Id, f.Name })
            .OrderBy(x => x.Name)
            .Select(x => new LoteBaseEngordeGranjaDto(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<LoteBaseEngordeGranjaDto?> AssignGranjaAsync(int loteBaseId, int farmId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        await EnsureLoteBaseAsync(companyId, loteBaseId, ct);

        var farm = await _ctx.Farms.AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == farmId && f.CompanyId == companyId && f.Status == "A" && f.DeletedAt == null, ct);
        if (farm is null)
            throw new InvalidOperationException("La granja no existe o no pertenece a la empresa activa.");

        var yaExiste = await _ctx.LoteBaseEngordeGranja
            .AnyAsync(g => g.LoteBaseEngordeId == loteBaseId && g.FarmId == farmId && g.CompanyId == companyId, ct);
        if (!yaExiste)
        {
            _ctx.LoteBaseEngordeGranja.Add(new LoteBaseEngordeGranja
            {
                LoteBaseEngordeId = loteBaseId,
                FarmId = farmId,
                CompanyId = companyId,
                CreatedByUserId = _current.UserId,
                CreatedAt = DateTime.UtcNow
            });
            await _ctx.SaveChangesAsync(ct);
        }
        return new LoteBaseEngordeGranjaDto(farm.Id, farm.Name);
    }

    public async Task<bool> UnassignGranjaAsync(int loteBaseId, int farmId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var row = await _ctx.LoteBaseEngordeGranja
            .SingleOrDefaultAsync(g => g.LoteBaseEngordeId == loteBaseId && g.FarmId == farmId && g.CompanyId == companyId, ct);
        if (row is null) return false;

        _ctx.LoteBaseEngordeGranja.Remove(row);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureLoteBaseAsync(int companyId, int loteBaseId, CancellationToken ct)
    {
        var existe = await _ctx.LoteBaseEngorde
            .AsNoTracking()
            .AnyAsync(b => b.Id == loteBaseId && b.CompanyId == companyId && b.DeletedAt == null, ct);
        if (!existe)
            throw new InvalidOperationException("El lote base no existe o no pertenece a la empresa activa.");
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

    private async Task<int> ContarAsignadosAsync(int loteBaseId, int companyId, CancellationToken ct) =>
        await _ctx.LoteAveEngorde
            .CountAsync(l => l.LoteBaseEngordeId == loteBaseId && l.CompanyId == companyId && l.DeletedAt == null, ct);

    private async Task<IReadOnlyList<int>> GetGranjaIdsAsync(int loteBaseId, int companyId, CancellationToken ct) =>
        await _ctx.LoteBaseEngordeGranja
            .AsNoTracking()
            .Where(g => g.LoteBaseEngordeId == loteBaseId && g.CompanyId == companyId)
            .Select(g => g.FarmId)
            .ToListAsync(ct);

    /// <summary>Resuelve nombres de creadores por cédula (CreatedByUserId int == users.cedula).</summary>
    private async Task<Dictionary<int, string>> ResolverCreadoresAsync(IEnumerable<int> userIds, CancellationToken ct)
    {
        var cedulas = userIds.Select(id => id.ToString()).Distinct().ToList();
        if (cedulas.Count == 0) return new();

        var users = await _ctx.Users.AsNoTracking()
            .Where(u => cedulas.Contains(u.cedula))
            .Select(u => new { u.cedula, u.firstName, u.surName })
            .ToListAsync(ct);

        var map = new Dictionary<int, string>();
        foreach (var u in users)
            if (int.TryParse(u.cedula, out var cid))
            {
                var nombre = $"{u.firstName} {u.surName}".Trim();
                if (!string.IsNullOrWhiteSpace(nombre)) map[cid] = nombre;
            }
        return map;
    }

    private async Task<string?> ResolverCreadorAsync(int userId, CancellationToken ct)
    {
        var map = await ResolverCreadoresAsync(new[] { userId }, ct);
        return map.TryGetValue(userId, out var nm) ? nm : null;
    }

    private static LoteBaseEngordeDto ToDto(LoteBaseEngorde ent, int asignados, IReadOnlyList<int> granjaIds, string? creadorNombre) => new(
        ent.Id, ent.Nombre, ent.Descripcion, ent.CodigoErp, ent.LineaGenetica,
        ent.FechaActivacion, ent.Activo, asignados, granjaIds, creadorNombre, ent.CreatedAt);

    private static string NormalizarNombre(string? nombre)
    {
        var n = (nombre ?? string.Empty).Trim();
        if (n.Length == 0)
            throw new InvalidOperationException("El nombre del lote base es requerido.");
        if (n.Length > 80)
            throw new InvalidOperationException("El nombre del lote base no puede superar 80 caracteres.");
        return n;
    }
}
