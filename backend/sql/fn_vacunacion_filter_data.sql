-- ============================================================================
-- fn_vacunacion_filter_data — Datos de los combos del módulo Vacunación en UN
-- solo round trip: granjas asignadas al usuario (lite), lotes de las 3 líneas,
-- vacunas del catálogo y usuarios de la empresa (para "aplicado por").
--
-- Reemplaza las 5+ consultas secuenciales de VacunacionCronogramaService.GetFilterDataAsync
-- (granjas FarmDto completo + 3 tablas de lote + vacunas). La BD filtra, el backend orquesta.
--
-- Claves jsonb en camelCase 1:1 con VacunacionFilterDataDto (el wrapper C# deserializa
-- vía VacunacionFilterDataJson.Parse — sincronizar si se cambia una clave).
--
-- El id de "usuarios" es la CÉDULA parseada a int: es el UserId entero del sistema
-- (mismo mapeo que TicketService.BuildNotaUserInfoAsync). Se excluyen cédulas no
-- numéricas o fuera de rango int4 para no reventar el cast.
--
-- ⚠ ALCANCE GRANULAR (W4): los 4 parámetros p_scope_* son el CIERRE de visibilidad del
--   usuario, ya calculado en C# por UserLocationScopeCalculos.ComputeScope y aplanado por
--   AplanarParaSql. Acá NO se recalcula nada: sólo se prueba pertenencia a conjuntos.
--   · p_scope_farm_ids  = granjas del usuario con restrict_locations = true.
--   · p_scope_nucleos   = claves COMPUESTAS 'granjaId|nucleoId' (nucleo_id se repite entre granjas).
--   · p_scope_galpones  = ids de galpón (PK global) · p_scope_lotes = lotes.lote_id permitidos.
--   Una granja en p_scope_farm_ids y ausente del resto ⇒ CERO lotes (fail-closed).
--   Los 4 parámetros son OBLIGATORIOS a propósito (sin DEFAULT): un llamador que se los
--   olvide debe fallar, no ver toda la empresa.
--   La regla del CASE espeja a UserLocationScopeCalculos.PermiteUbicacion, que es su dueña.
--   Va UNA sola vez, después del UNION: repetirla por rama la volvería a duplicar (y un
--   helper SQL aparte rompería la restauración de dumps, que ordena funciones por OID).
--
-- Sincronizada con las migraciones AddFnVacunacionConsultas y ScopingUbicacionVacunacionFns
-- — si se edita este archivo, actualizar la migración también.
-- ============================================================================
DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_vacunacion_filter_data(UUID, INT, INT, INT[], TEXT[], TEXT[], INT[]);

CREATE OR REPLACE FUNCTION public.fn_vacunacion_filter_data(
    p_user_guid       UUID,
    p_company_id      INT,
    p_pais_id         INT,
    p_scope_farm_ids  INT[],
    p_scope_nucleos   TEXT[],
    p_scope_galpones  TEXT[],
    p_scope_lotes     INT[]
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
lotes_ubicados AS (
    SELECT l.lote_postura_levante_id AS lote_id, 'Levante' AS linea_productiva,
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre AS estado_cierre,
           l.lote_id AS lote_tabla_id
    FROM public.lote_postura_levante l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_levante_id IS NOT NULL

    UNION ALL

    SELECT l.lote_postura_produccion_id, 'Produccion',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_cierre,
           l.lote_id
    FROM public.lote_postura_produccion l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_postura_produccion_id IS NOT NULL

    UNION ALL

    -- Engorde no tiene FK a la tabla `lotes` ⇒ se gobierna por galpón/núcleo (limitación
    -- conocida del alcance granular, espejada en UserLocationScopeCalculos.PermiteUbicacion).
    SELECT l.lote_ave_engorde_id, 'Engorde',
           l.lote_nombre, l.granja_id, l.nucleo_id, l.galpon_id, l.fecha_encaset,
           l.estado_operativo_lote,
           NULL::int
    FROM public.lote_ave_engorde l
    JOIN granjas g ON g.id = l.granja_id
    WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
      AND l.lote_ave_engorde_id IS NOT NULL
),
lotes AS (
    -- Alcance granular. Espejo de UserLocationScopeCalculos.PermiteUbicacion (su dueña).
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
vacunas AS (
    -- Case-insensitive: el dato real trae "Vacuna"/"vacuna" mezclado (paridad con EF ILike).
    SELECT i.id, i.codigo, i.nombre, i.unidad
    FROM public.item_inventario i
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
               -- El desempate por línea+id hace TOTAL el orden: sólo con fecha_encaset, dos lotes
               -- del mismo día salían en el orden que quisiera el plan y la lista cambiaba sola.
               ORDER BY l.fecha_encaset DESC NULLS LAST, l.linea_productiva, l.lote_id)
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
