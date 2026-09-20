using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Separa ABRIR de ATENDER en el módulo de tickets (19-sep-2026, plan
    /// <c>fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md</c>):
    ///
    /// <para>
    /// <b><c>roles.ticket_nivel_creacion</c></b> (NULL | NORMAL | IMPLEMENTADOR): qué tipos puede ABRIR
    /// quien tenga el rol. NULL = el rol no lo define ⇒ NORMAL, o sea el comportamiento anterior.
    /// Hasta ahora eso solo se podía habilitar persona por persona (y lo único que lo daba «por rol» era
    /// el permiso <c>tickets.gestionar</c>, que además deja gestionar la bandeja ajena).
    /// </para>
    ///
    /// <para>
    /// <b><c>alcance</c></b> en <c>ticket_resolutores</c> y <c>ticket_resolutor_rol</c>
    /// (EMPRESA | GLOBAL, default EMPRESA): a qué tickets aplica una fila de resolutor. EMPRESA = solo
    /// los de su <c>company_id</c>; GLOBAL = los de todas las empresas, también las que se creen. Antes
    /// «global» no existía como concepto: la etiqueta «Global» de la pantalla era <c>pais_id NULL</c>
    /// (todos los países DE ESA empresa) y el global real se lograba repitiendo la fila del rol
    /// <c>Admin</c> en cada empresa. El default EMPRESA deja todo lo existente exactamente como estaba.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b> (<c>ADD COLUMN IF NOT EXISTS</c> + <c>CHECK</c> creados solo si faltan) y sin
    /// tocar datos: la corrección de los que quedaron en la empresa equivocada va en
    /// <c>CorregirEmpresaYAlcanceConfiguracionTickets</c>.
    /// </para>
    /// </summary>
    public partial class AddAlcanceYNivelCreacionTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.ticket_resolutores   ADD COLUMN IF NOT EXISTS alcance varchar(10) NOT NULL DEFAULT 'EMPRESA';
ALTER TABLE public.ticket_resolutor_rol ADD COLUMN IF NOT EXISTS alcance varchar(10) NOT NULL DEFAULT 'EMPRESA';
ALTER TABLE public.roles                ADD COLUMN IF NOT EXISTS ticket_nivel_creacion varchar(20) NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ticket_resolutores_alcance') THEN
        ALTER TABLE public.ticket_resolutores
            ADD CONSTRAINT ck_ticket_resolutores_alcance CHECK (alcance IN ('EMPRESA','GLOBAL'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ticket_resolutor_rol_alcance') THEN
        ALTER TABLE public.ticket_resolutor_rol
            ADD CONSTRAINT ck_ticket_resolutor_rol_alcance CHECK (alcance IN ('EMPRESA','GLOBAL'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_roles_ticket_nivel_creacion') THEN
        ALTER TABLE public.roles
            ADD CONSTRAINT ck_roles_ticket_nivel_creacion
            CHECK (ticket_nivel_creacion IS NULL OR ticket_nivel_creacion IN ('NORMAL','IMPLEMENTADOR'));
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.ticket_resolutores   DROP CONSTRAINT IF EXISTS ck_ticket_resolutores_alcance;
ALTER TABLE public.ticket_resolutor_rol DROP CONSTRAINT IF EXISTS ck_ticket_resolutor_rol_alcance;
ALTER TABLE public.roles                DROP CONSTRAINT IF EXISTS ck_roles_ticket_nivel_creacion;
ALTER TABLE public.ticket_resolutores   DROP COLUMN IF EXISTS alcance;
ALTER TABLE public.ticket_resolutor_rol DROP COLUMN IF EXISTS alcance;
ALTER TABLE public.roles                DROP COLUMN IF EXISTS ticket_nivel_creacion;
");
        }
    }
}
