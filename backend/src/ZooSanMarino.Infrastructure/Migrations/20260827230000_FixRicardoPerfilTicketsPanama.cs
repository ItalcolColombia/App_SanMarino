using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrige un supuesto equivocado de <c>20260827214243_SeedEnrutamientoTicketsPorEmpresa</c>:
    /// esa migración asumió que Ricardo (rol "Admin Panama") ya era IMPLEMENTADOR y ya podía
    /// gestionar tickets porque su rol tiene <c>tickets.admin</c> — sin verificar que
    /// <c>company_permissions</c> tiene <c>tickets.admin</c> **apagado** para ItalcolPanama
    /// (`is_enabled = false`, medido en código antes de este fix). Con el permiso fail-closed a
    /// nivel empresa, Ricardo no tenía en la práctica ni <c>tickets.admin</c> ni
    /// <c>tickets.gestionar</c>: `GetTiposPermitidosAsync` lo dejaba en nivel NORMAL (solo
    /// SOPORTE/DUDAS, sin REQUERIMIENTO) y `TicketService.PuedeGestionar()` le negaba hasta
    /// gestionar sus propios tickets asignados. Verificado en vivo contra el backend local
    /// (sesión real de Ricardo): `GET /api/ticket-perfiles/tipos-permitidos` devolvía `[]`.
    ///
    /// <para>
    /// No se toca <c>company_permissions.tickets.admin</c> de Panamá (habilitarlo daría
    /// administración global de TODOS los países, no solo permiso de gestionar sus propios
    /// tickets) — se usa el mismo mecanismo puntual que ya se aplicó a Verenice (Sanmarino) y
    /// Lady Malave (Ecuador) en la migración anterior: <c>tickets.gestionar</c> al rol +
    /// <c>ticket_perfil_usuario</c> IMPLEMENTADOR para Ricardo.
    /// </para>
    ///
    /// <para>
    /// Alcanza también a los otros 3 usuarios de "Admin Panama" (admin.panama, Abdiel Tejada,
    /// Edwards Fernandez) — mismo criterio ya aceptado para "Implementador Sanmarino Colombia" y
    /// "Ecuador Administrador": son roles admin-tier de confianza, no usuarios nuevos.
    /// </para>
    ///
    /// Migración DATA-ONLY: Designer clonado del de
    /// <c>20260827214243_SeedEnrutamientoTicketsPorEmpresa</c> (diff normalizado = 0 líneas fuera
    /// del nombre/timestamp), ModelSnapshot intacto. Idempotente, localizada por nombre/email.
    /// </summary>
    public partial class FixRicardoPerfilTicketsPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(UP_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DOWN_SQL);
        }

        private const string UP_SQL = @"
DO $mig$
DECLARE
    v_company_panama int;
    v_role_admin_panama int;
    v_user_ricardo uuid;
    v_perm_tickets_gestionar int;
BEGIN
    SELECT id INTO v_company_panama FROM public.companies WHERE name = 'ItalcolPanama';
    SELECT id INTO v_role_admin_panama FROM public.roles WHERE name = 'Admin Panama';

    SELECT u.id INTO v_user_ricardo
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ricardodelarosa@italcol.com';

    SELECT id INTO v_perm_tickets_gestionar FROM public.permissions WHERE key = 'tickets.gestionar';

    IF v_company_panama IS NULL OR v_role_admin_panama IS NULL OR v_user_ricardo IS NULL
       OR v_perm_tickets_gestionar IS NULL THEN
        RAISE EXCEPTION 'FixRicardoPerfilTicketsPanama: no se resolvio algun id base — revisar nombres/emails contra este entorno.';
    END IF;

    INSERT INTO public.role_permissions (role_id, permission_id)
    SELECT v_role_admin_panama, v_perm_tickets_gestionar
    WHERE NOT EXISTS (
        SELECT 1 FROM public.role_permissions rp
        WHERE rp.role_id = v_role_admin_panama AND rp.permission_id = v_perm_tickets_gestionar);

    INSERT INTO public.ticket_perfil_usuario (user_id, company_id, nivel, activo)
    SELECT v_user_ricardo, v_company_panama, 'IMPLEMENTADOR', true
    WHERE NOT EXISTS (
        SELECT 1 FROM public.ticket_perfil_usuario tpu
        WHERE tpu.user_id = v_user_ricardo AND tpu.company_id = v_company_panama);
END
$mig$;
";

        private const string DOWN_SQL = @"
DO $mig$
DECLARE
    v_company_panama int;
    v_role_admin_panama int;
    v_user_ricardo uuid;
    v_perm_tickets_gestionar int;
BEGIN
    SELECT id INTO v_company_panama FROM public.companies WHERE name = 'ItalcolPanama';
    SELECT id INTO v_role_admin_panama FROM public.roles WHERE name = 'Admin Panama';

    SELECT u.id INTO v_user_ricardo
    FROM public.users u JOIN public.user_logins ul ON ul.user_id = u.id JOIN public.logins l ON l.id = ul.login_id
    WHERE l.email = 'ricardodelarosa@italcol.com';

    SELECT id INTO v_perm_tickets_gestionar FROM public.permissions WHERE key = 'tickets.gestionar';

    IF v_company_panama IS NULL THEN RETURN; END IF;

    IF v_user_ricardo IS NOT NULL THEN
        DELETE FROM public.ticket_perfil_usuario
        WHERE user_id = v_user_ricardo AND company_id = v_company_panama;
    END IF;

    IF v_role_admin_panama IS NOT NULL AND v_perm_tickets_gestionar IS NOT NULL THEN
        DELETE FROM public.role_permissions
        WHERE role_id = v_role_admin_panama AND permission_id = v_perm_tickets_gestionar;
    END IF;
END
$mig$;
";
    }
}
