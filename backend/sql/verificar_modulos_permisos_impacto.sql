-- =============================================================================
-- verificar_modulos_permisos_impacto.sql — SOLO LECTURA sobre los datos del negocio.
--
-- Mide qué permisos EFECTIVOS pierde o gana cada usuario al introducir los módulos de permisos
-- (plan: fase_de_desarrollo/modulos_permisos_por_empresa_plan.md, migración SeedModulosDePermisos).
--
-- Uso (patrón «1ª vez congela, 2ª compara»):
--   psql ... -f backend/sql/verificar_modulos_permisos_impacto.sql   -- ANTES de aplicar: congela la línea base
--   (aplicar migraciones / levantar el backend)
--   psql ... -f backend/sql/verificar_modulos_permisos_impacto.sql   -- DESPUÉS: compara contra la línea base
--
-- Permiso efectivo = role_permissions ∩ company_permissions(is_enabled) por par (user_roles.user_id,
-- user_roles.company_id) — la misma intersección que AuthService.PermisosEfectivosAsync.
--
-- Resultado esperado tras SeedModulosDePermisos (medido 12-sep-2026 sobre la copia de prod):
--   PIERDEN = sólo keys de pantallas que la empresa no tiene (engorde/Panamá/vacunación en postura).
--   GANAN   = 0 filas (regla S1: la siembra no resucita asignaciones huérfanas).
--
-- La tabla _verif_modulos_permisos_base es el único objeto que crea; borrarla reinicia la medición:
--   DROP TABLE IF EXISTS public._verif_modulos_permisos_base;
-- =============================================================================

CREATE TABLE IF NOT EXISTS public._verif_modulos_permisos_base (
    user_id       uuid    NOT NULL,
    company_id    integer NOT NULL,
    permission_key text   NOT NULL,
    congelado_en  timestamptz NOT NULL DEFAULT now()
);

-- 1ª corrida: congela (sólo si la tabla está vacía).
INSERT INTO public._verif_modulos_permisos_base (user_id, company_id, permission_key)
SELECT DISTINCT ur.user_id, ur.company_id, p.key
  FROM public.user_roles ur
  JOIN public.role_permissions rp    ON rp.role_id = ur.role_id
  JOIN public.company_permissions cp ON cp.company_id = ur.company_id
                                    AND cp.permission_id = rp.permission_id
                                    AND cp.is_enabled
  JOIN public.permissions p          ON p.id = rp.permission_id
 WHERE NOT EXISTS (SELECT 1 FROM public._verif_modulos_permisos_base);

WITH actual AS (
    SELECT DISTINCT ur.user_id, ur.company_id, p.key AS permission_key
      FROM public.user_roles ur
      JOIN public.role_permissions rp    ON rp.role_id = ur.role_id
      JOIN public.company_permissions cp ON cp.company_id = ur.company_id
                                        AND cp.permission_id = rp.permission_id
                                        AND cp.is_enabled
      JOIN public.permissions p          ON p.id = rp.permission_id
),
base AS (SELECT user_id, company_id, permission_key FROM public._verif_modulos_permisos_base)
SELECT 'PIERDEN' AS cambio, c.name AS empresa, b.permission_key, count(DISTINCT b.user_id) AS usuarios
  FROM base b
  JOIN public.companies c ON c.id = b.company_id
 WHERE NOT EXISTS (SELECT 1 FROM actual a
                    WHERE a.user_id = b.user_id AND a.company_id = b.company_id
                      AND a.permission_key = b.permission_key)
 GROUP BY c.name, b.permission_key
UNION ALL
SELECT 'GANAN', c.name, a.permission_key, count(DISTINCT a.user_id)
  FROM actual a
  JOIN public.companies c ON c.id = a.company_id
 WHERE NOT EXISTS (SELECT 1 FROM base b
                    WHERE a.user_id = b.user_id AND a.company_id = b.company_id
                      AND a.permission_key = b.permission_key)
 GROUP BY c.name, a.permission_key
ORDER BY 1, 2, 3;

-- Después de la migración, el estado por módulo se mira con (no va arriba: antes de
-- AddModulosDePermisos las tablas no existen y la consulta cortaría el script):
--
-- SELECT c.name AS empresa, pm.key AS modulo, cpm.is_enabled,
--        count(pmp.permission_id) AS permisos_del_modulo,
--        count(cp.permission_id) FILTER (WHERE cp.is_enabled) AS habilitados_en_empresa
--   FROM public.company_permission_modules cpm
--   JOIN public.companies c           ON c.id = cpm.company_id
--   JOIN public.permission_modules pm ON pm.id = cpm.module_id
--   LEFT JOIN public.permission_module_permissions pmp ON pmp.module_id = pm.id
--   LEFT JOIN public.company_permissions cp ON cp.company_id = cpm.company_id AND cp.permission_id = pmp.permission_id
--  GROUP BY c.name, pm.key, pm.orden, cpm.is_enabled
--  ORDER BY c.name, pm.orden;
