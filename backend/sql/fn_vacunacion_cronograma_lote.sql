-- ============================================================================
-- fn_vacunacion_cronograma_lote — Cronograma completo de un lote en UN round trip.
--
-- Reemplaza las ~6 consultas encadenadas de VacunacionCronogramaService.GetCronogramaLoteAsync:
-- resuelve el par Levante↔Producción (cronograma "de toda la vida del lote"), junta
-- registro de aplicación + nombres (vacuna, lote, granja, usuarios) y calcula la franja.
--
-- ⚠ La fórmula de franja DEBE mantenerse idéntica a VacunacionCalculos.CalcularFranja
--   (y a fecha_objetivo_efectiva de fn_vacunacion_cumplimiento_lote):
--     Semana → encaset + (valor-1)*7 · Dia → encaset + valor · Fecha → fecha_objetivo,
--     franja = [base - rango_dias_antes, base + rango_dias_despues].
--   Franja NULL (lote sin fecha de encaset con unidad Semana/Dia) → el wrapper C#
--   (VacunacionCronogramaMapper) lanza InvalidOperationException, igual que el cálculo puro.
--
-- Los nombres de usuario se resuelven por CÉDULA (users.cedula = id entero del sistema,
-- mismo mapeo que TicketService) con LATERAL LIMIT 1 para tolerar cédulas duplicadas.
--
-- Columnas snake_case (gotcha SqlQueryRaw + EFCore.NamingConventions), sin dígitos.
-- Sincronizada con la migración AddFnVacunacionConsultas.
-- ============================================================================
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
    -- Emparejamiento Levante↔Producción (LotePosturaProduccion.lote_postura_levante_id);
    -- Engorde no tiene fases previas: solo su propio id.
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
    -- El encaset/nombre se toma del lote de LA LÍNEA DEL ÍTEM (paridad con MapItem).
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
