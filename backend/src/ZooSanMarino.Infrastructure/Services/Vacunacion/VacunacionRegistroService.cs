// Vacunacion/VacunacionRegistroService.cs
// Partial 'ancla': campos, ctor y helpers compartidos.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionRegistroService : IVacunacionRegistroService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public VacunacionRegistroService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    /// <summary>Umbral de días para "incumplido" (rojo): configurado por empresa/país, default 14.</summary>
    private async Task<int> GetUmbralIncumplidoAsync(CancellationToken ct)
    {
        if (!_currentUser.PaisId.HasValue) return 14;
        var cfg = await _ctx.VacunacionConfiguracion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == _currentUser.CompanyId && x.PaisId == _currentUser.PaisId.Value, ct);
        return cfg?.DiasUmbralIncumplido ?? 14;
    }

    private async Task<(DateTime? FechaEncaset, string LoteNombre)> ResolverLoteInfoAsync(VacunacionCronogramaItem item, CancellationToken ct)
    {
        if (item.LotePosturaLevanteId.HasValue)
        {
            var l = await _ctx.LotePosturaLevante.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LotePosturaLevanteId == item.LotePosturaLevanteId, ct);
            return (l?.FechaEncaset, l?.LoteNombre ?? "");
        }
        if (item.LotePosturaProduccionId.HasValue)
        {
            var p = await _ctx.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LotePosturaProduccionId == item.LotePosturaProduccionId, ct);
            return (p?.FechaEncaset, p?.LoteNombre ?? "");
        }
        if (item.LoteAveEngordeId.HasValue)
        {
            var e = await _ctx.LoteAveEngorde.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LoteAveEngordeId == item.LoteAveEngordeId, ct);
            return (e?.FechaEncaset, e?.LoteNombre ?? "");
        }
        return (null, "");
    }
}
