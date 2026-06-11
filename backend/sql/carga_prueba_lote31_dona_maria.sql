-- ============================================================================
-- CARGA DE PRUEBA — Lote 31 «Doña María D-1» (fuente: Excel Libro1.xlsx)
-- ----------------------------------------------------------------------------
-- Crea sobre la granja EXISTENTE de Panamá (company 5 · granja 87 · núcleo
-- 612229 · galpón G0447):
--   1) 1 lote pollo engorde  (56,355 aves, encaset 2026-05-28, ROSS AP 2026)
--   2) 2 lotes reproductora  (34: H-34+M-34 · 32: H-32+M-32)
--   3) 7 días de seguimiento diario por lote reproductora (días 1–7)
--
-- El trigger trg_cruce_reproductora_engorde regenera automáticamente el
-- seguimiento consolidado del lote engorde (seguimiento_diario_aves_engorde,
-- origen_cruce = true) en cada INSERT — igual que cuando se digita en la app.
--
-- Conversión usada (la oficial de la app): 1 QQ = 45.36 kg.
-- Los QQ originales del Excel quedan documentados en `observaciones`.
--
-- ⚠️ SOLO PRUEBAS (BD local). DML puro, transaccional (todo o nada).
-- ⚠️ Este INSERT directo NO descuenta inventario de alimento (eso lo hace el
--    backend al crear vía API). Para validar inventario, digitar por el módulo.
-- ⚠️ Con 7/7 registros ambos lotes reproductora quedan CERRADOS (recogida
--    completa). Si prefiere dejarlos abiertos, comente las filas del día 7.
-- ============================================================================

BEGIN;
SET LOCAL TimeZone = 'UTC';  -- el cruce castea fecha::date; igualar a prod (UTC)

DO $$
DECLARE
  v_company_id  int         := 5;            -- ItalcolPanama
  v_granja_id   int         := 87;           -- panama
  v_nucleo_id   varchar     := '612229';     -- n1
  v_galpon_id   varchar     := 'G0447';      -- galpo 1
  v_user_id     int         := 1369984321;   -- admin panama
  v_user_str    varchar     := 'CARGA_EXCEL_LOTE31';
  v_fecha_enc   timestamptz := '2026-05-28 00:00:00+00';
  v_pais_id     int;
  v_pais_nombre varchar;
  v_lote_id     int;
  v_rep34_id    int;
  v_rep32_id    int;
