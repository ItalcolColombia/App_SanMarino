// src/ZooSanMarino.Infrastructure/Services/Migracion/MigracionRepository.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MigracionRepository : IMigracionRepository
{
    private readonly ZooSanMarinoContext _ctx;

    public MigracionRepository(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<MigracionMasiva> RegistrarAsync(MigracionMasiva registro, CancellationToken ct = default)
    {
        _ctx.MigracionMasiva.Add(registro);
        await _ctx.SaveChangesAsync(ct);
        return registro;
    }

    public async Task<(IReadOnlyList<MigracionMasiva> Items, int Total)> GetHistorialAsync(
        int companyId, string? tipo, int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default)
    {
        var q = _ctx.MigracionMasiva.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(tipo))
            q = q.Where(x => x.Tipo == tipo);
        if (!incluirValidaciones)
            q = q.Where(x => !x.FueDryRun);

        var total = await q.CountAsync(ct);

        var pageClamped = page < 1 ? 1 : page;
        var pageSizeClamped = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        var items = await q.OrderByDescending(x => x.FechaProceso)
            .Skip((pageClamped - 1) * pageSizeClamped)
            .Take(pageSizeClamped)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<MigracionMasiva?> GetPorIdAsync(int id, int companyId, CancellationToken ct = default) =>
        await _ctx.MigracionMasiva.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.DeletedAt == null, ct);
}
