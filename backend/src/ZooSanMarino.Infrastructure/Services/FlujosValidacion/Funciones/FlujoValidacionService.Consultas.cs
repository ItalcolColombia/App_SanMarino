// Resolución de modo (plan §8.1): decide si un proceso opera SECUENCIAL, LEGACY_UN_PASO o INMEDIATO
// para una empresa dada. Es el punto de integración con la doble validación existente.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class FlujoValidacionService
{
    public async Task<ModoValidacionProceso> ResolverModoAsync(int companyId, string procesoKey, CancellationToken ct = default)
    {
        var proceso = await _ctx.ValidacionProcesos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == procesoKey && p.IsActive, ct);
        if (proceso is null) return ModoValidacionProceso.Inmediato;

        var hayPublicado = await _ctx.ValidacionFlujos.AsNoTracking()
            .AnyAsync(f => f.CompanyId == companyId && f.ProcesoId == proceso.Id
                        && f.Estado == EstadoFlujoValidacion.Publicado, ct);
        if (hayPublicado) return ModoValidacionProceso.Secuencial;

        var requiereLegacy = await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.RequiereValidacionSeguimientoDiario)
            .FirstOrDefaultAsync(ct);
        return requiereLegacy ? ModoValidacionProceso.LegacyUnPaso : ModoValidacionProceso.Inmediato;
    }
}