BEGIN
  -- País Panamá del catálogo (mismo origen que usa el backend: tabla paises)
  SELECT p.pais_id, p.pais_nombre
    INTO v_pais_id, v_pais_nombre
    FROM paises p
   WHERE p.pais_nombre ILIKE 'panam%'
   LIMIT 1;

  -- Guard anti-duplicado: aborta si la prueba ya fue cargada
  IF EXISTS (SELECT 1 FROM lote_ave_engorde
              WHERE company_id = v_company_id
                AND lote_nombre = 'Lote 31'
                AND deleted_at IS NULL) THEN
    RAISE EXCEPTION 'Ya existe "Lote 31" en company %. Ejecute el bloque de LIMPIEZA (al final del archivo) y reintente.', v_company_id;
  END IF;

  -- ──────────────────────────────────────────────────────────────────────────
  -- 1) LOTE POLLO ENGORDE
  --    Hembras = H-34 + H-32 = 14,595 + 13,995 = 28,590
  --    Machos  = M-34 + M-32 = 14,096 + 13,669 = 27,765   → total 56,355
  --    Pesos llegada ponderados: H 39.30 g · M 39.60 g (Excel: prom 39.46)
  -- ──────────────────────────────────────────────────────────────────────────
  INSERT INTO lote_ave_engorde (
      lote_nombre, granja_id, nucleo_id, galpon_id,
      fecha_encaset, hembras_l, machos_l, mixtas, aves_encasetadas,
      peso_inicial_h, peso_inicial_m, mort_caja_h, mort_caja_m,
      raza, ano_tabla_genetica, tecnico, lote_erp,
      estado_operativo_lote, aves_sobrante,
      company_id, created_by_user_id, created_at,
      pais_id, pais_nombre, empresa_nombre
  ) VALUES (
      'Lote 31', v_granja_id, v_nucleo_id, v_galpon_id,
      v_fecha_enc, 28590, 27765, 0, 56355,
      39.30, 39.60, 0, 0,
      'ROSS AP', 2026, 'panama admin', '31',
      'Abierto', 0,
      v_company_id, v_user_id, now(),
      v_pais_id, v_pais_nombre, 'ItalcolPanama'
  )
  RETURNING lote_ave_engorde_id INTO v_lote_id;

  -- ──────────────────────────────────────────────────────────────────────────
  -- 2) LOTES REPRODUCTORA (H y M juntos, como espera la app)
  -- ──────────────────────────────────────────────────────────────────────────
  INSERT INTO lote_reproductora_ave_engorde (
      lote_ave_engorde_id, reproductora_id, codigo_reproductora, nombre_lote,
      fecha_encasetamiento, h, m, aves_inicio_hembras, aves_inicio_machos,
      mixtas, mort_caja_h, mort_caja_m,
      peso_inicial_h, peso_inicial_m,
      created_at, updated_at
  ) VALUES (
      v_lote_id, 'LR-34', 'H-34 / M-34', 'Lote reproductora 34',
      v_fecha_enc, 14595, 14096, 14595, 14096,
      0, 0, 0,
      38.630, 39.000,
      now(), now()
  )
  RETURNING id INTO v_rep34_id;

  INSERT INTO lote_reproductora_ave_engorde (
      lote_ave_engorde_id, reproductora_id, codigo_reproductora, nombre_lote,
      fecha_encasetamiento, h, m, aves_inicio_hembras, aves_inicio_machos,
      mixtas, mort_caja_h, mort_caja_m,
      peso_inicial_h, peso_inicial_m,
      created_at, updated_at
  ) VALUES (
      v_lote_id, 'LR-32', 'H-32 / M-32', 'Lote reproductora 32',
      v_fecha_enc, 13995, 13669, 13995, 13669,
      0, 0, 0,
      39.990, 40.220,
      now(), now()
  )
  RETURNING id INTO v_rep32_id;

  -- ──────────────────────────────────────────────────────────────────────────
  -- 3) SEGUIMIENTO DIARIO — LOTE REPRODUCTORA 34 (H-34 + M-34)
  --    consumo_kg = qq × 45.36 · agua/calidad de agua SOLO aquí (el cruce
  --    toma el valor del PRIMER lote reproductora, no suma).
  --    qq H: 3,5,6,8,8,9 · qq M: 4,5,6,8,8,9 (días 1–6; día 7 sin datos)
  -- ──────────────────────────────────────────────────────────────────────────
  INSERT INTO seguimiento_diario_lote_reproductora_aves_engorde (
      lote_reproductora_ave_engorde_id, fecha,
      mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
      error_sexaje_hembras, error_sexaje_machos,
      consumo_kg_hembras, consumo_kg_machos, tipo_alimento,
      peso_prom_hembras, peso_prom_machos,
      consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
      ciclo, observaciones, created_by_user_id, created_at
  ) VALUES
  (v_rep34_id, '2026-05-29 00:00:00+00', 10, 13, 0, 0, 0, 0, 136.080, 181.440, 'Pre inicio',  56,  56, 2025, 5.09, 400, 28.3, 'Normal', 'Día 1 — H: 3 qq · M: 4 qq (1 qq = 45.36 kg). Carga Excel Doña María D-1.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-05-30 00:00:00+00',  9, 11, 0, 0, 0, 0, 226.800, 226.800, 'Pre inicio',  72,  72, 2025, 5.16, 407, 28.0, 'Normal', 'Día 2 — H: 5 qq · M: 5 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-05-31 00:00:00+00', 10, 10, 0, 0, 0, 0, 272.160, 272.160, 'Pre inicio',  90,  90, 2026, 5.29, 432, 27.7, 'Normal', 'Día 3 — H: 6 qq · M: 6 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-06-01 00:00:00+00',  9, 10, 5, 0, 0, 0, 362.880, 362.880, 'Pre inicio', 108, 108, 2027, 5.42, 472, 27.2, 'Normal', 'Día 4 — H: 8 qq · M: 8 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-06-02 00:00:00+00',  8,  9, 5, 5, 0, 0, 362.880, 362.880, 'Pre inicio', 136, 136, 2029, 5.70, 492, 27.4, 'Normal', 'Día 5 — H: 8 qq · M: 8 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-06-03 00:00:00+00', 10,  8, 0, 5, 0, 0, 408.240, 408.240, 'Pre inicio', 164, 164, 2032, 5.72, 500, 27.0, 'Normal', 'Día 6 — H: 9 qq · M: 9 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep34_id, '2026-06-04 00:00:00+00',  0,  0, 0, 0, 0, 0,   0.000,   0.000, 'Pre inicio', NULL, NULL, 2035, NULL, NULL, NULL, 'Normal', 'Día 7 — sin datos en el Excel (cierra recogida 7/7).', 'CARGA_EXCEL_LOTE31', now());

  -- ──────────────────────────────────────────────────────────────────────────
  -- 4) SEGUIMIENTO DIARIO — LOTE REPRODUCTORA 32 (H-32 + M-32)
  --    qq H: 4,5,6,8,8,9 · qq M: 4,5,6,8,8,9 (días 1–6; día 7 sin datos)
  -- ──────────────────────────────────────────────────────────────────────────
  INSERT INTO seguimiento_diario_lote_reproductora_aves_engorde (
      lote_reproductora_ave_engorde_id, fecha,
      mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
      error_sexaje_hembras, error_sexaje_machos,
      consumo_kg_hembras, consumo_kg_machos, tipo_alimento,
      peso_prom_hembras, peso_prom_machos,
      consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura,
      ciclo, observaciones, created_by_user_id, created_at
  ) VALUES
  (v_rep32_id, '2026-05-29 00:00:00+00', 10, 11, 0, 0, 0, 0, 181.440, 181.440, 'Pre inicio',  56,  56, NULL, NULL, NULL, NULL, 'Normal', 'Día 1 — H: 4 qq · M: 4 qq (1 qq = 45.36 kg). Carga Excel Doña María D-1.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-05-30 00:00:00+00',  9, 10, 0, 0, 0, 0, 226.800, 226.800, 'Pre inicio',  72,  72, NULL, NULL, NULL, NULL, 'Normal', 'Día 2 — H: 5 qq · M: 5 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-05-31 00:00:00+00',  9, 11, 0, 0, 0, 0, 272.160, 272.160, 'Pre inicio',  90,  90, NULL, NULL, NULL, NULL, 'Normal', 'Día 3 — H: 6 qq · M: 6 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-06-01 00:00:00+00', 11, 12, 5, 5, 0, 0, 362.880, 362.880, 'Pre inicio', 108, 108, NULL, NULL, NULL, NULL, 'Normal', 'Día 4 — H: 8 qq · M: 8 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-06-02 00:00:00+00', 13, 13, 5, 0, 0, 0, 362.880, 362.880, 'Pre inicio', 136, 136, NULL, NULL, NULL, NULL, 'Normal', 'Día 5 — H: 8 qq · M: 8 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-06-03 00:00:00+00',  9,  8, 5, 5, 0, 0, 408.240, 408.240, 'Pre inicio', 164, 164, NULL, NULL, NULL, NULL, 'Normal', 'Día 6 — H: 9 qq · M: 9 qq.', 'CARGA_EXCEL_LOTE31', now()),
  (v_rep32_id, '2026-06-04 00:00:00+00',  0,  0, 0, 0, 0, 0,   0.000,   0.000, 'Pre inicio', NULL, NULL, NULL, NULL, NULL, NULL, 'Normal', 'Día 7 — sin datos en el Excel (cierra recogida 7/7).', 'CARGA_EXCEL_LOTE31', now());

  RAISE NOTICE 'OK — lote_ave_engorde_id=% · reproductora 34 id=% · reproductora 32 id=%', v_lote_id, v_rep34_id, v_rep32_id;
