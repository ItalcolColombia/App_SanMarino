using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Seed de datos (sin cambios de schema): le da a <b>Santa Reyes</b> su <b>perfil de atención</b>
    /// de tickets, para que pueda abrir casos y escalarlos a desarrollo.
    ///
    /// <para>
    /// <b>El síntoma.</b> «Santa Reyes no deja crear el ticket para asignarlo a desarrollo». No había
    /// error en pantalla: el desplegable de <i>Tipo</i> del formulario de «Nuevo caso» salía vacío y,
    /// como <c>tipo</c> y <c>asignadoGuid</c> son <c>Validators.required</c>, el botón de guardar
    /// nunca se habilitaba.
    /// </para>
    ///
    /// <para>
    /// <b>La causa, medida.</b> No es permiso, ni menú, ni rol: los tres ya estaban
    /// (<c>tickets.crear</c>/<c>gestionar</c>/<c>admin</c> habilitados en <c>company_permissions</c> de
    /// la empresa y asignados a sus dos roles; <c>tickets</c> + <c>tickets.mis</c> en
    /// <c>company_menus</c> y <c>role_menus</c>). Lo que falta es que la empresa <b>no tiene una sola
    /// fila</b> en <c>ticket_resolutor_rol</c> ni en <c>ticket_resolutores</c> — las 14 + 11 filas
    /// existentes son de Sanmarino, Demo, Ecuador y Panamá. Y
    /// <c>TicketPerfilService.GetTiposPermitidosAsync</c> <b>descarta todo tipo cuyo listado de
    /// asignables venga vacío</b> (<c>if (asignables.Count > 0)</c>), así que devolvía <c>200 []</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Qué siembra (6 filas).</b> El rol global <c>Admin</c> —el del equipo de desarrollo, el mismo
    /// que ya es resolutor de <c>DESARROLLO</c> en las otras cuatro empresas— atiende
    /// <c>DESARROLLO</c> y <c>REQUERIMIENTO</c>; el rol propio <c>Santa Reyes Implementador</c>
    /// atiende los cuatro tipos. Resultado: los 4 tipos quedan disponibles, y Desarrollo/Requerimiento
    /// ofrecen dos destinos (el equipo global o el implementador de la empresa).
    /// </para>
    ///
    /// <para>
    /// <b>La raíz queda tapada aparte:</b> <c>CompanyService.CreateAsync</c> ahora siembra el resolutor
    /// global al crear una empresa (<c>SembrarResolutorGlobalTicketsAsync</c>, con la decisión pura en
    /// <c>TicketPerfilAtencionSiembraCalculos</c> y sus tests). Esta migración es el arrastre de la
    /// empresa que nació antes de ese arreglo; no hay backfill para las otras cuatro porque ya la
    /// tienen.
    /// </para>
    ///
    /// <para>
    /// <b>La idempotencia va por <c>NOT EXISTS</c>, no por el índice único.</b>
    /// <c>ux_ticket_resolutor_rol_role_tipo_pais_company</c> incluye <c>pais_id</c>, y en Postgres dos
    /// NULL <b>no chocan</b>: sin el <c>NOT EXISTS</c> (con <c>pais_id IS NULL</c> explícito) una
    /// segunda corrida duplicaría las 6 filas sin dar un solo error. Y como el service exige
    /// <c>r.Activo</c>, una fila apagada equivale a no tenerla: además del INSERT va el UPDATE que la
    /// reenciende.
    /// </para>
    ///
    /// <para>
    /// Todo se localiza por <c>companies.name</c>/<c>identifier</c> y <c>roles.name</c>, nunca por id:
    /// los ids difieren entre local y prod.
    /// </para>
    /// </summary>
    public partial class SeedPerfilAtencionTicketsSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UP_SQL);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DOWN_SQL);

        /// <summary>
        /// Empresa destino y las 6 filas (rol, tipo) del perfil. `pais_id` va NULL = global, igual que
        /// las 5 filas del rol `Admin` que ya existen, y es lo que espera el filtro del service
        /// (`r.PaisId == null || r.PaisId == paisId`).
        /// </summary>
        private const string PLAN_SQL = @"
destino AS (
    SELECT c.id AS company_id
      FROM public.companies c
     WHERE c.name = 'Santa Reyes' OR c.identifier = '901000001-1'
     ORDER BY c.id
     LIMIT 1
),
plan(rol, tipo) AS (
    VALUES ('Admin'::text,                     'DESARROLLO'::text),
           ('Admin'::text,                     'REQUERIMIENTO'::text),
           ('Santa Reyes Implementador'::text, 'SOPORTE'::text),
           ('Santa Reyes Implementador'::text, 'DUDAS'::text),
           ('Santa Reyes Implementador'::text, 'DESARROLLO'::text),
           ('Santa Reyes Implementador'::text, 'REQUERIMIENTO'::text)
)";

        private const string UP_SQL = @"
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) Las filas que faltan. `roles.name` se compara EXACTO: en la base conviven
--    'Admin Panama', 'Admin Demo', 'Ecuador Administrador' y 'Santa Reyes Administrador',
--    que son administradores DE SU EMPRESA y no el equipo de desarrollo.
-- ─────────────────────────────────────────────────────────────────────────────
WITH " + PLAN_SQL + @"
INSERT INTO public.ticket_resolutor_rol (role_id, tipo, pais_id, company_id, activo, created_at)
SELECT r.id, p.tipo, NULL, d.company_id, true, now()
  FROM plan p
  JOIN public.roles r ON r.name = p.rol
  CROSS JOIN destino d
 WHERE NOT EXISTS (SELECT 1 FROM public.ticket_resolutor_rol x
                    WHERE x.role_id    = r.id
                      AND x.tipo       = p.tipo
                      AND x.pais_id IS NULL
                      AND x.company_id = d.company_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Fila apagada = fila ausente: `GetAsignablesInternalAsync` filtra por `r.Activo`.
-- ─────────────────────────────────────────────────────────────────────────────
WITH " + PLAN_SQL + @"
UPDATE public.ticket_resolutor_rol trr
   SET activo = true, updated_at = now()
  FROM plan p
  JOIN public.roles r ON r.name = p.rol
  CROSS JOIN destino d
 WHERE trr.role_id    = r.id
   AND trr.tipo       = p.tipo
   AND trr.pais_id   IS NULL
   AND trr.company_id = d.company_id
   AND trr.activo IS DISTINCT FROM true;
";

        // Revertir = devolver a Santa Reyes a no poder crear tickets. Se borran EXACTAMENTE las 6
        // filas que el Up agrega, y sólo las de esta empresa: las 14 de las otras cuatro no se tocan.
        private const string DOWN_SQL = @"
WITH " + PLAN_SQL + @"
DELETE FROM public.ticket_resolutor_rol trr
 USING plan p
  JOIN public.roles r ON r.name = p.rol
  CROSS JOIN destino d
 WHERE trr.role_id    = r.id
   AND trr.tipo       = p.tipo
   AND trr.pais_id   IS NULL
   AND trr.company_id = d.company_id;
";
    }
}
