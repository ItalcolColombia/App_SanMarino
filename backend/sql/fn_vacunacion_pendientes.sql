-- ============================================================================
-- fn_vacunacion_pendientes — Bandeja "hoy me toca": las vacunas que un usuario
-- tiene sin registrar, de TODOS sus lotes vivos, en UN solo round trip.
--
-- Alimenta GET /api/VacunacionRegistro/pendientes y el panel del inicio. Sin esta
-- función habría que abrir lote por lote para saber qué falta.
--
-- ⚠ La fórmula de franja DEBE mantenerse idéntica a VacunacionCalculos.CalcularFranja
--   y a fn_vacunacion_cronograma_lote:
--     Semana → encaset + (valor-1)*7 · Dia → encaset + valor · Fecha → fecha_objetivo,
--     franja = [base - rango_dias_antes, base + rango_dias_despues].
--   Franja NULL (Semana/Dia sin fecha de encaset) ⇒ la fila NO entra en la bandeja:
--   no se inventa una fecha para apurar a nadie.
--
-- ⚠ La clasificación (Vencido / EnFranja / Proximo) espeja a
--   VacunacionPendientesCalculos.Clasificar, que es su especificación ejecutable
--   (tests xUnit). El día del fin de franja TODAVÍA cumple: hoy = fin ⇒ EnFranja.
--
-- ⚠ p_hoy viaja desde C# con DateTime.UtcNow.Date — la MISMA base con la que el
--   registro sella la aplicación. No se usa CURRENT_DATE: dependería de la zona
--   horaria de la sesión y la bandeja diría un día distinto que el guardado.
--
-- ⚠ ALCANCE: el filtro es el MISMO que el de fn_vacunacion_filter_data — granja
--   (user_farms + empresa + país) y, desde W4, ubicación granular. Las dos funciones se
--   cambian juntas: si una sube y la otra no, la bandeja muestra lotes que el resto del
--   módulo ya no deja ver.
--   Los 4 parámetros p_scope_* son el CIERRE de visibilidad ya calculado en C#
--   (UserLocationScopeCalculos.ComputeScope + AplanarParaSql); acá sólo se prueba
--   pertenencia a conjuntos. Núcleos con clave COMPUESTA 'granjaId|nucleoId'.
--   Granja en p_scope_farm_ids y ausente del resto ⇒ CERO filas (fail-closed).
--   Sin DEFAULT a propósito: el llamador que los olvide debe fallar, no ver toda la empresa.
--
-- Pendiente = sin registro, o con registro en estado 'Pendiente' (mismo criterio que
-- el guard de VacunacionRegistroService.CargarItemAsync y que el materializador).
-- Lote cerrado ⇒ fuera: se compara SIEMPRE por desigualdad contra 'Cerrado' (el dato
-- dice 'Abierto' y 'Abierta' según quién creó el lote).
--
-- Granja/núcleo/galpón salen del LOTE (dónde está hoy), no del ítem: el que va a
-- vacunar necesita la ubicación vigente.
--
-- Columnas snake_case (gotcha SqlQueryRaw + EFCore.NamingConventions), sin dígitos.
-- Sincronizada con las migraciones AddFnVacunacionPendientes y ScopingUbicacionVacunacionFns.
-- ============================================================================
DROP FUNCTION IF EXISTS public.fn_vacunacion_pendientes(UUID, INT, INT, DATE, INT);
DROP FUNCTION IF EXISTS public.fn_vacunacion_pendientes(UUID, INT, INT, DATE, INT[], TEXT[], TEXT[], INT[], INT);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_pendientes(
    p_user_guid       UUID,
    p_company_id      INT,
    p_pais_id         INT,
    p_hoy             DATE,
    -- Los scope_* van ANTES del horizonte: en Postgres, después de un parámetro con
    -- DEFAULT todos los siguientes deben tenerlo, y estos no lo llevan a propósito.
    p_scope_farm_ids  INT[],
    p_scope_nucleos   TEXT[],
    p_scope_galpones  TEXT[],
    p_scope_lotes     INT[],
    p_dias_horizonte  INT DEFAULT 7
)
RETURNS TABLE (
    cronograma_item_id     INT,
    linea_productiva       TEXT,
    lote_id                INT,
    lote_nombre            TEXT,
    granja_id              INT,
    granja_nombre          TEXT,
    nucleo_id              TEXT,
    galpon_id              TEXT,
    item_inventario_id     INT,
    item_inventario_nombre TEXT,
    unidad_objetivo        TEXT,
    valor_objetivo         INT,
    fecha_inicio_franja    DATE,
    fecha_fin_franja       DATE,
    situacion              TEXT,
    dias                   INT
)
LANGUAGE sql
STABLE
AS $$
WITH granjas AS (
    -- COPIA del alcance de fn_vacunacion_filter_data (ver nota de W4 en el encabezado).
    SELECT f.id, f.name
    FROM public.farms f
    WHERE f.company_id = p_company_id
      AND f.deleted_at IS NULL
      AND EXISTS (SELECT 1 FROM public.user_farms uf
                  WHERE uf.farm_id = f.id AND uf.user_id = p_user_guid)
      AND (p_pais_id IS NULL OR EXISTS (
            SELECT 1 FROM public.departamentos d
            WHERE d.departamento_id = f.departamento_id AND d.pais_id = p_pais_id))
),
lotes_ubicados AS (
    SELECT l.lote_postura_levante_id AS lote_id, 'Levante'::text AS linea,
           l.lote_nombre, l.fecha_encaset::date AS fecha_encaset,
           l.granja_id, l.nucleo_id, l.galpon_id,
           l.lote_id AS lote_tabla_id
    FROM public.lote_postura_levante l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_levante_id IS NOT NULL
      AND l.estado_cierre IS DISTINCT FROM 'Cerrado'

    UNION ALL

    SELECT l.lote_postura_produccion_id, 'Produccion'::text,
           l.lote_nombre, l.fecha_encaset::date,
           l.granja_id, l.nucleo_id, l.galpon_id,
           l.lote_id
    FROM public.lote_postura_produccion l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_produccion_id IS NOT NULL
      AND l.estado_cierre IS DISTINCT FROM 'Cerrado'

    UNION ALL

    -- Engorde no tiene FK a la tabla `lotes` ⇒ galpón/núcleo lo gobiernan.
    SELECT l.lote_ave_engorde_id, 'Engorde'::text,
           l.lote_nombre, l.fecha_encaset::date,
           l.granja_id, l.nucleo_id, l.galpon_id,
           NULL::int
    FROM public.lote_ave_engorde l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_ave_engorde_id IS NOT NULL
      AND l.estado_operativo_lote IS DISTINCT FROM 'Cerrado'
),
lotes AS (
    -- Alcance granular. Espejo de UserLocationScopeCalculos.PermiteUbicacion (su dueña) y
    -- de fn_vacunacion_filter_data: las dos funciones filtran con la MISMA expresión.
    SELECT u.* FROM lotes_ubicados u
    WHERE NOT (u.granja_id = ANY(COALESCE(p_scope_farm_ids, '{}'::int[])))
       OR CASE
            WHEN u.lote_tabla_id IS NOT NULL
                THEN u.lote_tabla_id = ANY(COALESCE(p_scope_lotes, '{}'::int[]))
            WHEN COALESCE(u.galpon_id, '') <> ''
                THEN u.galpon_id = ANY(COALESCE(p_scope_galpones, '{}'::text[]))
            WHEN COALESCE(u.nucleo_id, '') <> ''
                THEN (u.granja_id::text || '|' || u.nucleo_id) = ANY(COALESCE(p_scope_nucleos, '{}'::text[]))
            ELSE false
          END
),
base AS (
    SELECT
        ci.id AS cronograma_item_id,
        ci.linea_productiva,
        lo.lote_id, lo.lote_nombre, lo.fecha_encaset,
        lo.granja_id, lo.nucleo_id, lo.galpon_id,
        ci.item_inventario_id, ci.unidad_objetivo, ci.valor_objetivo, ci.fecha_objetivo,
        ci.rango_dias_antes, ci.rango_dias_despues, ci.orden
    FROM public.vacunacion_cronograma_item ci
    JOIN lotes lo
      ON lo.linea = ci.linea_productiva
     AND lo.lote_id = COALESCE(ci.lote_postura_levante_id,
                               ci.lote_postura_produccion_id,
                               ci.lote_ave_engorde_id)
    LEFT JOIN public.vacunacion_registro_aplicacion ra
      ON ra.vacunacion_cronograma_item_id = ci.id
    WHERE ci.company_id = p_company_id
      AND ci.deleted_at IS NULL
      AND ci.activo = true
      AND (ra.id IS NULL OR ra.estado = 'Pendiente')
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
),
clasificado AS (
    SELECT f.*,
        (f.fecha_base - f.rango_dias_antes)   AS inicio,
        (f.fecha_base + f.rango_dias_despues) AS fin
    FROM franja f
    WHERE f.fecha_base IS NOT NULL
)
SELECT
    c.cronograma_item_id,
    c.linea_productiva,
    c.lote_id,
    COALESCE(c.lote_nombre, '')  AS lote_nombre,
    c.granja_id,
    g.name                       AS granja_nombre,
    c.nucleo_id,
    c.galpon_id,
    c.item_inventario_id,
    COALESCE(ii.nombre, '')      AS item_inventario_nombre,
    c.unidad_objetivo,
    c.valor_objetivo,
    c.inicio                     AS fecha_inicio_franja,
    c.fin                        AS fecha_fin_franja,
    CASE WHEN c.fin < p_hoy    THEN 'Vencido'
         WHEN c.inicio <= p_hoy THEN 'EnFranja'
         ELSE 'Proximo'
    END                          AS situacion,
    CASE WHEN c.fin < p_hoy     THEN (p_hoy - c.fin)
         WHEN c.inicio <= p_hoy THEN 0
         ELSE -(c.inicio - p_hoy)
    END                          AS dias
FROM clasificado c
LEFT JOIN granjas g ON g.id = c.granja_id
LEFT JOIN public.item_inventario_ecuador ii ON ii.id = c.item_inventario_id
-- El horizonte recorta SÓLO lo que viene: un vencido de hace un año sigue siendo pendiente.
WHERE c.fin < p_hoy
   OR c.inicio <= p_hoy
   OR (c.inicio - p_hoy) <= GREATEST(COALESCE(p_dias_horizonte, 0), 0)
ORDER BY
    CASE WHEN c.fin < p_hoy THEN 0 WHEN c.inicio <= p_hoy THEN 1 ELSE 2 END,
    CASE WHEN c.fin < p_hoy THEN (p_hoy - c.fin) ELSE 0 END DESC,
    c.inicio,
    c.orden,
    c.cronograma_item_id;
$$;
