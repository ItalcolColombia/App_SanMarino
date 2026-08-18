// src/ZooSanMarino.Infrastructure/Services/SuperAdminLookup.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// El ÚNICO lector de la marca de Super Admin. Todos los servicios que antes consultaban el correo
/// del usuario para compararlo con un literal pasan por acá.
///
/// <para>
/// La decisión vive en <see cref="SuperAdminCalculos"/> (pura y testeada); esto sólo trae el dato.
/// El costo es el mismo de antes —cada sitio ya hacía su propio <c>SELECT</c> del email—, pero ahora
/// hay una sola consulta y una sola regla en todo el backend.
/// </para>
/// </summary>
public static class SuperAdminLookup
{
    /// <summary>
    /// ¿El usuario del Guid es Super Admin? <b>Fail-closed</b>: sin Guid o sin fila ⇒ <c>false</c>.
    /// </summary>
    public static async Task<bool> EsSuperAdminAsync(
        ZooSanMarinoContext ctx,
        Guid? userGuid,
        CancellationToken ct = default)
    {
        if (!userGuid.HasValue) return false;

        var marca = await ctx.Users
            .AsNoTracking()
            .Where(u => u.Id == userGuid.Value)
            .Select(u => (bool?)u.IsSuperAdmin)
            .FirstOrDefaultAsync(ct);

        return SuperAdminCalculos.EsSuperAdmin(marca);
    }
}
