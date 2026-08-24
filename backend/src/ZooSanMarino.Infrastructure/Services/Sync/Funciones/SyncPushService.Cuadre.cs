using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Sync;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <inheritdoc />
    public async Task<List<CuadrePendienteDto>> ListarCuadresPendientesAsync(CancellationToken ct = default)
    {
        // Fail-closed, mismo criterio que el push: sin empresa activa, bandeja vacía — nunca la de
        // otra empresa por accidente.
        if (_current.CompanyId <= 0) return new List<CuadrePendienteDto>();

        return await _ctx.SyncOperaciones
            .AsNoTracking()
            .Where(x => x.CompanyId == _current.CompanyId
                     && x.Estado == SyncPushCalculos.Estados.RequiereCuadre
                     && x.CuadreResueltoAt == null)
            .OrderByDescending(x => x.RecibidoAt)
            .Select(x => new CuadrePendienteDto
            {
                Id = x.Id,
                Tipo = x.Tipo,
                EntidadId = x.EntidadId,
                Detalle = x.Detalle,
                DeviceId = x.DeviceId,
                RecibidoAt = x.RecibidoAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ResolverCuadreAsync(long id, CancellationToken ct = default)
    {
        if (_current.CompanyId <= 0) return false;

        var fila = await _ctx.SyncOperaciones.FirstOrDefaultAsync(x =>
            x.Id == id
            && x.CompanyId == _current.CompanyId
            && x.Estado == SyncPushCalculos.Estados.RequiereCuadre
            && x.CuadreResueltoAt == null, ct);

        if (fila is null) return false;

        // SOLO marca visto — ver el doc-comment de la interfaz. No toca RespuestaJson, EntidadId ni
        // ninguna columna del efecto ya aplicado.
        fila.CuadreResueltoAt = DateTime.UtcNow;
        fila.CuadreResueltoPor = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
