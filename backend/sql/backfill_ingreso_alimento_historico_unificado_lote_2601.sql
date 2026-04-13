-- =============================================================================
-- Backfill: ingresos de alimento (INV_INGRESO) → public.lote_registro_historico_unificado
-- Lote negocio 2601, galpones/fechas/cantidades de la planilla.
--
-- ENTIDAD LoteRegistroHistoricoUnificado (C#) ↔ columnas (lo que rellenamos):
--   CompanyId              → company_id              (desde lote_ave_engorde)
--   LoteAveEngordeId       → lote_ave_engorde_id     (desde join lote; obligatorio para el API)
--   FarmId                 → farm_id                 (= granja_id)
--   NucleoId               → nucleo_id
--   GalponId               → galpon_id
--   FechaOperacion         → fecha_operacion         (DATE)
--   TipoEvento             → tipo_evento             ('INV_INGRESO')
--   OrigenTabla            → origen_tabla            (UNIQUE junto con OrigenId)
--   OrigenId               → origen_id               (integer 1..N; NO usar millones*100)
--   MovementTypeOriginal   → movement_type_original  ('Ingreso')
--   ItemInventarioEcuadorId→ item_inventario_ecuador_id (opcional, si existe SM0175)
--   ItemResumen            → item_resumen
--   CantidadKg             → cantidad_kg
--   Unidad                 → unidad                  ('kg')
--   CantidadHembras/Machos/Mixtas → (no aplica ingreso kg; NULL)
--   Referencia             → referencia
--   NumeroDocumento        → numero_documento
--   AcumuladoEntradasAlimentoKg → acumulado_entradas_alimento_kg (NULL al insert; luego UPDATE)
--   Anulado                → anulado                 (FALSE)
--   CreatedAt              → created_at              (timestamptz UTC)
--
-- Si ves: "current transaction is aborted, commands ignored until end of transaction block"
--   → Un comando anterior en la MISMA sesión falló. Ejecuta:  ROLLBACK;
--   → Luego vuelve a correr este script (o solo el INSERT). Este archivo NO usa BEGIN/COMMIT
--     global para que un fallo no deje la sesión en estado abortado entre sentencias.
--
-- Servicio: GetHistoricoUnificadoPorLoteAsync (filtra por lote_ave_engorde_id + company_id).
-- =============================================================================

-- (Opcional) Desbloquear sesión si quedaste en transacción abortada:
-- ROLLBACK;

-- ---------------------------------------------------------------------------
-- 1) INSERT ingresos
-- ---------------------------------------------------------------------------
WITH
const AS (
  SELECT 'manual_backfill_ingreso_lote_2601'::text AS origen_tabla
),
datos (granja_id, nucleo_id, galpon_id, cantidad_kg, fecha_operacion) AS (
  VALUES
    (38, '963529',  'G0035', 15000::numeric, DATE '2026-02-10'),
    (38, '963529',  'G0036', 15000::numeric, DATE '2026-02-03'),
    (39, '464969',  'G0037',  7200::numeric, DATE '2026-01-16'),
    (39, '464969',  'G0038',  7200::numeric, DATE '2026-01-16'),
    (40, '723809',  'G0039',  6000::numeric, DATE '2026-02-13'),
    (40, '723809',  'G0040',  6000::numeric, DATE '2026-02-17'),
    (40, '723809',  'G0041',  6000::numeric, DATE '2026-02-24'),
    (40, '723809',  'G0042',  6000::numeric, DATE '2026-02-24'),
    (43, '351885',  'G0051',  7600::numeric, DATE '2026-01-06'),
    (43, '351885',  'G0052',  7600::numeric, DATE '2026-01-09'),
    (43, '351885',  'G0055',  7600::numeric, DATE '2026-01-13'),
    (42, '795634',  'G0047',  6000::numeric, DATE '2026-01-04'),
    (42, '795634',  'G0048',  6000::numeric, DATE '2026-01-04'),
    (42, '795634',  'G0049',  6000::numeric, DATE '2025-12-30'),
    (42, '795634',  'G0050',  7600::numeric, DATE '2025-12-27')
),
lotes_resueltos AS (
  SELECT
    d.*,
    l.lote_ave_engorde_id,
    l.company_id,
    ROW_NUMBER() OVER (
      PARTITION BY d.granja_id, d.nucleo_id, d.galpon_id
      ORDER BY l.lote_ave_engorde_id DESC
    ) AS rn
  FROM datos d
  JOIN public.lote_ave_engorde l
    ON l.granja_id = d.granja_id
   AND COALESCE(TRIM(l.nucleo_id), '') = COALESCE(TRIM(d.nucleo_id), '')
   AND COALESCE(TRIM(l.galpon_id), '') = COALESCE(TRIM(d.galpon_id), '')
   AND l.deleted_at IS NULL
   AND TRIM(l.lote_nombre) = '2601'
),
lotes_ok AS (
  SELECT * FROM lotes_resueltos WHERE rn = 1 AND lote_ave_engorde_id IS NOT NULL
),
ins AS (
  INSERT INTO public.lote_registro_historico_unificado (
    company_id,
    lote_ave_engorde_id,
    farm_id,
    nucleo_id,
    galpon_id,
    fecha_operacion,
    tipo_evento,
    origen_tabla,
    origen_id,
    movement_type_original,
    item_inventario_ecuador_id,
    item_resumen,
    cantidad_kg,
    unidad,
    referencia,
    numero_documento,
    acumulado_entradas_alimento_kg,
    anulado,
    created_at
  )
  SELECT
    x.company_id,
    x.lote_ave_engorde_id,
    x.granja_id,
    x.nucleo_id,
    x.galpon_id,
    x.fecha_operacion,
    'INV_INGRESO',
    c.origen_tabla,
    ROW_NUMBER() OVER (ORDER BY x.granja_id, x.galpon_id, x.fecha_operacion)::integer,
    'Ingreso',
    ii.id,
    COALESCE(
      CONCAT(ii.codigo, ' — ', ii.nombre),
      'SM0175 — SM POLLITO PREINICIADOR'
    ),
    x.cantidad_kg,
    'kg',
    'Backfill histórico ingreso alimento lote 2601 (pre-unificación)',
    'SM0175',
    NULL,
    FALSE,
    (NOW() AT TIME ZONE 'utc')
  FROM lotes_ok x
  CROSS JOIN const c
  LEFT JOIN LATERAL (
    SELECT i.id, i.codigo, i.nombre
    FROM public.item_inventario_ecuador i
    WHERE i.codigo = 'SM0175'
      AND i.company_id = x.company_id
    LIMIT 1
  ) ii ON TRUE
  ON CONFLICT (origen_tabla, origen_id) DO NOTHING
  RETURNING lote_ave_engorde_id
)
SELECT COUNT(*) AS filas_insertadas FROM ins;

