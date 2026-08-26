-- =============================================================================
-- INVARIANTE: ningun registro origen_cruce puede quedar SIN VALIDAR.
--
-- POR QUE EXISTE
-- Este bug nacio de un INSERT que omitio en silencio una columna con DEFAULT:
-- `fn_cruce_reproductora_a_engorde` insertaba los dias 1-7 de pollo engorde sin nombrar
-- `validado` (DEFAULT false), mientras el C# documentaba que "nacen validados". Cuando la
-- reproductora se confirma tarde, esos registros nacen con fecha pasada y, con un plazo de
-- 1 dia, nacen YA VENCIDOS => bloquean el alta de dias nuevos del lote y NADIE puede
-- destrabarlo, porque son de solo lectura en la UI.
--
-- Medido el 25-ago-2026 sobre la copia de produccion: 28 registros asi, en 4 lotes de
-- DAYLAND (215, 216, 224, 225), dos de ellos creados ese mismo dia.
--
-- 🔴 EL CUERPO DE ESA FUNCION ESTA COPIADO EN 5 MIGRACIONES. La proxima que lo reescriba
-- desde una copia vieja reintroduce el defecto exactamente igual, y en silencio. Esta
-- verificacion es la unica red: no hay test de C# que pueda ver un INSERT de plpgsql.
--
-- USO
--   psql ... -f backend/sql/verificar_cruce_nace_validado.sql
--
-- COMO SE LEE
--   Los tres chequeos tienen que decir OK. Cualquier otra cosa se investiga antes de mergear.
--
-- Plan: fase_de_desarrollo/cruce_reproductora_nace_sin_validar_plan.md
-- =============================================================================
\timing off

\echo ''
\echo '=== 1. La funcion desplegada escribe `validado`? (asi nacio el bug: no lo hacia) ==='
SELECT CASE
         WHEN prosrc LIKE '%validado%' THEN 'OK — la fn nombra la columna validado'
         ELSE '*** FALLA: la fn NO escribe validado. Los dias de cruce van a nacer PENDIENTES ***'
       END AS chequeo_1
FROM pg_proc WHERE proname = 'fn_cruce_reproductora_a_engorde';

\echo ''
\echo '=== 2. Hay registros de cruce sin validar? (tiene que dar 0) ==='
SELECT CASE
         WHEN COUNT(*) = 0 THEN 'OK — 0 registros origen_cruce sin validar'
         ELSE '*** FALLA: ' || COUNT(*) || ' registros origen_cruce sin validar ***'
       END AS chequeo_2
FROM seguimiento_diario_aves_engorde
WHERE origen_cruce AND NOT validado;

\echo ''
\echo '--- detalle, si el chequeo 2 fallo ---'
SELECT c.name AS empresa, f.name AS granja, g.galpon_nombre AS galpon,
       s.lote_ave_engorde_id AS lote, l.lote_nombre,
       COUNT(*) AS registros,
       MIN(s.fecha)::date AS desde, MAX(s.fecha)::date AS hasta,
       MIN(s.created_at)::date AS creados,
       COUNT(*) FILTER (WHERE CURRENT_DATE > s.fecha::date + 1) AS ya_vencidos
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
JOIN companies c ON c.id = l.company_id
JOIN farms f ON f.id = l.granja_id
LEFT JOIN galpones g ON g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
WHERE s.origen_cruce AND NOT s.validado
GROUP BY 1,2,3,4,5
ORDER BY 6 DESC;

\echo ''
\echo '=== 3. Que lotes estan BLOQUEADOS por vencidos, y por que ==='
\echo '    (los de cruce son un defecto; los propios son trabajo pendiente del operario)'
SELECT c.name AS empresa, f.name AS granja, g.galpon_nombre AS galpon,
       l.lote_ave_engorde_id AS lote, l.lote_nombre,
       COUNT(*) FILTER (WHERE NOT s.validado AND CURRENT_DATE > s.fecha::date + 1) AS vencidos,
       COUNT(*) FILTER (WHERE NOT s.validado AND CURRENT_DATE > s.fecha::date + 1 AND s.origen_cruce) AS de_cruce_DEFECTO,
       COUNT(*) FILTER (WHERE NOT s.validado AND CURRENT_DATE > s.fecha::date + 1 AND NOT s.origen_cruce) AS propios_a_validar
FROM seguimiento_diario_aves_engorde s
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
JOIN companies c ON c.id = l.company_id AND c.requiere_validacion_seguimiento_diario
JOIN farms f ON f.id = l.granja_id
LEFT JOIN galpones g ON g.galpon_id = l.galpon_id AND g.granja_id = l.granja_id
GROUP BY 1,2,3,4,5
HAVING COUNT(*) FILTER (WHERE NOT s.validado AND CURRENT_DATE > s.fecha::date + 1) > 0
ORDER BY 6 DESC;
\echo '    (solo se listan empresas con la doble validacion ENCENDIDA: en las demas nadie lee validado)'

\echo ''
\echo '=== 4. Aguas arriba: reproductoras sin confirmar que van a disparar el cruce con fecha vieja ==='
\echo '    (no es un defecto del codigo: es el origen operativo del caso. Al confirmarlas, con el'
\echo '     arreglo puesto los dias nacen validados y el lote NO se traba)'
SELECT f.name AS granja, r.lote_ave_engorde_id AS lote_engorde, r.nombre_lote AS lote_reproductora,
       COUNT(*) AS dias_sin_confirmar,
       MIN(s.fecha)::date AS desde, MAX(s.fecha)::date AS hasta,
       CURRENT_DATE - MIN(s.fecha)::date AS antiguedad_dias
FROM seguimiento_diario_lote_reproductora_aves_engorde s
JOIN lote_reproductora_ave_engorde r ON r.id = s.lote_reproductora_ave_engorde_id
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = r.lote_ave_engorde_id
JOIN companies c ON c.id = l.company_id AND c.requiere_validacion_seguimiento_diario
JOIN farms f ON f.id = l.granja_id
WHERE NOT s.confirmado
GROUP BY 1,2,3
ORDER BY 7 DESC;
