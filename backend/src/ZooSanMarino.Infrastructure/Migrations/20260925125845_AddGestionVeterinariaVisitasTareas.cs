using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations;

/// <summary>
/// Agenda veterinaria, tareas territoriales, evidencia fotográfica y menú del módulo.
/// El DDL es idempotente porque las migraciones corren al arrancar ECS y una ejecución parcial
/// nunca debe impedir que el siguiente despliegue complete los objetos faltantes.
/// </summary>
public partial class AddGestionVeterinariaVisitasTareas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(UpSql);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(DownSql);

    private const string UpSql = """
CREATE TABLE IF NOT EXISTS public.visitas_tecnicas (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    farm_id integer NOT NULL,
    nucleo_id character varying(80) NULL,
    galpon_id character varying(80) NULL,
    lote_id integer NULL,
    titulo character varying(200) NOT NULL,
    objetivo character varying(2000) NULL,
    fecha_programada timestamp with time zone NOT NULL,
    fecha_realizada timestamp with time zone NULL,
    observaciones character varying(4000) NULL,
    estado character varying(16) NOT NULL DEFAULT 'PROGRAMADA',
    veterinario_user_id uuid NOT NULL,
    company_id integer NOT NULL,
    created_by_user_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_by_user_id integer NULL,
    updated_at timestamp with time zone NULL,
    deleted_at timestamp with time zone NULL,
    CONSTRAINT ck_visitas_tecnicas_estado
        CHECK (estado IN ('PROGRAMADA', 'REALIZADA', 'CANCELADA')),
    CONSTRAINT fk_visitas_tecnicas_farms_farm_id
        FOREIGN KEY (farm_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_visitas_tecnicas_users_veterinario_user_id
        FOREIGN KEY (veterinario_user_id) REFERENCES public.users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS public.tareas_campo (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    visita_id bigint NULL,
    farm_id integer NOT NULL,
    nucleo_id character varying(80) NULL,
    galpon_id character varying(80) NULL,
    lote_id integer NULL,
    titulo character varying(200) NOT NULL,
    instrucciones character varying(3000) NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date NOT NULL,
    requiere_observacion boolean NOT NULL DEFAULT false,
    requiere_foto boolean NOT NULL DEFAULT false,
    estado character varying(16) NOT NULL DEFAULT 'PENDIENTE',
    creada_por_user_id uuid NOT NULL,
    realizada_por_user_id uuid NULL,
    fecha_realizada timestamp with time zone NULL,
    observacion_cumplimiento character varying(2000) NULL,
    company_id integer NOT NULL,
    created_by_user_id integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_by_user_id integer NULL,
    updated_at timestamp with time zone NULL,
    deleted_at timestamp with time zone NULL,
    CONSTRAINT ck_tareas_campo_estado
        CHECK (estado IN ('PENDIENTE', 'REALIZADA', 'CANCELADA')),
    CONSTRAINT fk_tareas_campo_visitas_tecnicas_visita_id
        FOREIGN KEY (visita_id) REFERENCES public.visitas_tecnicas(id) ON DELETE SET NULL,
    CONSTRAINT fk_tareas_campo_farms_farm_id
        FOREIGN KEY (farm_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tareas_campo_users_creada_por_user_id
        FOREIGN KEY (creada_por_user_id) REFERENCES public.users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tareas_campo_users_realizada_por_user_id
        FOREIGN KEY (realizada_por_user_id) REFERENCES public.users(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS public.tarea_campo_evidencias (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tarea_id bigint NOT NULL,
    imagen_base64 text NOT NULL,
    file_name character varying(200) NULL,
    content_type character varying(40) NOT NULL,
    size_bytes integer NOT NULL,
    created_by_user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT fk_tarea_campo_evidencias_tareas_campo_tarea_id
        FOREIGN KEY (tarea_id) REFERENCES public.tareas_campo(id) ON DELETE CASCADE,
    CONSTRAINT fk_tarea_campo_evidencias_users_created_by_user_id
        FOREIGN KEY (created_by_user_id) REFERENCES public.users(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_visitas_tecnicas_company_fecha
    ON public.visitas_tecnicas (company_id, fecha_programada);
CREATE INDEX IF NOT EXISTS ix_visitas_tecnicas_farm_id
    ON public.visitas_tecnicas (farm_id);
CREATE INDEX IF NOT EXISTS ix_visitas_tecnicas_veterinario_estado
    ON public.visitas_tecnicas (veterinario_user_id, estado);
CREATE INDEX IF NOT EXISTS ix_tareas_campo_company_estado_fecha
    ON public.tareas_campo (company_id, estado, fecha_fin);
CREATE INDEX IF NOT EXISTS ix_tareas_campo_creada_por
    ON public.tareas_campo (creada_por_user_id);
CREATE INDEX IF NOT EXISTS ix_tareas_campo_realizada_por_user_id
    ON public.tareas_campo (realizada_por_user_id);
CREATE INDEX IF NOT EXISTS ix_tareas_campo_ubicacion
    ON public.tareas_campo (farm_id, nucleo_id, galpon_id, lote_id);
CREATE INDEX IF NOT EXISTS ix_tareas_campo_visita_id
    ON public.tareas_campo (visita_id);
CREATE INDEX IF NOT EXISTS ix_tarea_campo_evidencias_created_by_user_id
    ON public.tarea_campo_evidencias (created_by_user_id);
CREATE INDEX IF NOT EXISTS ix_tarea_campo_evidencias_tarea
    ON public.tarea_campo_evidencias (tarea_id);

INSERT INTO public.menus
    (label, icon, route, parent_id, "order", sort_order, is_group, is_active, key, created_at, updated_at)
SELECT
    'Gestión veterinaria',
    'stethoscope',
    '/gestion-veterinaria',
    NULL,
    COALESCE((SELECT MAX(m."order") FROM public.menus m WHERE m.parent_id IS NULL), 0) + 1,
    COALESCE((SELECT MAX(m.sort_order) FROM public.menus m WHERE m.parent_id IS NULL), 0) + 1,
    false,
    true,
    'gestion_veterinaria',
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM public.menus WHERE route = '/gestion-veterinaria'
);

INSERT INTO public.company_menus
    (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
    c.id,
    m.id,
    true,
    COALESCE((
        SELECT MAX(cm.sort_order) + 1
        FROM public.company_menus cm
        WHERE cm.company_id = c.id
    ), 0),
    NULL
FROM public.companies c
JOIN public.menus m ON m.route = '/gestion-veterinaria'
WHERE NOT EXISTS (
    SELECT 1
    FROM public.company_menus cm
    WHERE cm.company_id = c.id AND cm.menu_id = m.id
);
""";

    private const string DownSql = """
DELETE FROM public.role_menus
WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/gestion-veterinaria');
DELETE FROM public.company_menus
WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/gestion-veterinaria');
DELETE FROM public.menu_permissions
WHERE menu_id IN (SELECT id FROM public.menus WHERE route = '/gestion-veterinaria');
DELETE FROM public.menus WHERE route = '/gestion-veterinaria';

DROP TABLE IF EXISTS public.tarea_campo_evidencias;
DROP TABLE IF EXISTS public.tareas_campo;
DROP TABLE IF EXISTS public.visitas_tecnicas;
""";
}
