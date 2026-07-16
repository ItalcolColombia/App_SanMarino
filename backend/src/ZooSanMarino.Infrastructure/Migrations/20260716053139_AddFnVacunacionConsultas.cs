using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Funciones de consulta del módulo Vacunación (la BD filtra, el backend orquesta):
    /// fn_vacunacion_filter_data (combos en 1 round trip), fn_vacunacion_cronograma_lote
    /// (cronograma completo en 1 round trip con franja en SQL) y fn_vacunacion_cumplimiento_detalle
    /// (reportería ítem a ítem) + índice company/país del cronograma. Sincronizadas con
    /// backend/sql/fn_vacunacion_*.sql — si se edita un .sql, actualizar aquí también.
    /// </summary>
    public partial class AddFnVacunacionConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_vacunacion_cronograma_item_company_pais " +
                "ON public.vacunacion_cronograma_item (company_id, pais_id);",
                suppressTransaction: true);

            migrationBuilder.Sql(FN_FILTER_DATA, suppressTransaction: true);
            migrationBuilder.Sql(FN_CRONOGRAMA_LOTE, suppressTransaction: true);
            migrationBuilder.Sql(FN_CUMPLIMIENTO_DETALLE, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.fn_vacunacion_cumplimiento_detalle(INT, INT, INT[], TEXT, TEXT, INT[], TEXT, DATE, DATE);",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.fn_vacunacion_cronograma_lote(INT, TEXT, INT);",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT);",
                suppressTransaction: true);
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS public.ix_vacunacion_cronograma_item_company_pais;",
                suppressTransaction: true);
        }

        private const string FN_FILTER_DATA = @"
DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_filter_data(
    p_user_guid  UUID,
    p_company_id INT,
    p_pais_id    INT DEFAULT NULL
)
RETURNS jsonb
LANGUAGE sql
STABLE
AS $$
WITH granjas AS (
    SELECT f.id, f.company_id, f.name
    FROM public.farms f
    WHERE f.company_id = p_company_id
      AND f.deleted_at IS NULL
      AND EXISTS (SELECT 1 FROM public.user_farms uf
                  WHERE uf.farm_id = f.id AND uf.user_id = p_user_guid)
      AND (p_pais_id IS NULL OR EXISTS (
            SELECT 1 FROM public.departamentos d
            WHERE d.departamento_id = f.departamento_id AND d.pais_id = p_pais_id))
),
lotes AS (
    SELECT l.lote_postura_levante_id AS lote_id, 'Levante' AS linea_productiva,
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre AS estado_cierre
    FROM public.lote_postura_levante l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_levante_id IS NOT NULL

    UNION ALL

    SELECT l.lote_postura_produccion_id, 'Produccion',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre
    FROM public.lote_postura_produccion l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_produccion_id IS NOT NULL

    UNION ALL

    SELECT l.lote_ave_engorde_id, 'Engorde',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_operativo_lote
    FROM public.lote_ave_engorde l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_ave_engorde_id IS NOT NULL
),
vacunas AS (
    -- Case-insensitive: el dato real trae 'Vacuna'/'vacuna' mezclado (paridad con EF ILike).
    SELECT i.id, i.codigo, i.nombre, i.unidad
    FROM public.item_inventario_ecuador i
    WHERE i.company_id = p_company_id AND i.activo = true AND i.tipo_item ILIKE 'vacuna'
),
usuarios AS (
    SELECT DISTINCT (u.cedula)::int AS id,
           NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE u.is_active = true
      AND u.cedula ~ '^[0-9]{1,18}$'
      AND (u.cedula)::bigint BETWEEN 1 AND 2147483647
      AND EXISTS (SELECT 1 FROM public.user_companies uc
                  WHERE uc.user_id = u.id AND uc.company_id = p_company_id)
)
SELECT jsonb_build_object(
    'granjas', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'id', g.id, 'companyId', g.company_id, 'name', g.name)
               ORDER BY g.name)
        FROM granjas g), '[]'::jsonb),
    'lotes', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'loteId', l.lote_id, 'lineaProductiva', l.linea_productiva,
                   'loteNombre', l.lote_nombre, 'granjaId', l.granja_id,
                   'nucleoId', l.nucleo_id, 'galponId', l.galpon_id,
                   -- ::date: serialización estable (sin hora/offset del timezone del servidor)
                   'fechaEncaset', l.fecha_encaset::date, 'estadoCierre', l.estado_cierre)
               ORDER BY l.fecha_encaset DESC NULLS LAST)
        FROM lotes l), '[]'::jsonb),
    'vacunas', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
                   'id', v.id, 'codigo', v.codigo, 'nombre', v.nombre, 'unidad', v.unidad)
               ORDER BY v.nombre)
        FROM vacunas v), '[]'::jsonb),
    'usuarios', COALESCE((
        SELECT jsonb_agg(jsonb_build_object('id', u.id, 'nombre', u.nombre)
               ORDER BY u.nombre NULLS LAST)
        FROM usuarios u), '[]'::jsonb)
);
$$;
";

        private const string FN_CRONOGRAMA_LOTE = @"
