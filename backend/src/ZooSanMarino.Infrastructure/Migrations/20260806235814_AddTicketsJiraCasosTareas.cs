using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Módulo de tickets como CASOS tipo Jira:
    ///  - <c>tickets</c>: solicitante delegado ("a nombre de"), prioridad, orden del tablero,
    ///    estimación, compromiso de solución y fechas de roadmap.
    ///  - <c>ticket_notas.tipo_evento</c>: clasifica las notas que escribe el sistema
    ///    (NULL = comentario humano ⇒ las notas existentes conservan su significado).
    ///  - Tablas nuevas <c>ticket_tareas</c> (tablero de tareas) y <c>ticket_tiempos</c> (worklog).
    ///  - Menús <c>tickets.tablero</c> y <c>tickets.roadmap</c> gated por <c>tickets.admin</c>.
    ///
    /// Todo el DDL es IDEMPOTENTE (IF NOT EXISTS / WHERE NOT EXISTS): el deploy aplica las
    /// migraciones al arrancar y una re-ejecución no puede romper el arranque de la app.
    /// </summary>
    public partial class AddTicketsJiraCasosTareas : Migration
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
-- 1) tickets: solicitante delegado + gestión tipo tablero
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS solicitante_user_guid uuid NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS solicitante_user_id   integer NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS prioridad             character varying(20) NOT NULL DEFAULT 'MEDIA';
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS orden_tablero         integer NOT NULL DEFAULT 0;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS horas_estimadas       numeric(8,2) NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS fecha_limite          timestamp with time zone NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS fecha_inicio_plan     date NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS fecha_fin_plan        date NULL;