-- ---------------------------------------------------------------------------
-- 2) Recalcular acumulado_entradas_alimento_kg en filas INV_INGRESO / TRASLADO_ENTRADA
--    de los lotes tocados por este backfill (misma referencia).
--    Ejecutar solo si el INSERT anterior fue OK.
-- ---------------------------------------------------------------------------
WITH afectados AS (
  SELECT DISTINCT h.lote_ave_engorde_id
  FROM public.lote_registro_historico_unificado h
  WHERE h.origen_tabla = 'manual_backfill_ingreso_lote_2601'
    AND h.referencia = 'Backfill histórico ingreso alimento lote 2601 (pre-unificación)'
),
sums AS (
  SELECT
    h.id,
    SUM(COALESCE(h.cantidad_kg, 0)) OVER (
      PARTITION BY h.lote_ave_engorde_id
      ORDER BY h.fecha_operacion, h.id
      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS acum
  FROM public.lote_registro_historico_unificado h
  INNER JOIN afectados a ON a.lote_ave_engorde_id = h.lote_ave_engorde_id
  WHERE NOT h.anulado
    AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA')
)
UPDATE public.lote_registro_historico_unificado t
SET acumulado_entradas_alimento_kg = s.acum
FROM sums s
WHERE t.id = s.id;

/*
-- Diagnóstico: lotes 2601 por ubicación
SELECT
  l.granja_id,
  l.nucleo_id,
  l.galpon_id,
  l.lote_nombre,
  l.lote_ave_engorde_id,
  l.company_id
FROM public.lote_ave_engorde l
WHERE TRIM(l.lote_nombre) = '2601'
  AND l.deleted_at IS NULL
  AND (
       (l.granja_id = 38 AND l.nucleo_id = '963529' AND l.galpon_id IN ('G0035','G0036'))
    OR (l.granja_id = 39 AND l.nucleo_id = '464969' AND l.galpon_id IN ('G0037','G0038'))
    OR (l.granja_id = 40 AND l.nucleo_id = '723809' AND l.galpon_id IN ('G0039','G0040','G0041','G0042'))
    OR (l.granja_id = 42 AND l.nucleo_id = '795634' AND l.galpon_id IN ('G0047','G0048','G0049','G0050'))
    OR (l.granja_id = 43 AND l.nucleo_id = '351885' AND l.galpon_id IN ('G0051','G0052','G0055'))
  )
ORDER BY l.granja_id, l.galpon_id, l.lote_ave_engorde_id DESC;
*/