DROP FUNCTION IF EXISTS public.fn_vacunacion_cronograma_lote(INT, TEXT, INT);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_cronograma_lote(
    p_company_id       INT,
    p_linea_productiva TEXT,
    p_lote_id          INT
)
RETURNS TABLE (
    id                        INT,
    linea_productiva          TEXT,
    lote_id                   INT,
    lote_nombre               TEXT,
    granja_id                 INT,
    granja_nombre             TEXT,
    nucleo_id                 TEXT,
    galpon_id                 TEXT,
    item_inventario_id        INT,
    item_inventario_nombre    TEXT,
    unidad_objetivo           TEXT,
    valor_objetivo            INT,
    fecha_objetivo            DATE,
    rango_dias_antes          INT,
    rango_dias_despues        INT,
    fecha_inicio_franja       DATE,
    fecha_fin_franja          DATE,
    orden                     INT,
    activo                    BOOLEAN,
    notas                     TEXT,
    registro_id               INT,
    registro_estado           TEXT,
    registro_fecha_aplicacion DATE,
    registro_dias_desviacion  INT,
    registro_incumplido       BOOLEAN,
    registro_motivo           TEXT,
    usuario_registra_id       INT,
    usuario_registra_nombre   TEXT,
    aplicado_por_user_id      INT,
    aplicado_por_user_nombre  TEXT,
    aplicado_por_nombre_libre TEXT
)
LANGUAGE sql
STABLE
AS $$
WITH par AS (
    SELECT
        CASE WHEN p_linea_productiva = 'Levante' THEN p_lote_id
             WHEN p_linea_productiva = 'Produccion' THEN
                 (SELECT lpp.lote_postura_levante_id
                  FROM public.lote_postura_produccion lpp
                  WHERE lpp.lote_postura_produccion_id = p_lote_id
                    AND lpp.company_id = p_company_id
                  LIMIT 1)
        END AS levante_id,
        CASE WHEN p_linea_productiva = 'Produccion' THEN p_lote_id
             WHEN p_linea_productiva = 'Levante' THEN
                 (SELECT lpp.lote_postura_produccion_id
                  FROM public.lote_postura_produccion lpp
                  WHERE lpp.lote_postura_levante_id = p_lote_id
                    AND lpp.company_id = p_company_id
                  LIMIT 1)
        END AS produccion_id,
        CASE WHEN p_linea_productiva = 'Engorde' THEN p_lote_id END AS engorde_id
),
base AS (
    SELECT
        ci.id                 AS item_id,
        ci.linea_productiva   AS item_linea,
        COALESCE(ci.lote_postura_levante_id, ci.lote_postura_produccion_id, ci.lote_ave_engorde_id) AS item_lote_id,
        COALESCE(lpl.lote_nombre, lpp.lote_nombre, lae.lote_nombre, '')          AS item_lote_nombre,
        COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date  AS fecha_encaset,
        ci.granja_id, ci.nucleo_id, ci.galpon_id,
        ci.item_inventario_id, ci.unidad_objetivo, ci.valor_objetivo, ci.fecha_objetivo,
        ci.rango_dias_antes, ci.rango_dias_despues, ci.orden, ci.activo, ci.notas
    FROM public.vacunacion_cronograma_item ci
    CROSS JOIN par
    LEFT JOIN public.lote_postura_levante lpl
           ON ci.linea_productiva = 'Levante' AND lpl.lote_postura_levante_id = ci.lote_postura_levante_id
    LEFT JOIN public.lote_postura_produccion lpp
           ON ci.linea_productiva = 'Produccion' AND lpp.lote_postura_produccion_id = ci.lote_postura_produccion_id
    LEFT JOIN public.lote_ave_engorde lae
           ON ci.linea_productiva = 'Engorde' AND lae.lote_ave_engorde_id = ci.lote_ave_engorde_id
    WHERE ci.company_id = p_company_id
      AND ((par.levante_id    IS NOT NULL AND ci.lote_postura_levante_id    = par.levante_id)
        OR (par.produccion_id IS NOT NULL AND ci.lote_postura_produccion_id = par.produccion_id)
        OR (par.engorde_id    IS NOT NULL AND ci.lote_ave_engorde_id        = par.engorde_id))
),
franja AS (
    SELECT b.*,
        (CASE b.unidad_objetivo
            WHEN 'Semana' THEN CASE WHEN b.fecha_encaset IS NOT NULL AND b.valor_objetivo IS NOT NULL
                                    THEN b.fecha_encaset + ((b.valor_objetivo - 1) * 7) END
            WHEN 'Dia'    THEN CASE WHEN b.fecha_encaset IS NOT NULL AND b.valor_objetivo IS NOT NULL
                                    THEN b.fecha_encaset + b.valor_objetivo END
            WHEN 'Fecha'  THEN b.fecha_objetivo::date
         END) AS fecha_base
    FROM base b
)
SELECT
    f.item_id                                   AS id,
    f.item_linea                                AS linea_productiva,
    f.item_lote_id                              AS lote_id,
    f.item_lote_nombre                          AS lote_nombre,
    f.granja_id,
    fm.name                                     AS granja_nombre,
    f.nucleo_id,
    f.galpon_id,
    f.item_inventario_id,
    COALESCE(ii.nombre, '')                     AS item_inventario_nombre,
    f.unidad_objetivo,
    f.valor_objetivo,
    f.fecha_objetivo::date                      AS fecha_objetivo,
    f.rango_dias_antes,
    f.rango_dias_despues,
    (f.fecha_base - f.rango_dias_antes)         AS fecha_inicio_franja,
    (f.fecha_base + f.rango_dias_despues)       AS fecha_fin_franja,
    f.orden,
    f.activo,
    f.notas,
    ra.id                                       AS registro_id,
    ra.estado                                   AS registro_estado,
    ra.fecha_aplicacion::date                   AS registro_fecha_aplicacion,
    ra.dias_desviacion                          AS registro_dias_desviacion,
    ra.incumplido                               AS registro_incumplido,
    ra.motivo_descripcion                       AS registro_motivo,
    ra.usuario_registra_id,
    ur.nombre                                   AS usuario_registra_nombre,
    ra.aplicado_por_user_id,
    ua.nombre                                   AS aplicado_por_user_nombre,
    ra.aplicado_por_nombre_libre
