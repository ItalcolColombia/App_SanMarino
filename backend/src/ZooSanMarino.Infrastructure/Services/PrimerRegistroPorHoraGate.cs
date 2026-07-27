// src/ZooSanMarino.Infrastructure/Services/PrimerRegistroPorHoraGate.cs
// Resuelve el flag de empresa que habilita la regla "la hora de llegada decide el primer día con
// registro". Vive acá y no dentro de un service porque lo consultan los cinco puntos de captura
// (formulario diario de reproductora, formulario diario de engorde x2, y las dos cargas masivas) más
// los dos PUT de lote: si cada uno resolviera el flag a su manera, la regla se aplicaría distinto
// según el canal — que es justo el bug que ya tuvimos con la validación de fecha.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

internal static class PrimerRegistroPorHoraGate
{
    /// <summary>
    /// <c>true</c> si la empresa aplica la regla de la hora de llegada.
    /// <para>
    /// <b>Fail-closed hacia el comportamiento previo</b>: empresa inexistente o flag apagado devuelven
    /// <c>false</c>, y con la regla apagada la hora se ignora por completo
    /// (<c>EncasetamientoCalculos.HoraEfectiva</c>), así que el lote se comporta exactamente como antes.
    /// </para>
    /// </summary>
    public static Task<bool> ActivaAsync(ZooSanMarinoContext ctx, int companyId, CancellationToken ct = default) =>
        ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.PrimerRegistroSegunHoraLlegada)
            .FirstOrDefaultAsync(ct);
}