CREATE INDEX IF NOT EXISTS ix_tickets_solicitante_user_id ON public.tickets (solicitante_user_id);
CREATE INDEX IF NOT EXISTS ix_tickets_prioridad           ON public.tickets (prioridad);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2) ticket_notas: clasificación de las notas de sistema (NULL = comentario humano)
-- ─────────────────────────────────────────────────────────────────────────────
ALTER TABLE public.ticket_notas ADD COLUMN IF NOT EXISTS tipo_evento character varying(30) NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- 3) ticket_tareas: el tablero de trabajo de cada caso
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.ticket_tareas (
    id                 bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    ticket_id          bigint NOT NULL,
    codigo             character varying(40) NULL,
    tipo               character varying(20) NOT NULL DEFAULT 'TAREA',
    estado             character varying(20) NOT NULL DEFAULT 'BACKLOG',
    prioridad          character varying(20) NOT NULL DEFAULT 'MEDIA',
    titulo             character varying(200) NOT NULL,
    descripcion        text NULL,
    asignado_user_guid uuid NULL,
    parent_tarea_id    bigint NULL,
    orden              integer NOT NULL DEFAULT 0,
    horas_estimadas    numeric(8,2) NULL,
    fecha_inicio_plan  date NULL,
    fecha_fin_plan     date NULL,
    fecha_inicio_real  timestamp with time zone NULL,
    fecha_fin_real     timestamp with time zone NULL,
    etiquetas          character varying(300) NULL,
    company_id         integer NOT NULL,
    created_by_user_id integer NOT NULL,
    created_at         timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
    updated_by_user_id integer NULL,
    updated_at         timestamp with time zone NULL,
    deleted_at         timestamp with time zone NULL,
    CONSTRAINT pk_ticket_tareas PRIMARY KEY (id),
    CONSTRAINT fk_ticket_tareas_tickets_ticket_id
        FOREIGN KEY (ticket_id) REFERENCES public.tickets (id) ON DELETE CASCADE,
    CONSTRAINT fk_ticket_tareas_ticket_tareas_parent_tarea_id
        FOREIGN KEY (parent_tarea_id) REFERENCES public.ticket_tareas (id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_ticket_tareas_ticket_id ON public.ticket_tareas (ticket_id);
CREATE INDEX IF NOT EXISTS ix_ticket_tareas_estado    ON public.ticket_tareas (estado);
CREATE INDEX IF NOT EXISTS ix_ticket_tareas_asignado  ON public.ticket_tareas (asignado_user_guid);
CREATE INDEX IF NOT EXISTS ix_ticket_tareas_parent    ON public.ticket_tareas (parent_tarea_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4) ticket_tiempos: registro de horas (worklog). Borrado lógico para no alterar
--    totales ya reportados.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.ticket_tiempos (
    id          bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    ticket_id   bigint NOT NULL,
    tarea_id    bigint NULL,
    user_guid   uuid NULL,
    user_id     integer NOT NULL,
    fecha       date NOT NULL,
    horas       numeric(6,2) NOT NULL,
    descripcion character varying(500) NULL,
    created_at  timestamp with time zone NOT NULL DEFAULT timezone('utc', now()),
    deleted_at  timestamp with time zone NULL,
    CONSTRAINT pk_ticket_tiempos PRIMARY KEY (id),
    CONSTRAINT fk_ticket_tiempos_tickets_ticket_id
        FOREIGN KEY (ticket_id) REFERENCES public.tickets (id) ON DELETE CASCADE,
    CONSTRAINT fk_ticket_tiempos_ticket_tareas_tarea_id
        FOREIGN KEY (tarea_id) REFERENCES public.ticket_tareas (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_ticket_tiempos_ticket_id ON public.ticket_tiempos (ticket_id);
CREATE INDEX IF NOT EXISTS ix_ticket_tiempos_tarea_id  ON public.ticket_tiempos (tarea_id);
CREATE INDEX IF NOT EXISTS ix_ticket_tiempos_user_guid ON public.ticket_tiempos (user_guid);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5) Menús del módulo de administración (localizados por key, nunca por id fijo:
--    los ids difieren entre local y prod).
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.tablero', 'Tablero de casos', 'columns', '/tickets/tablero', 4, 4, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.tablero');

INSERT INTO public.menus (key, label, icon, route, ""order"", sort_order, is_group, is_active, parent_id, created_at, updated_at)
SELECT 'tickets.roadmap', 'Roadmap', 'calendar', '/tickets/roadmap', 5, 5, false, true, p.id, now(), now()
FROM public.menus p
WHERE p.key = 'tickets'
  AND NOT EXISTS (SELECT 1 FROM public.menus m WHERE m.key = 'tickets.roadmap');

-- Gating por permiso: ambas vistas son de gestión (resolutor o administrador global).
INSERT INTO public.menu_permissions (menu_id, permission_id)
SELECT m.id, p.id
FROM public.menus m
JOIN public.permissions p ON p.key IN ('tickets.gestionar','tickets.admin')
WHERE m.key IN ('tickets.tablero','tickets.roadmap')
  AND NOT EXISTS (
        SELECT 1 FROM public.menu_permissions mp
        WHERE mp.menu_id = m.id AND mp.permission_id = p.id);
";

        private const string DOWN_SQL = @"
-- Menús
DELETE FROM public.menu_permissions
WHERE menu_id IN (SELECT id FROM public.menus WHERE key IN ('tickets.tablero','tickets.roadmap'));
DELETE FROM public.menus WHERE key IN ('tickets.tablero','tickets.roadmap');

-- Tablas nuevas (tiempos primero por la FK hacia tareas)
DROP TABLE IF EXISTS public.ticket_tiempos;
DROP TABLE IF EXISTS public.ticket_tareas;

-- Columnas nuevas
ALTER TABLE public.ticket_notas DROP COLUMN IF EXISTS tipo_evento;

DROP INDEX IF EXISTS public.ix_tickets_solicitante_user_id;
DROP INDEX IF EXISTS public.ix_tickets_prioridad;

ALTER TABLE public.tickets DROP COLUMN IF EXISTS solicitante_user_guid;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS solicitante_user_id;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS prioridad;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS orden_tablero;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS horas_estimadas;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS fecha_limite;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS fecha_inicio_plan;
ALTER TABLE public.tickets DROP COLUMN IF EXISTS fecha_fin_plan;
";
    }
}
