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

    public async Task<IReadOnlyList<MigracionMasiva>> GetHistorialAsync(int companyId, string? tipo, CancellationToken ct = default)
    {
        var q = _ctx.MigracionMasiva.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(tipo))
            q = q.Where(x => x.Tipo == tipo);

        return await q.OrderByDescending(x => x.FechaProceso)
                      .Take(200)
                      .ToListAsync(ct);
    }
}
