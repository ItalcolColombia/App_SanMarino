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
-- Sincronizada con la migración AddFnVacunacionConsultas — si se edita este archivo,
-- actualizar la migración también.
-- ============================================================================
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
    -- Case-insensitive: el dato real trae "Vacuna"/"vacuna" mezclado (paridad con EF ILike).
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
