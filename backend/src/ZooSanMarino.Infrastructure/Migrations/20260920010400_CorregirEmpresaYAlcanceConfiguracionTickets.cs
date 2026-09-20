using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only) de la configuración de tickets que quedó guardada en la empresa
    /// equivocada, y formalización del alcance GLOBAL. Plan:
    /// <c>fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md</c>.
    ///
    /// <para>
    /// <b>1) Perfiles de apertura en una empresa ajena.</b> <c>TicketPerfilService</c> guardaba en la
    /// empresa activa DEL QUE EDITA, así que el admin global parado en Sanmarino habilitó a un usuario de
    /// Santa Reyes y el perfil quedó con <c>company_id = 1</c>: en su empresa el usuario siguió NORMAL y
    /// el formulario «Nuevo caso» le salía sin un solo tipo. Medido sobre la copia de producción del
    /// 18-sep-2026: <b>5 de 10 perfiles</b> estaban fuera de la empresa del usuario (Lenin Castilla y
    /// Admin Santa Reyes ⇒ 6, usuario 1 y 2 demo ⇒ 4, y una fila ya apagada de Lady Solange).
    /// Se MUDAN a la empresa del usuario cuando tiene una sola y no hay ya una fila ahí (una por usuario,
    /// la más vigente); el resto se APAGA. Nada se borra.
    /// </para>
    ///
    /// <para>
    /// <b>2) Plantilla de atención de un rol en una empresa que no es del rol.</b> Mismo defecto, del otro
    /// lado: al rol <c>Costos</c> (de Panamá, cuyo único permiso de tickets es <c>tickets.crear</c>) le
    /// prendieron DESARROLLO el 18-sep-2026 para que su gente pudiera ABRIR casos de desarrollo, y la fila
    /// quedó en Sanmarino ⇒ sus dos usuarios de Panamá aparecían como <b>resolutores</b> en los tickets de
    /// Sanmarino y seguían sin poder abrir nada. Para cualquier rol que no sea el administrador de la
    /// aplicación, esa fila se apaga y —si era de DESARROLLO/REQUERIMIENTO, o sea la intención de abrir—
    /// el rol recibe <c>ticket_nivel_creacion = 'IMPLEMENTADOR'</c>, que es lo que se quiso hacer
    /// (decisión del usuario, 19-sep-2026). Las filas del rol administrador NO se tocan acá: son
    /// cross-empresa a propósito.
    /// </para>
    ///
    /// <para>
    /// <b>3) El global de hecho pasa a ser GLOBAL de verdad.</b> Una fila (rol administrador, tipo, país)
    /// ACTIVA en TODAS las empresas era la única forma de expresar «atiende todas»: queda UNA con
    /// <c>alcance = 'GLOBAL'</c> —alojada en la empresa del rol— y las copias por empresa se apagan. Sobre
    /// la copia de producción eso es exactamente <c>Admin</c> / DESARROLLO (repetida en las 5 empresas), o
    /// sea el resolutor global de desarrollo. Con la fila GLOBAL, una empresa nueva queda cubierta sin
    /// sembrarle nada.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b> (2.ª corrida = 0 filas afectadas, verificado sobre un clon de la copia real) y
    /// <b>reglas genéricas, nunca por id</b>: si producción difiere de la copia, corrige lo que encuentre y
    /// no inventa nada. Se mide antes y después con
    /// <c>backend/sql/verificar_perfiles_tickets_empresa.sql</c>. Sin reversa: <c>Down</c> no puede saber
    /// qué fila estaba en qué empresa (queda la auditoría de las apagadas).
    /// </para>
    /// </summary>
    public partial class CorregirEmpresaYAlcanceConfiguracionTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1a) El perfil de apertura se MUDA a la empresa del usuario (una sola empresa, sin fila ahí).
            migrationBuilder.Sql(@"
WITH candidatas AS (
    SELECT DISTINCT ON (tp.user_id) tp.id, uc.company_id AS destino
      FROM public.ticket_perfil_usuario tp
      JOIN (SELECT user_id, min(company_id) AS company_id
              FROM public.user_companies GROUP BY user_id HAVING count(*) = 1) uc
        ON uc.user_id = tp.user_id
     WHERE tp.company_id <> uc.company_id
       AND NOT EXISTS (SELECT 1 FROM public.ticket_perfil_usuario o
                        WHERE o.user_id = tp.user_id AND o.company_id = uc.company_id)
     ORDER BY tp.user_id, tp.activo DESC, tp.updated_at DESC NULLS LAST, tp.id DESC
)
UPDATE public.ticket_perfil_usuario tp
   SET company_id = c.destino, updated_at = now()
  FROM candidatas c
 WHERE tp.id = c.id;
");

            // 1b) Lo que quede fuera de la empresa del usuario se APAGA (queda la auditoría).
            migrationBuilder.Sql(@"
UPDATE public.ticket_perfil_usuario tp
   SET activo = false, updated_at = now()
 WHERE tp.activo
   AND NOT EXISTS (SELECT 1 FROM public.user_companies uc
                    WHERE uc.user_id = tp.user_id AND uc.company_id = tp.company_id);
");

            // 2) La plantilla de un rol NO administrador en empresa ajena era intención de ABRIR: el rol
            //    recibe el nivel...
            migrationBuilder.Sql(@"
UPDATE public.roles r
   SET ticket_nivel_creacion = 'IMPLEMENTADOR'
 WHERE r.ticket_nivel_creacion IS NULL
   AND r.id IN (
       SELECT rr.role_id
         FROM public.ticket_resolutor_rol rr
         JOIN public.roles ro ON ro.id = rr.role_id
        WHERE rr.activo AND rr.alcance = 'EMPRESA'
          AND rr.tipo IN ('DESARROLLO','REQUERIMIENTO')
          AND lower(btrim(ro.name)) NOT IN ('admin','administrador')
          AND EXISTS (SELECT 1 FROM public.role_companies rc WHERE rc.role_id = rr.role_id)
          AND NOT EXISTS (SELECT 1 FROM public.role_companies rc
                           WHERE rc.role_id = rr.role_id AND rc.company_id = rr.company_id));
");

            // ... y la fila de resolutor se apaga.
            migrationBuilder.Sql(@"
UPDATE public.ticket_resolutor_rol rr
   SET activo = false, updated_at = now()
 WHERE rr.activo AND rr.alcance = 'EMPRESA'
   AND EXISTS (SELECT 1 FROM public.roles ro
                WHERE ro.id = rr.role_id AND lower(btrim(ro.name)) NOT IN ('admin','administrador'))
   AND EXISTS (SELECT 1 FROM public.role_companies rc WHERE rc.role_id = rr.role_id)
   AND NOT EXISTS (SELECT 1 FROM public.role_companies rc
                    WHERE rc.role_id = rr.role_id AND rc.company_id = rr.company_id);
");

            // 3) La fila del rol administrador presente en TODAS las empresas queda como GLOBAL única.
            migrationBuilder.Sql(@"
WITH candidatos AS (
    SELECT rr.role_id, rr.tipo, rr.pais_id
      FROM public.ticket_resolutor_rol rr
      JOIN public.roles r ON r.id = rr.role_id
     WHERE rr.activo AND rr.alcance = 'EMPRESA'
       AND lower(btrim(r.name)) IN ('admin','administrador')
     GROUP BY rr.role_id, rr.tipo, rr.pais_id
    HAVING count(DISTINCT rr.company_id) = (SELECT count(*) FROM public.companies)
), anfitriones AS (
    SELECT DISTINCT ON (c.role_id, c.tipo, c.pais_id) rr.id
      FROM candidatos c
      JOIN public.ticket_resolutor_rol rr
        ON rr.role_id = c.role_id AND rr.tipo = c.tipo
       AND rr.pais_id IS NOT DISTINCT FROM c.pais_id
       AND rr.activo AND rr.alcance = 'EMPRESA'
     ORDER BY c.role_id, c.tipo, c.pais_id,
              (EXISTS (SELECT 1 FROM public.role_companies rc
                        WHERE rc.role_id = rr.role_id AND rc.company_id = rr.company_id)) DESC,
              rr.company_id
)
UPDATE public.ticket_resolutor_rol rr
   SET alcance = 'GLOBAL', updated_at = now()
 WHERE rr.id IN (SELECT id FROM anfitriones);
");

            // 4) Con la fila GLOBAL vigente, las copias por empresa de la misma (rol, tipo, país) sobran.
            migrationBuilder.Sql(@"
UPDATE public.ticket_resolutor_rol rr
   SET activo = false, updated_at = now()
 WHERE rr.activo AND rr.alcance = 'EMPRESA'
   AND EXISTS (SELECT 1 FROM public.ticket_resolutor_rol g
                WHERE g.role_id = rr.role_id AND g.tipo = rr.tipo
                  AND g.pais_id IS NOT DISTINCT FROM rr.pais_id
                  AND g.activo AND g.alcance = 'GLOBAL');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversa: no se puede saber en qué empresa estaba cada perfil ni qué fila se apagó por
            // esta corrección y cuál ya estaba apagada. Las columnas las quita
            // AddAlcanceYNivelCreacionTickets.
        }
    }
}