END $$;

COMMIT;

-- ============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
-- ============================================================================

-- A) Lote creado
SELECT lote_ave_engorde_id, lote_nombre, hembras_l, machos_l, aves_encasetadas,
       raza, ano_tabla_genetica, fecha_encaset::date, estado_operativo_lote
  FROM lote_ave_engorde
 WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL;

-- B) Lotes reproductora + totales de bajas (esperado: 34 → mort 117, sel 20 ·
--    32 → mort 126, sel 25 · saldos finales 28,554 y 27,513 → total 56,067)
SELECT lr.reproductora_id, lr.nombre_lote, lr.aves_inicio_hembras, lr.aves_inicio_machos,
       COUNT(s.id)                                                  AS registros,
       SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)) AS mort_total,
       SUM(COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0))                 AS sel_total,
       (lr.aves_inicio_hembras + lr.aves_inicio_machos)
         - SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0))
         - SUM(COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0))             AS aves_actuales
  FROM lote_reproductora_ave_engorde lr
  JOIN seguimiento_diario_lote_reproductora_aves_engorde s ON s.lote_reproductora_ave_engorde_id = lr.id
 WHERE lr.lote_ave_engorde_id = (SELECT lote_ave_engorde_id FROM lote_ave_engorde
                                  WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL)
 GROUP BY lr.id, lr.reproductora_id, lr.nombre_lote, lr.aves_inicio_hembras, lr.aves_inicio_machos
 ORDER BY lr.reproductora_id DESC;

