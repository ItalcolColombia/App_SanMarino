-- verificar_nombre_lote_engorde_sin_sufijo_corrida.sql
-- SOLO LECTURA. Mide lo que corrige la migración 20260914153000_NombreLoteEngordeSinSufijoCorrida:
-- lotes de engorde de empresas SIN sufijo de corrida (companies.nombre_lote_incluye_corrida = false,
-- Ecuador) que quedaron con nombre "{base} - {corrida}" por el sufijo automático.
-- Antes de la migración: lista los candidatos. Después: las dos consultas deben dar 0 filas.

-- 1) Lotes vivos con sufijo automático (y qué nombre/corrida les toca).
WITH vivos AS (
    SELECT l.lote_ave_engorde_id,
           c.name AS empresa,
           f.name AS granja,
           g.galpon_nombre,
           l.galpon_id,
           l.company_id,
           l.lote_nombre,
           l.numero_corrida,
           btrim(b.nombre) AS base_nombre,
           l.estado_operativo_lote,
           ROW_NUMBER() OVER (
               PARTITION BY l.company_id, l.lote_base_engorde_id, l.galpon_id
               ORDER BY l.fecha_encaset NULLS LAST, l.created_at, l.lote_ave_engorde_id
           ) AS corrida_viva
      FROM lote_ave_engorde l
      JOIN companies c ON c.id = l.company_id AND c.nombre_lote_incluye_corrida = false
      JOIN lote_base_engorde b ON b.id = l.lote_base_engorde_id
      JOIN farms f ON f.id = l.granja_id
      LEFT JOIN galpones g ON g.galpon_id = l.galpon_id
     WHERE l.deleted_at IS NULL
       AND l.galpon_id IS NOT NULL
       AND l.numero_corrida IS NOT NULL
)
SELECT v.lote_ave_engorde_id, v.empresa, v.granja, v.galpon_nombre, v.lote_nombre, v.numero_corrida,
       v.base_nombre AS nombre_nuevo, v.corrida_viva AS corrida_nueva, v.estado_operativo_lote,
       EXISTS (SELECT 1 FROM lote_ave_engorde o
                WHERE o.company_id = v.company_id AND o.galpon_id = v.galpon_id AND o.deleted_at IS NULL
                  AND o.lote_ave_engorde_id <> v.lote_ave_engorde_id
                  AND btrim(o.lote_nombre) = v.base_nombre) AS se_salta_por_homonimo
  FROM vivos v
 WHERE v.base_nombre <> ''
   AND v.lote_nombre = v.base_nombre || ' - ' || v.numero_corrida
 ORDER BY v.empresa, v.granja, v.galpon_nombre;

-- 2) Referencias de gastos de inventario que todavía llevan el nombre con sufijo de esos lotes.
SELECT 'inventario_gestion_movimiento' AS tabla, m.id, m.reference AS texto
  FROM inventario_gestion_movimiento m
  JOIN inventario_gasto ga ON left(m.reference, length('Gasto inventario #' || ga.id || ' ')) = 'Gasto inventario #' || ga.id || ' '
  JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = ga.lote_ave_engorde_id AND l.deleted_at IS NULL
  JOIN companies c ON c.id = l.company_id AND c.nombre_lote_incluye_corrida = false
  JOIN lote_base_engorde b ON b.id = l.lote_base_engorde_id
 WHERE m.reference LIKE '% · Lote ' || btrim(b.nombre) || ' - %'
UNION ALL
SELECT 'lote_registro_historico_unificado', h.id, h.referencia
  FROM lote_registro_historico_unificado h
  JOIN inventario_gasto ga ON left(h.referencia, length('Gasto inventario #' || ga.id || ' ')) = 'Gasto inventario #' || ga.id || ' '
  JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = ga.lote_ave_engorde_id AND l.deleted_at IS NULL
  JOIN companies c ON c.id = l.company_id AND c.nombre_lote_incluye_corrida = false
  JOIN lote_base_engorde b ON b.id = l.lote_base_engorde_id
 WHERE h.referencia LIKE '% · Lote ' || btrim(b.nombre) || ' - %'
 ORDER BY 1, 2;
