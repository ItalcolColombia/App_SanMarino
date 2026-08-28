// src/ZooSanMarino.Infrastructure/Services/PrimerRegistroPorHoraGate.cs
// Resuelve el flag de empresa `primer_registro_segun_hora_llegada`.
//
// ⚠️ 28-ago-2026: su alcance se REDUJO al corrimiento del DÍA DE PESAJE. El PRIMER DÍA CON REGISTRO
// dejó de gatearse por empresa —lo decide la hora del lote y punto—, porque el formulario ofrece el
// campo "Hora de encasetamiento" a todas las empresas con la leyenda "desde las 13:00 el primer
// registro pasa al día siguiente" y con el gate puesto ItalcolEcuador la llenó 16 veces, todas
// ≥ 13:00, sin que el backend la mirara. Ver EncasetamientoCalculos.HoraEfectiva.
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