FROM franja f
LEFT JOIN public.farms fm ON fm.id = f.granja_id
LEFT JOIN public.item_inventario_ecuador ii ON ii.id = f.item_inventario_id
LEFT JOIN public.vacunacion_registro_aplicacion ra ON ra.vacunacion_cronograma_item_id = f.item_id
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE ra.usuario_registra_id IS NOT NULL AND u.cedula = ra.usuario_registra_id::text
    LIMIT 1
) ur ON true
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE ra.aplicado_por_user_id IS NOT NULL AND u.cedula = ra.aplicado_por_user_id::text
    LIMIT 1
) ua ON true
ORDER BY (f.fecha_base - f.rango_dias_antes) NULLS LAST, f.orden;
$$;
";

        private const string FN_CUMPLIMIENTO_DETALLE = @"
DROP FUNCTION IF EXISTS public.fn_vacunacion_cumplimiento_detalle(INT, INT, INT[], TEXT, TEXT, INT[], TEXT, DATE, DATE);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_cumplimiento_detalle(
    p_company_id       INT,
    p_pais_id          INT     DEFAULT NULL,
    p_granja_ids       INT[]   DEFAULT NULL,
    p_nucleo_id        TEXT    DEFAULT NULL,
    p_galpon_id        TEXT    DEFAULT NULL,
    p_lote_ids         INT[]   DEFAULT NULL,
    p_linea_productiva TEXT    DEFAULT NULL,
    p_fecha_desde      DATE    DEFAULT NULL,
    p_fecha_hasta      DATE    DEFAULT NULL
)
RETURNS TABLE (
    item_id                 INT,
    granja_id               INT,
    granja_nombre           TEXT,
    lote_id                 INT,
    lote_nombre             TEXT,
    linea_productiva        TEXT,
    nucleo_id               TEXT,
    galpon_id               TEXT,
    vacuna_nombre           TEXT,
    unidad_objetivo         TEXT,
    valor_objetivo          INT,
    fecha_objetivo_efectiva DATE,
    fecha_inicio_franja     DATE,
    fecha_fin_franja        DATE,
    estado                  TEXT,
    fecha_aplicacion        DATE,
    dias_desviacion         INT,
    incumplido              BOOLEAN,
    motivo                  TEXT,
    aplicado_por            TEXT,
    registrado_por          TEXT,
    notas                   TEXT
)
LANGUAGE sql
STABLE
AS $$
WITH base AS (
    SELECT
        ci.id AS item_id,
        COALESCE(ci.lote_postura_levante_id, ci.lote_postura_produccion_id, ci.lote_ave_engorde_id) AS lote_id,
        ci.linea_productiva,
        ci.granja_id,
        f.name AS granja_nombre,
        ci.nucleo_id,
        ci.galpon_id,
        COALESCE(lpl.lote_nombre, lpp.lote_nombre, lae.lote_nombre) AS lote_nombre,
        COALESCE(ii.nombre, '') AS vacuna_nombre,
        ci.unidad_objetivo,
        ci.valor_objetivo,
        ci.rango_dias_antes,
        ci.rango_dias_despues,
        ci.notas,
        COALESCE(ra.estado, 'Pendiente') AS estado,
        ra.fecha_aplicacion::date AS fecha_aplicacion,
        ra.dias_desviacion,
        COALESCE(ra.incumplido, false) AS incumplido,
        ra.motivo_descripcion AS motivo,
        ra.aplicado_por_nombre_libre,
        ra.aplicado_por_user_id,
        ra.usuario_registra_id,
        (CASE ci.unidad_objetivo
            WHEN 'Semana' THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ((ci.valor_objetivo - 1) * 7)
            WHEN 'Dia'    THEN COALESCE(lpl.fecha_encaset, lpp.fecha_encaset, lae.fecha_encaset)::date + ci.valor_objetivo
            WHEN 'Fecha'  THEN ci.fecha_objetivo::date
         END) AS fecha_objetivo_efectiva
    FROM public.vacunacion_cronograma_item ci
    LEFT JOIN public.vacunacion_registro_aplicacion ra ON ra.vacunacion_cronograma_item_id = ci.id
    LEFT JOIN public.farms f ON f.id = ci.granja_id
    LEFT JOIN public.item_inventario_ecuador ii ON ii.id = ci.item_inventario_id
    LEFT JOIN public.lote_postura_levante lpl ON lpl.lote_postura_levante_id = ci.lote_postura_levante_id
    LEFT JOIN public.lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = ci.lote_postura_produccion_id
    LEFT JOIN public.lote_ave_engorde lae ON lae.lote_ave_engorde_id = ci.lote_ave_engorde_id
    WHERE ci.company_id = p_company_id
      AND ci.activo = true
      AND (p_pais_id IS NULL OR ci.pais_id = p_pais_id)
      AND (p_granja_ids IS NULL OR ci.granja_id = ANY(p_granja_ids))
      AND (p_nucleo_id IS NULL OR ci.nucleo_id = p_nucleo_id)
      AND (p_galpon_id IS NULL OR ci.galpon_id = p_galpon_id)
      AND (p_linea_productiva IS NULL OR ci.linea_productiva = p_linea_productiva)
),
filtrado AS (
    SELECT b.*
    FROM base b
    WHERE (p_lote_ids IS NULL OR b.lote_id = ANY(p_lote_ids))
      AND (p_fecha_desde IS NULL OR b.fecha_objetivo_efectiva IS NULL OR b.fecha_objetivo_efectiva >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR b.fecha_objetivo_efectiva IS NULL OR b.fecha_objetivo_efectiva <= p_fecha_hasta)
)
SELECT
    fl.item_id,
    fl.granja_id,
    fl.granja_nombre,
    fl.lote_id,
    fl.lote_nombre,
    fl.linea_productiva,
    fl.nucleo_id,
    fl.galpon_id,
    fl.vacuna_nombre,
    fl.unidad_objetivo,
    fl.valor_objetivo,
    fl.fecha_objetivo_efectiva,
    (fl.fecha_objetivo_efectiva - fl.rango_dias_antes)   AS fecha_inicio_franja,
    (fl.fecha_objetivo_efectiva + fl.rango_dias_despues) AS fecha_fin_franja,
    fl.estado,
    fl.fecha_aplicacion,
    fl.dias_desviacion,
    fl.incumplido,
    fl.motivo,
    COALESCE(fl.aplicado_por_nombre_libre, ua.nombre)    AS aplicado_por,
    ur.nombre                                            AS registrado_por,
    fl.notas
FROM filtrado fl
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE fl.aplicado_por_user_id IS NOT NULL AND u.cedula = fl.aplicado_por_user_id::text
    LIMIT 1
) ua ON true
LEFT JOIN LATERAL (
    SELECT NULLIF(btrim(COALESCE(u.first_name, '') || ' ' || COALESCE(u.sur_name, '')), '') AS nombre
    FROM public.users u
    WHERE fl.usuario_registra_id IS NOT NULL AND u.cedula = fl.usuario_registra_id::text
    LIMIT 1
) ur ON true
ORDER BY fl.granja_nombre NULLS LAST, fl.lote_nombre NULLS LAST,
         fl.fecha_objetivo_efectiva NULLS LAST, fl.item_id;
$$;
";
    }
}
