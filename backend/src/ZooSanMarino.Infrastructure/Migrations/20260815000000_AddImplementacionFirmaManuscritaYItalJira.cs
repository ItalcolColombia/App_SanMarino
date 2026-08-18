using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fases I1 e I3 del plan <c>fase_de_desarrollo/implementacion_italjira_firma_home_plan.md</c>:
    ///
    /// <para><b>I1 — el plan vive en el tablero.</b> <c>implementacion_planes.historia_id</c> y
    /// <c>implementacion_tareas.ticket_tarea_id</c> enlazan el cronograma de entrega con la historia
    /// (épica) y las tareas de ItalJira donde realmente se ejecuta el trabajo. <b>Sin FK a propósito</b>:
    /// los dos tableros tienen ciclos de vida distintos y borrar una historia no debe arrastrar el
    /// plan (ni al revés) — el trabajo hecho es evidencia propia de cada lado.</para>
    ///
    /// <para><b>I3 — la firma pasa de digitada a manuscrita.</b> El participante firma con el dedo en
    /// el celular o con el mouse; se guarda el PNG del trazo más la evidencia de contexto.
    /// <c>contenido_hash</c> es la pieza que convierte la imagen en prueba: es el SHA-256 de lo que
    /// se aceptó (plan + punto + fecha de realización), calculado siempre en el servidor, de modo que
    /// si alguien edita el punto después de firmado el detalle pueda mostrar que el contenido cambió.</para>
    ///
    /// <para>Todo aditivo y NULLABLE: las firmas digitadas existentes siguen válidas y se leen igual
    /// (<c>firma_texto</c> se conserva y sigue siendo el camino accesible), y un plan sin historia
    /// funciona exactamente como hasta hoy.</para>
    ///
    /// Idempotente (<c>IF NOT EXISTS</c>). Escrita a mano.
    /// </summary>
    public partial class AddImplementacionFirmaManuscritaYItalJira : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── I1: vínculo con ItalJira ─────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.implementacion_planes ADD COLUMN IF NOT EXISTS historia_id bigint NULL;
ALTER TABLE public.implementacion_tareas ADD COLUMN IF NOT EXISTS ticket_tarea_id bigint NULL;

CREATE INDEX IF NOT EXISTS ix_implementacion_planes_historia
    ON public.implementacion_planes (historia_id) WHERE historia_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_implementacion_tareas_ticket_tarea
    ON public.implementacion_tareas (ticket_tarea_id) WHERE ticket_tarea_id IS NOT NULL;
");

            // ── I3: firma manuscrita + evidencia ─────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.implementacion_tarea_firmas ADD COLUMN IF NOT EXISTS firma_imagen text NULL;
ALTER TABLE public.implementacion_tarea_firmas ADD COLUMN IF NOT EXISTS firma_tipo character varying(12) NULL;
ALTER TABLE public.implementacion_tarea_firmas ADD COLUMN IF NOT EXISTS contenido_hash character varying(64) NULL;
ALTER TABLE public.implementacion_tarea_firmas ADD COLUMN IF NOT EXISTS firmado_user_agent character varying(400) NULL;
ALTER TABLE public.implementacion_tarea_firmas ADD COLUMN IF NOT EXISTS firmado_ip character varying(45) NULL;
");

            // Las firmas ya registradas fueron digitadas: se rotulan para que la UI no las muestre
            // como "sin tipo". IS DISTINCT FROM para no ensuciar updated_at de filas ya correctas.
            migrationBuilder.Sql(@"
UPDATE public.implementacion_tarea_firmas
   SET firma_tipo = 'digitada'
 WHERE firma_texto IS NOT NULL
   AND firma_tipo IS DISTINCT FROM 'digitada';
");

            // El tipo solo admite los dos valores conocidos (NULL sigue permitido: firmas viejas
            // sin texto, p. ej. las que registraron novedad en vez de firmar).
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_implementacion_firma_tipo') THEN
        ALTER TABLE public.implementacion_tarea_firmas
            ADD CONSTRAINT ck_implementacion_firma_tipo
            CHECK (firma_tipo IS NULL OR firma_tipo IN ('digitada', 'manuscrita'));
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.implementacion_tarea_firmas DROP CONSTRAINT IF EXISTS ck_implementacion_firma_tipo;
ALTER TABLE public.implementacion_tarea_firmas DROP COLUMN IF EXISTS firmado_ip;
ALTER TABLE public.implementacion_tarea_firmas DROP COLUMN IF EXISTS firmado_user_agent;
ALTER TABLE public.implementacion_tarea_firmas DROP COLUMN IF EXISTS contenido_hash;
ALTER TABLE public.implementacion_tarea_firmas DROP COLUMN IF EXISTS firma_tipo;
ALTER TABLE public.implementacion_tarea_firmas DROP COLUMN IF EXISTS firma_imagen;

DROP INDEX IF EXISTS public.ix_implementacion_tareas_ticket_tarea;
DROP INDEX IF EXISTS public.ix_implementacion_planes_historia;
ALTER TABLE public.implementacion_tareas DROP COLUMN IF EXISTS ticket_tarea_id;
ALTER TABLE public.implementacion_planes DROP COLUMN IF EXISTS historia_id;
");
        }
    }
}
