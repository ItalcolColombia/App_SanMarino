-- =============================================================================
-- INVARIANTE de `company_permissions`: nadie pierde acceso por el seed.
--
-- POR QUÉ EXISTE
-- `company_permissions` (migración 20260812025725) volvió el catálogo de permisos algo POR EMPRESA:
-- un permiso solo cuenta si la empresa del rol que lo otorga lo tiene habilitado. El gate es
-- fail-closed, así que **una tabla mal sembrada deja usuarios sin permisos**, y el seed
-- (20260812030035) es lo delicado, no el gate: deriva la config de lo que cada empresa YA usa,
-- uniendo `role_companies` Y `user_roles.company_id` (con uno solo, alguien pierde acceso).
--
-- LO QUE HACE ESTE ARCHIVO
-- Compara, por usuario, los permisos efectivos ANTES del modelo nuevo (solo rol) contra los de
-- DESPUÉS (rol ∩ empresa habilitada). El diff **tiene que ser vacío**.
--
-- 🔑 NO hace falta correrlo antes del deploy. Apagar un permiso NO borra `role_permissions`: el
-- seed solo INSERTA en la tabla nueva. Por eso las dos mitades del diff se calculan en una sola
-- conexión, después de desplegar. Verificado: 49 usuarios locales, diff vacío.
--
-- USO — una sola corrida, después del deploy:
--
--     psql "<conn>" -f backend/sql/verificar_permisos_efectivos_company_permissions.sql
--
-- LECTURA DEL RESULTADO
--   Bloque 1 — 0 filas  ⇒ nadie perdió permisos. Es el que decide si el deploy quedó sano.
--   Bloque 2 — contexto: cuántos permisos habilitó el seed por empresa. Una empresa en 0 es la
--              señal de que el seed no la cubrió (o de que no tiene roles ni usuarios).
--
-- SI EL BLOQUE 1 TRAE FILAS: no se revierte el deploy. Se habilita el permiso que falta para esa
-- empresa (`UPDATE company_permissions SET is_enabled = true ...`, o el INSERT si no existe la
-- fila). La asignación en `role_permissions` sigue intacta — por eso el usuario la recupera sin
-- tocar roles.
-- =============================================================================

\echo ''
\echo '== 1) USUARIOS QUE PIERDEN PERMISOS (tiene que dar 0 filas) =================='

WITH antes AS (
    -- Modelo viejo: el permiso vale por venir del rol, sin mirar la empresa.
    SELECT ur.user_id, p.key AS permiso
      FROM user_roles ur
      JOIN role_permissions rp ON rp.role_id = ur.role_id
      JOIN permissions      p  ON p.id       = rp.permission_id
),
despues AS (
    -- Modelo nuevo: además, la EMPRESA DE ESE ROL tiene que tenerlo habilitado.
    -- El join va por `ur.company_id` —el par (rol, empresa)— y no por la empresa activa: un
    -- permiso sobrevive si viene de un rol cuya empresa lo habilita, aunque otro rol del mismo
    -- usuario esté en una empresa que no.
    SELECT ur.user_id, p.key AS permiso
      FROM user_roles ur
      JOIN role_permissions   rp ON rp.role_id       = ur.role_id
      JOIN permissions        p  ON p.id             = rp.permission_id
      JOIN company_permissions cp ON cp.company_id   = ur.company_id
                                 AND cp.permission_id = rp.permission_id
                                 AND cp.is_enabled
)
SELECT a.user_id,
       -- `users` no tiene email (el login vive en `login`); se identifica por nombre y cédula.
       u.first_name || ' ' || u.sur_name AS usuario,
       u.cedula,
       a.permiso AS permiso_perdido
  FROM (SELECT DISTINCT user_id, permiso FROM antes)   a
  LEFT JOIN (SELECT DISTINCT user_id, permiso FROM despues) d
         ON d.user_id = a.user_id AND d.permiso = a.permiso
  LEFT JOIN users u ON u.id = a.user_id
 WHERE d.permiso IS NULL
 ORDER BY a.user_id, a.permiso;

\echo ''
\echo '== 2) CONTEXTO: permisos habilitados por empresa (0 = el seed no la cubrió) =='

SELECT c.id            AS company_id,
       c.name          AS empresa,
       COUNT(*) FILTER (WHERE cp.is_enabled) AS habilitados,
       COUNT(*)                              AS filas_sembradas
  FROM companies c
  LEFT JOIN company_permissions cp ON cp.company_id = c.id
 GROUP BY c.id, c.name
 ORDER BY habilitados, c.id;

\echo ''
