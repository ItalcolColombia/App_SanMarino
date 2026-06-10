-- ============================================================================
-- DIAGNÓSTICO: ¿por qué un ticket aparece (o no) en cada bandeja?
-- Ejecutar contra la BD del entorno (local: sanmarinoapplocal).
-- ============================================================================

-- 1) Tickets con su creador y asignado (Guid + nombre + país/empresa)
SELECT t.id, t.codigo, t.tipo, t.estado, t.company_id, t.pais_id,
       t.created_by_user_id,
       cu.first_name || ' ' || cu.sur_name      AS creado_por,
       t.assigned_to_user_guid,
       au.first_name || ' ' || au.sur_name      AS asignado_a
FROM tickets t
LEFT JOIN users cu ON cu.id = t.created_by_user_guid
LEFT JOIN users au ON au.id = t.assigned_to_user_guid
WHERE t.deleted_at IS NULL
ORDER BY t.id;

-- 2) Perfiles de RESOLUTOR por usuario (lo que alimenta la Bandeja de gestión)
--    pais_id NULL = global (atiende ese tipo en TODOS los países).
SELECT r.user_id,
       u.first_name || ' ' || u.sur_name AS usuario,
       r.tipo, r.pais_id, r.company_id, r.activo
FROM ticket_resolutores r
LEFT JOIN users u ON u.id = r.user_id
ORDER BY u.first_name, r.tipo;

-- 3) Nivel del solicitante por usuario (lo que alimenta "Tipo de ticket" al crear)
SELECT p.user_id,
       u.first_name || ' ' || u.sur_name AS usuario,
       p.nivel, p.company_id, p.activo
FROM ticket_perfiles_usuario p
LEFT JOIN users u ON u.id = p.user_id
ORDER BY u.first_name;

-- 4) ¿La cédula coincide con el user_id del JWT? (para que el nombre del autor de
--    notas mapee). Reemplazá el valor por el user_id que veas en el token.
-- SELECT id, cedula, first_name, sur_name FROM users WHERE cedula = '496236603';

-- 5) Para un usuario resolutor concreto (reemplazá el Guid), qué tickets DEBERÍA
--    ver en su Bandeja de gestión según sus perfiles:
-- WITH perfiles AS (
--   SELECT tipo, pais_id FROM ticket_resolutores
--   WHERE user_id = '92afe4c8-bf3e-4ab0-a31a-467890463542' AND activo
-- )
-- SELECT t.id, t.codigo, t.tipo, t.pais_id
-- FROM tickets t
-- WHERE t.deleted_at IS NULL
--   AND EXISTS (
--     SELECT 1 FROM perfiles p
--     WHERE p.tipo = t.tipo AND (p.pais_id IS NULL OR p.pais_id = t.pais_id)
--   )
-- ORDER BY t.id;
