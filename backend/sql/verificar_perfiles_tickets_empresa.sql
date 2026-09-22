-- backend/sql/verificar_perfiles_tickets_empresa.sql
-- Diagnóstico de SOLO LECTURA de la configuración de tickets: abrir vs atender, empresa y alcance.
-- Se corre igual antes y después de un cambio: la primera pasada congela la línea base, la segunda
-- compara. NO modifica nada.
--
-- Qué mide y por qué (19-sep-2026, plan fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md):
--   [1] Perfiles de apertura guardados en una empresa a la que el usuario NO pertenece. Es el defecto
--       que dejó a Lenin (Santa Reyes) con «Implementador» escrito en Sanmarino: en su empresa seguía
--       NORMAL y no le aparecía ningún tipo. Tiene que dar 0 filas.
--   [2] Plantillas de atención de un rol guardadas en una empresa que no es del rol: hacen que los
--       usuarios de una empresa aparezcan como resolutores en los tickets de otra (caso rol Costos de
--       Panamá con una fila en Sanmarino). Solo el rol administrador de la aplicación puede tenerlas a
--       propósito, y desde el 19-sep-2026 eso se expresa con alcance GLOBAL.
--   [3] Resolutores directos de un usuario en una empresa a la que no pertenece (los crea el admin
--       global a propósito; se listan para revisarlos, no son un error).
--   [4] Filas GLOBAL vigentes: quién atiende TODAS las empresas. Debería ser una lista corta y conocida.
--   [5] Copias por empresa redundantes: la misma (rol, tipo, país) activa como EMPRESA existiendo una
--       fila GLOBAL. Tiene que dar 0.
--   [6] USUARIOS MUDOS: por empresa, cuántos usuarios activos con permiso de tickets no pueden abrir
--       NINGÚN tipo (su nivel solo habilita tipos que nadie atiende en esa empresa). Es la medida que
--       explica el «hay que habilitar siempre a cada persona».
--
-- SIN-MIGRACION: diagnóstico de solo lectura; no crea ni modifica objetos.

\echo === [1] Perfiles de apertura ACTIVOS fuera de la empresa del usuario (debe dar 0 filas)
SELECT tp.id, u.first_name || ' ' || u.sur_name AS usuario, tp.company_id AS guardado_en,
       c.name AS empresa_guardada, tp.nivel, tp.activo,
       (SELECT string_agg(uc.company_id::text, ',' ORDER BY uc.company_id)
          FROM user_companies uc WHERE uc.user_id = tp.user_id) AS empresas_del_usuario
  FROM ticket_perfil_usuario tp
  JOIN users u ON u.id = tp.user_id
  LEFT JOIN companies c ON c.id = tp.company_id
 WHERE tp.activo
   AND NOT EXISTS (SELECT 1 FROM user_companies uc
                    WHERE uc.user_id = tp.user_id AND uc.company_id = tp.company_id)
 ORDER BY usuario;

\echo --- [1b] Lo mismo pero ya APAGADO (auditoria de lo corregido: informativo, no es un problema)
SELECT count(*) AS perfiles_apagados_fuera_de_su_empresa
  FROM ticket_perfil_usuario tp
 WHERE NOT tp.activo
   AND NOT EXISTS (SELECT 1 FROM user_companies uc
                    WHERE uc.user_id = tp.user_id AND uc.company_id = tp.company_id);

\echo === [2] Plantillas de rol ACTIVAS fuera de la empresa del rol (debe dar 0 con esperado=false)
\echo ---     esperado=true: el rol administrador de la aplicacion atendiendo otra empresa, puesto a proposito por el admin global
SELECT rr.id, rr.role_id, r.name AS rol, rr.tipo, rr.pais_id, rr.company_id AS guardada_en,
       c.name AS empresa_guardada, rr.alcance,
       (lower(btrim(r.name)) IN ('admin','administrador')) AS esperado,
       (SELECT string_agg(rc.company_id::text, ',' ORDER BY rc.company_id)
          FROM role_companies rc WHERE rc.role_id = rr.role_id) AS empresas_del_rol,
       (SELECT count(*) FROM user_roles ur WHERE ur.role_id = rr.role_id) AS usuarios_con_el_rol
  FROM ticket_resolutor_rol rr
  JOIN roles r ON r.id = rr.role_id
  LEFT JOIN companies c ON c.id = rr.company_id
 WHERE rr.activo AND rr.alcance <> 'GLOBAL'
   AND EXISTS (SELECT 1 FROM role_companies rc WHERE rc.role_id = rr.role_id)
   AND NOT EXISTS (SELECT 1 FROM role_companies rc
                    WHERE rc.role_id = rr.role_id AND rc.company_id = rr.company_id)
 ORDER BY esperado, rol, rr.tipo;

\echo === [3] Resolutores directos en una empresa ajena al usuario (informativo: los pone el admin global)
SELECT r.id, u.first_name || ' ' || u.sur_name AS usuario, r.tipo, r.pais_id,
       r.company_id, c.name AS empresa, r.alcance, r.activo
  FROM ticket_resolutores r
  JOIN users u ON u.id = r.user_id
  LEFT JOIN companies c ON c.id = r.company_id
 WHERE r.activo AND r.alcance <> 'GLOBAL'
   AND NOT EXISTS (SELECT 1 FROM user_companies uc
                    WHERE uc.user_id = r.user_id AND uc.company_id = r.company_id)
 ORDER BY usuario, r.tipo;

