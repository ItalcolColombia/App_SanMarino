using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class CompanyService
{
    /// <summary>
    /// Deja a la empresa nueva con un <b>perfil de atención de tickets</b> utilizable: el rol global
    /// de desarrollo como resolutor de los cuatro tipos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué es necesario.</b> <c>ticket_resolutor_rol</c> lleva <c>company_id</c> a propósito
    /// (índice <c>ux_ticket_resolutor_rol_role_tipo_pais_company</c>): un rol cubre varias empresas
    /// con <b>una fila por empresa</b>, no dejando la fila sin empresa. Como la creación de la
    /// empresa no replicaba esa fila, la empresa nacía sin ningún asignable y
    /// <c>TicketPerfilService.GetTiposPermitidosAsync</c> —que descarta todo tipo sin asignables—
    /// devolvía lista vacía: el formulario de «Nuevo caso» quedaba con el desplegable de Tipo vacío
    /// y, siendo <c>required</c>, no se podía enviar. Le pasó a Santa Reyes desde su alta.
    /// </para>
    /// <para>
    /// <b>Sólo si está vacía</b>, mismo criterio que
    /// <c>ICompanyPermissionService.SembrarCatalogoCompletoSiVaciaAsync</c>: una empresa que ya tiene
    /// su perfil configurado no se toca. Y si no existe el rol global, no se siembra nada
    /// (fail-closed): la decisión de quién es ese rol vive en
    /// <see cref="TicketPerfilAtencionSiembraCalculos"/>, con sus tests.
    /// </para>
    /// <para>
    /// <c>pais_id = NULL</c> ⇒ todos los países de la empresa, que es como están las filas del rol
    /// <c>Admin</c> en las empresas anteriores y lo que espera el filtro del service
    /// (<c>r.PaisId == null || r.PaisId == paisId</c>).
    /// </para>
    /// <para>
    /// Desde el 19-sep-2026 los tipos que el rol ya atiende con alcance <b>GLOBAL</b> (hoy: Desarrollo)
    /// no se siembran: la fila global ya cubre a la empresa nueva.
    /// </para>
    /// </remarks>
    private async Task SembrarResolutorGlobalTicketsAsync(int companyId)
    {
        var yaTiene = await _ctx.TicketResolutorRoles
            .AsNoTracking()
            .AnyAsync(r => r.CompanyId == companyId);
        if (yaTiene) return;

        var roles = await _ctx.Roles
            .AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        // Lo que ya cubre una fila GLOBAL (atiende todas las empresas, también las nuevas) cuenta como
        // «ya está»: sembrarlo de nuevo por empresa sería una copia redundante de lo mismo.
        var cubiertosPorGlobal = await _ctx.TicketResolutorRoles
            .AsNoTracking()
            .Where(r => r.Activo && r.PaisId == null && r.Alcance == TicketAlcance.Global)
            .Select(r => new { r.RoleId, r.Tipo })
            .ToListAsync();

        var filas = TicketPerfilAtencionSiembraCalculos.FilasFaltantes(
            roles.Select(r => (r.Id, (string?)r.Name)),
            existentes: cubiertosPorGlobal.Select(c => (c.RoleId, c.Tipo)));
        if (filas.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var fila in filas)
        {
            _ctx.TicketResolutorRoles.Add(new TicketResolutorRol
            {
                RoleId    = fila.RoleId,
                Tipo      = fila.Tipo,
                PaisId    = null,
                CompanyId = companyId,
                Activo    = true,
                CreatedAt = now,
            });
        }

        await _ctx.SaveChangesAsync();
    }
}