-- C) Cruce generado automáticamente (esperado: 7 filas origen_cruce = true).
--    Día 1: mort H 20 / M 24 (=44) · consumo total 680.40 kg (15 qq).
--    Totales 7 días: mort 243 · sel 45 · consumo 7,212.24 kg (159 qq).
SELECT (metadata->>'edad')::int AS edad, fecha::date,
       mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
       consumo_kg_hembras, consumo_kg_machos,
       round((COALESCE(consumo_kg_hembras,0)+COALESCE(consumo_kg_machos,0))::numeric / 45.36, 2) AS qq_dia,
       peso_prom_hembras, peso_prom_machos, consumo_agua_diario
  FROM seguimiento_diario_aves_engorde
 WHERE origen_cruce
   AND lote_ave_engorde_id = (SELECT lote_ave_engorde_id FROM lote_ave_engorde
                               WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL)
 ORDER BY edad;

-- D) Suma de control vs Excel (tabla principal):
--    mort acum 243 ✓ · sel acum 45 (Excel principal dice 46 ⚠) ·
--    consumo acum 159 qq (Excel principal dice 158 ⚠) — ver plan, discrepancias del Excel.
SELECT SUM(mortalidad_hembras + mortalidad_machos) AS mort_acum,
       SUM(sel_h + sel_m)                          AS sel_acum,
       round(SUM(consumo_kg_hembras + consumo_kg_machos)::numeric, 2) AS kg_acum,
       round(SUM(consumo_kg_hembras + consumo_kg_machos)::numeric / 45.36, 2) AS qq_acum
  FROM seguimiento_diario_aves_engorde
 WHERE origen_cruce
   AND lote_ave_engorde_id = (SELECT lote_ave_engorde_id FROM lote_ave_engorde
                               WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL);

-- ============================================================================
-- LIMPIEZA (para repetir la prueba) — descomentar y ejecutar completo
-- ============================================================================
-- BEGIN;
-- WITH lote AS (
--   SELECT lote_ave_engorde_id AS id FROM lote_ave_engorde
--    WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL
-- )
-- DELETE FROM seguimiento_diario_lote_reproductora_aves_engorde
--  WHERE lote_reproductora_ave_engorde_id IN (
--        SELECT id FROM lote_reproductora_ave_engorde
--         WHERE lote_ave_engorde_id IN (SELECT id FROM lote));
-- -- (el trigger ya borró el cruce al eliminar los seguimientos; por si acaso:)
-- DELETE FROM seguimiento_diario_aves_engorde
--  WHERE origen_cruce AND lote_ave_engorde_id IN (
--        SELECT lote_ave_engorde_id FROM lote_ave_engorde
--         WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL);
-- DELETE FROM lote_reproductora_ave_engorde
--  WHERE lote_ave_engorde_id IN (
--        SELECT lote_ave_engorde_id FROM lote_ave_engorde
--         WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL);
-- DELETE FROM lote_ave_engorde
--  WHERE lote_nombre = 'Lote 31' AND company_id = 5 AND deleted_at IS NULL;
-- COMMIT;