\echo === [4] Quien atiende TODAS las empresas (alcance GLOBAL vigente)
SELECT 'rol' AS origen, r.name AS quien, rr.tipo, rr.pais_id, rr.company_id AS configurada_desde,
       (SELECT string_agg(u.first_name || ' ' || u.sur_name, ', ')
          FROM user_roles ur JOIN users u ON u.id = ur.user_id WHERE ur.role_id = rr.role_id) AS alcanza_a
  FROM ticket_resolutor_rol rr JOIN roles r ON r.id = rr.role_id
 WHERE rr.activo AND rr.alcance = 'GLOBAL'
UNION ALL
SELECT 'usuario', u.first_name || ' ' || u.sur_name, d.tipo, d.pais_id, d.company_id, NULL
  FROM ticket_resolutores d JOIN users u ON u.id = d.user_id
 WHERE d.activo AND d.alcance = 'GLOBAL'
 ORDER BY 1, 2, 3;

\echo === [5] Copias por empresa que una fila GLOBAL ya cubre (debe dar 0)
SELECT rr.id, r.name AS rol, rr.tipo, rr.pais_id, rr.company_id
  FROM ticket_resolutor_rol rr JOIN roles r ON r.id = rr.role_id
 WHERE rr.activo AND rr.alcance = 'EMPRESA'
   AND EXISTS (SELECT 1 FROM ticket_resolutor_rol g
                WHERE g.role_id = rr.role_id AND g.tipo = rr.tipo
                  AND g.pais_id IS NOT DISTINCT FROM rr.pais_id
                  AND g.activo AND g.alcance = 'GLOBAL')
 ORDER BY rol, rr.tipo;

\echo === [6] Usuarios que NO pueden abrir ningun tipo, por empresa (el "habilitar siempre")
WITH uc AS (
    SELECT uc.user_id, uc.company_id,
           (SELECT min(cp.pais_id) FROM company_pais cp WHERE cp.company_id = uc.company_id) AS pais_id
      FROM user_companies uc JOIN users u ON u.id = uc.user_id AND u.is_active),
permisos AS (
    SELECT uc.user_id, uc.company_id, array_agg(DISTINCT p.key::text) AS keys
      FROM uc
      JOIN user_roles ur ON ur.user_id = uc.user_id
      JOIN role_permissions rp ON rp.role_id = ur.role_id
      JOIN permissions p ON p.id = rp.permission_id AND p.key LIKE 'tickets%'
      JOIN company_permissions cpp ON cpp.company_id = uc.company_id
                                  AND cpp.permission_id = p.id AND cpp.is_enabled
     GROUP BY 1, 2),
nivel AS (
    SELECT uc.user_id, uc.company_id, uc.pais_id, COALESCE(pm.keys, '{}'::text[]) AS keys,
           CASE WHEN COALESCE(pm.keys, '{}'::text[]) && ARRAY['tickets.gestionar','tickets.admin']
                  THEN 'IMPLEMENTADOR'
                WHEN EXISTS (SELECT 1 FROM user_roles ur JOIN roles r ON r.id = ur.role_id
                              WHERE ur.user_id = uc.user_id
                                AND upper(btrim(COALESCE(r.ticket_nivel_creacion, ''))) = 'IMPLEMENTADOR')
                  THEN 'IMPLEMENTADOR'
                WHEN tp.nivel IS NOT NULL THEN upper(tp.nivel)
                ELSE 'NORMAL' END AS nivel
      FROM uc
      LEFT JOIN permisos pm ON pm.user_id = uc.user_id AND pm.company_id = uc.company_id
      LEFT JOIN ticket_perfil_usuario tp ON tp.user_id = uc.user_id
                                        AND tp.company_id = uc.company_id AND tp.activo),
atendidos AS (
    SELECT DISTINCT e.company_id, t.tipo
      FROM (SELECT DISTINCT company_id, pais_id FROM uc) e
      CROSS JOIN unnest(ARRAY['SOPORTE','DUDAS','DESARROLLO','REQUERIMIENTO']) AS t(tipo)
     WHERE EXISTS (SELECT 1 FROM ticket_resolutores r
                    WHERE r.activo AND r.tipo = t.tipo
                      AND (r.alcance = 'GLOBAL' OR r.company_id = e.company_id)
                      AND (r.pais_id IS NULL OR r.pais_id = e.pais_id))
        OR EXISTS (SELECT 1 FROM ticket_resolutor_rol rr
                    JOIN user_roles ur ON ur.role_id = rr.role_id
                    WHERE rr.activo AND rr.tipo = t.tipo
                      AND (rr.alcance = 'GLOBAL' OR rr.company_id = e.company_id)
                      AND (rr.pais_id IS NULL OR rr.pais_id = e.pais_id)))
SELECT n.company_id, c.name AS empresa, count(*) AS usuarios,
       count(*) FILTER (WHERE 'tickets.crear' = ANY(n.keys)
                           OR n.keys && ARRAY['tickets.gestionar','tickets.admin']) AS con_permiso_tickets,
       count(*) FILTER (WHERE (SELECT count(*) FROM atendidos a
                                WHERE a.company_id = n.company_id
                                  AND a.tipo = ANY(CASE WHEN n.nivel = 'IMPLEMENTADOR'
                                                        THEN ARRAY['SOPORTE','DUDAS','DESARROLLO','REQUERIMIENTO']
                                                        ELSE ARRAY['SOPORTE','DUDAS'] END)) = 0) AS mudos,
       (SELECT string_agg(a.tipo, ',' ORDER BY a.tipo) FROM atendidos a
         WHERE a.company_id = n.company_id) AS tipos_con_quien_atienda
  FROM nivel n JOIN companies c ON c.id = n.company_id
 GROUP BY 1, 2
 ORDER BY 1;
