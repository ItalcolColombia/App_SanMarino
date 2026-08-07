using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// ItalJira — el nivel HISTORIA (épica) encima de las tareas, y el trabajo que nace en el área
    /// de desarrollo sin pasar por un ticket:
    ///  - Tabla nueva <c>historias</c> (código <c>HIS-AAAA-NNNN</c>, estado/prioridad con el mismo
    ///    vocabulario de las tareas, responsable, orden de tablero, estimación y fechas de roadmap).
    ///  - <c>ticket_tareas.historia_id</c> y <c>tickets.historia_id</c> (FK <c>ON DELETE SET NULL</c>:
    ///    borrar una épica devuelve su trabajo a la bandeja «sin historia», nunca lo borra).
    ///  - <c>ticket_tareas.ticket_id</c> y <c>ticket_tiempos.ticket_id</c> pasan a NULLABLE — es lo
    ///    que permite una tarea nacida en desarrollo. Ninguna fila existente cambia: la columna
    ///    solo deja de ser obligatoria.
    ///
    /// <b>Sin CHECK de «no huérfana».</b> La primera versión de esta migración exigía que toda tarea
    /// tuviera caso, historia o tarea padre. El smoke lo tiró abajo: una tarea con los tres en NULL
    /// es un estado LEGÍTIMO — es la bandeja «sin historia» de ItalJira, donde nace una tarea suelta
    /// y adonde vuelve el trabajo cuando se borra su épica. Con el CHECK, borrar una historia daba
    /// 500. El invariante real (que ninguna tarea quede invisible) lo garantiza la consulta: toda
    /// fila cae en el árbol de su historia, en el panel de su caso, o en la bandeja de sueltas.
    ///
    /// Todo el DDL es IDEMPOTENTE (IF NOT EXISTS / DO $$ ... $$): el deploy aplica las migraciones
    /// al arrancar y una re-ejecución no puede tumbar el arranque de la app.
    ///
    /// ⚠️ El ModelSnapshot que acompaña a esta migración incorpora además
    /// <c>seguimiento_diario_levante.venta_aves_hembras/machos</c>, que otra sesión agregó por SQL
    /// idempotente dejando el snapshot un paso atrás a propósito (ver
    /// <c>20260806235000_VentaAvesEnFilaDiariaLevante</c>). Esta migración NO las crea —ya existen—;
    /// solo se salda el desfase del snapshot, que es exactamente lo que esa migración anticipaba.
    /// </summary>
    public partial class AddHistoriasItalJira : Migration
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
-- ─────────────────────────────────────────────────────────────────────────────
-- 1) historias: la épica de ItalJira
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.historias (
    id                    bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    codigo                character varying(40) NULL,
    pais_id               integer NOT NULL,
    titulo                character varying(200) NOT NULL,
    descripcion           text NULL,
    estado                character varying(20) NOT NULL DEFAULT 'BACKLOG',
    prioridad             character varying(20) NOT NULL DEFAULT 'MEDIA',
    responsable_user_guid uuid NULL,
    orden                 integer NOT NULL DEFAULT 0,
    horas_estimadas       numeric(8,2) NULL,
    fecha_inicio_plan     date NULL,
    fecha_fin_plan        date NULL,
    fecha_inicio_real     timestamp with time zone NULL,
    fecha_fin_real        timestamp with time zone NULL,
    etiquetas             character varying(300) NULL,
    company_id            integer NOT NULL,
    created_by_user_id    integer NOT NULL,
    created_at            timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
    updated_by_user_id    integer NULL,
    updated_at            timestamp with time zone NULL,
    deleted_at            timestamp with time zone NULL,
    CONSTRAINT pk_historias PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_historias_codigo      ON public.historias (codigo);
CREATE INDEX IF NOT EXISTS ix_historias_company_id  ON public.historias (company_id);
CREATE INDEX IF NOT EXISTS ix_historias_estado      ON public.historias (estado);
CREATE INDEX IF NOT EXISTS ix_historias_responsable ON public.historias (responsable_user_guid);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) Enganche del trabajo existente con la historia
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.ticket_tareas ADD COLUMN IF NOT EXISTS historia_id bigint NULL;
ALTER TABLE public.tickets       ADD COLUMN IF NOT EXISTS historia_id bigint NULL;

CREATE INDEX IF NOT EXISTS ix_ticket_tareas_historia_id ON public.ticket_tareas (historia_id);
CREATE INDEX IF NOT EXISTS ix_tickets_historia_id       ON public.tickets (historia_id);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ticket_tareas_historias_historia_id') THEN
        ALTER TABLE public.ticket_tareas
            ADD CONSTRAINT fk_ticket_tareas_historias_historia_id
            FOREIGN KEY (historia_id) REFERENCES public.historias (id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_tickets_historias_historia_id') THEN
        ALTER TABLE public.tickets
            ADD CONSTRAINT fk_tickets_historias_historia_id
            FOREIGN KEY (historia_id) REFERENCES public.historias (id) ON DELETE SET NULL;
    END IF;
END $$;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) El caso deja de ser obligatorio (trabajo nacido en desarrollo)
--    DROP NOT NULL es idempotente por naturaleza: si ya es nullable, no hace nada.
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.ticket_tareas  ALTER COLUMN ticket_id DROP NOT NULL;
ALTER TABLE public.ticket_tiempos ALTER COLUMN ticket_id DROP NOT NULL;

-- Defensivo: si una base intermedia llegó a tener el CHECK de «no huérfana» de la primera versión
-- de esta migración, se retira. Impedía la bandeja de tareas sueltas (ver el resumen de arriba).
ALTER TABLE public.ticket_tareas DROP CONSTRAINT IF EXISTS ck_ticket_tareas_no_huerfana;
";

        private const string DOWN_SQL = @"
-- Volver a NOT NULL solo es posible si no quedó trabajo sin caso; si lo hay, el Down falla
-- a propósito en vez de borrar filas reales en silencio.
ALTER TABLE public.ticket_tareas  ALTER COLUMN ticket_id SET NOT NULL;
ALTER TABLE public.ticket_tiempos ALTER COLUMN ticket_id SET NOT NULL;

ALTER TABLE public.ticket_tareas DROP CONSTRAINT IF EXISTS fk_ticket_tareas_historias_historia_id;
ALTER TABLE public.tickets       DROP CONSTRAINT IF EXISTS fk_tickets_historias_historia_id;

DROP INDEX IF EXISTS public.ix_ticket_tareas_historia_id;
DROP INDEX IF EXISTS public.ix_tickets_historia_id;

ALTER TABLE public.ticket_tareas DROP COLUMN IF EXISTS historia_id;
ALTER TABLE public.tickets       DROP COLUMN IF EXISTS historia_id;

DROP TABLE IF EXISTS public.historias;
";
    }
}
