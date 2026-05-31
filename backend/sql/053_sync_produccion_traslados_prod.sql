-- ============================================================================
-- 053 — Sincronización RDS Producción con migraciones de mayo 2026
-- ============================================================================
-- Contexto:
--   El deploy del 2026-05-26 falló (SIGSEGV) porque la app intenta correr
--   migraciones EF que referencian:
--     • Columnas faltantes (farm_id, erp_create, columnas de traslado, etc.)
--     • Tabla produccion_seguimiento que NO EXISTE en prod
--   Además __EFMigrationsHistory dice que 20251014231114_AddProduccionLoteYSeguimiento
--   está aplicada cuando en realidad nunca se ejecutó (la tabla produccion_lotes
--   sigue con el nombre viejo y produccion_seguimiento no existe).
--
-- Estrategia:
--   1) Crear produccion_seguimiento con TODAS las columnas que el código actual espera.
--   2) Aplicar columnas pendientes de mayo a tablas existentes (idempotente).
--   3) Sincronizar __EFMigrationsHistory marcando como aplicadas las 10 migraciones de mayo.
--   4) NO tocar produccion_lotes/produccion_lote en este script (requiere análisis aparte).
--
-- Pre-requisito: setear Database__RunMigrations=false en TaskDef ECS para evitar
--   que la app vuelva a intentar correr migraciones al arrancar.
--
-- Idempotente: usa IF NOT EXISTS / ON CONFLICT / DO blocks.
-- Reversible: dentro de un BEGIN/COMMIT — si algo falla, ROLLBACK manual.
-- ============================================================================

BEGIN;

-- ============================================================================
-- A) Crear tabla produccion_seguimiento (no existe en prod)
-- ============================================================================
-- Schema basado en entidad ProduccionSeguimiento.cs (Domain) + migraciones
-- 20260524143526 y 20260525041719 que la modifican.

CREATE TABLE IF NOT EXISTS public.produccion_seguimiento (
    id                              SERIAL          PRIMARY KEY,
    lote_id                         INTEGER         NOT NULL,
    fecha_registro                  DATE            NOT NULL,
    mortalidad_h                    INTEGER         NOT NULL DEFAULT 0,
    mortalidad_m                    INTEGER         NOT NULL DEFAULT 0,
    consumo_kg                      NUMERIC(10,2)   NOT NULL DEFAULT 0,
    huevos_totales                  INTEGER         NOT NULL DEFAULT 0,
    huevos_incubables               INTEGER         NOT NULL DEFAULT 0,
    peso_huevo                      NUMERIC(8,2)    NOT NULL DEFAULT 0,
    observaciones                   VARCHAR(1000)   NULL,
    -- Auditoría
    company_id                      INTEGER         NOT NULL,
    created_by_user_id              INTEGER         NOT NULL,
    created_at                      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_by_user_id              INTEGER         NULL,
    updated_at                      TIMESTAMP WITH TIME ZONE NULL,
    deleted_at                      TIMESTAMP WITH TIME ZONE NULL,
    -- Traslados (R3)
    traslado_hembras                INTEGER         NULL,
    traslado_machos                 INTEGER         NULL,
    lote_destino_id                 INTEGER         NULL,
    granja_destino_id               INTEGER         NULL,
    fecha_traslado                  TIMESTAMP       NULL,
    traslado_observaciones          VARCHAR(1000)   NULL,
    -- Traslado splits (Feature 14)
    traslado_ingreso_hembras        INTEGER         NOT NULL DEFAULT 0,
    traslado_ingreso_machos         INTEGER         NOT NULL DEFAULT 0,
    traslado_salida_hembras         INTEGER         NOT NULL DEFAULT 0,
    traslado_salida_machos          INTEGER         NOT NULL DEFAULT 0,
    -- Traslado flags (Feature 14)
    es_traslado                     BOOLEAN         NOT NULL DEFAULT FALSE,
    traslado_lote_contraparte_id    INTEGER         NULL,
    traslado_granja_contraparte_id  INTEGER         NULL,
    traslado_direccion              VARCHAR(10)     NULL,
    -- Selección/error sexaje (alineado con Levante)
    sel_h                           INTEGER         NOT NULL DEFAULT 0,
    sel_m                           INTEGER         NOT NULL DEFAULT 0,
    error_sexaje_hembras            INTEGER         NOT NULL DEFAULT 0,
    error_sexaje_machos             INTEGER         NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_produccion_seguimiento_lote_id
    ON public.produccion_seguimiento (lote_id);
CREATE INDEX IF NOT EXISTS ix_produccion_seguimiento_fecha_registro
    ON public.produccion_seguimiento (fecha_registro);
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_es_traslado
    ON public.produccion_seguimiento (es_traslado) WHERE es_traslado = TRUE;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_name='chk_produccion_seguimiento_traslado_direccion'
  ) THEN
    ALTER TABLE public.produccion_seguimiento
      ADD CONSTRAINT chk_produccion_seguimiento_traslado_direccion
      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA','INGRESO'));
  END IF;
END $$;

-- ============================================================================
-- B) lote_postura_base — agregar farm_id, erp_create
-- (migración 20260524180000_AddFarmIdErpCreateToLotePosturaBase)
-- ============================================================================
ALTER TABLE public.lote_postura_base
    ADD COLUMN IF NOT EXISTS farm_id     INTEGER NULL,
    ADD COLUMN IF NOT EXISTS erp_create  DATE    NULL;

CREATE INDEX IF NOT EXISTS ix_lote_postura_base_farm_id
    ON public.lote_postura_base (farm_id);

-- ============================================================================
-- C) seguimiento_lote_levante — campos de traslado
-- (migración 20260524143526_AddTrasladoAvesFieldsToSeguimientoLevante)
-- ============================================================================
ALTER TABLE public.seguimiento_lote_levante
    ADD COLUMN IF NOT EXISTS traslado_hembras         INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS traslado_machos          INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS lote_destino_id          INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS granja_destino_id        INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS fecha_traslado           TIMESTAMP   NULL,
    ADD COLUMN IF NOT EXISTS traslado_observaciones   VARCHAR(1000) NULL;

-- ============================================================================
-- D) seguimiento_diario_levante_reproductoras — flags + splits + audit
-- (migraciones 20260524214316 + 20260524223050 + 20260525031337)
-- ============================================================================
ALTER TABLE public.seguimiento_diario_levante_reproductoras
    ADD COLUMN IF NOT EXISTS es_traslado                     BOOLEAN     NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS traslado_lote_contraparte_id    INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS traslado_granja_contraparte_id  INTEGER     NULL,
    ADD COLUMN IF NOT EXISTS traslado_direccion              VARCHAR(10) NULL,
    ADD COLUMN IF NOT EXISTS traslado_ingreso_hembras        INTEGER     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS traslado_ingreso_machos         INTEGER     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS traslado_salida_hembras         INTEGER     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS traslado_salida_machos          INTEGER     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS updated_by_user_id              VARCHAR(64) NULL;

CREATE INDEX IF NOT EXISTS idx_seguimiento_diario_lev_es_traslado
    ON public.seguimiento_diario_levante_reproductoras (es_traslado)
    WHERE es_traslado = TRUE;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE constraint_name='chk_sdlr_traslado_direccion'
  ) THEN
    ALTER TABLE public.seguimiento_diario_levante_reproductoras
      ADD CONSTRAINT chk_sdlr_traslado_direccion
      CHECK (traslado_direccion IS NULL OR traslado_direccion IN ('SALIDA','INGRESO'));
  END IF;
END $$;

-- ============================================================================
-- E) lote_postura_levante — acumulados de traslado (con prefijo levante_)
-- (migración 20260524214316_AddTrasladoAcumuladosLPL + rename 20260525131406)
-- ============================================================================
ALTER TABLE public.lote_postura_levante
    ADD COLUMN IF NOT EXISTS levante_traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS levante_traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS levante_traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS levante_traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;

-- ============================================================================
-- F) lote_postura_produccion — acumulados de traslado (con prefijo produccion_)
-- (migración 20260525041719_AddTrasladoAcumuladosLPPandSeguimiento + rename 20260525131406)
-- ============================================================================
ALTER TABLE public.lote_postura_produccion
    ADD COLUMN IF NOT EXISTS produccion_traslado_ingreso_hembras INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS produccion_traslado_ingreso_machos  INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS produccion_traslado_salida_hembras  INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS produccion_traslado_salida_machos   INTEGER NOT NULL DEFAULT 0;

-- ============================================================================
-- G) Sincronizar __EFMigrationsHistory (marcar 10 migraciones de mayo como aplicadas)
-- ============================================================================
INSERT INTO public."__EFMigrationsHistory" (migration_id, product_version) VALUES
  ('20260521100000_AddFechaAlistamientoLoteEngorde',           '9.0.6'),
  ('20260521110000_AddPesosRealesMovimientoEngorde',           '9.0.6'),
  ('20260524143526_AddTrasladoAvesFieldsToSeguimientoLevante', '9.0.6'),
  ('20260524143554_AddTrasladoAvesFieldsToProduccionSeguimiento','9.0.6'),
  ('20260524180000_AddFarmIdErpCreateToLotePosturaBase',       '9.0.6'),
  ('20260524214316_AddTrasladoAcumuladosLPL',                  '9.0.6'),
  ('20260524223050_AddTrasladoSplitsToSeguimientoDiarioLev',   '9.0.6'),
  ('20260525031337_AddUpdatedByUserIdSeguimientoDiarioLev',    '9.0.6'),
  ('20260525041719_AddTrasladoAcumuladosLPPandSeguimiento',    '9.0.6'),
  ('20260525131406_RenameTrasladoColumnsPerFase',              '9.0.6')
ON CONFLICT (migration_id) DO NOTHING;

-- ============================================================================
-- Verificación final (informativa)
-- ============================================================================
DO $$
DECLARE
  missing_count INTEGER := 0;
BEGIN
  -- Verificar columnas críticas
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='lote_postura_base' AND column_name='farm_id') THEN
    RAISE WARNING 'FALTA: lote_postura_base.farm_id';
    missing_count := missing_count + 1;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='lote_postura_base' AND column_name='erp_create') THEN
    RAISE WARNING 'FALTA: lote_postura_base.erp_create';
    missing_count := missing_count + 1;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name='produccion_seguimiento') THEN
    RAISE WARNING 'FALTA: tabla produccion_seguimiento';
    missing_count := missing_count + 1;
  END IF;

  IF missing_count = 0 THEN
    RAISE NOTICE 'OK: todas las columnas/tablas verificadas presentes';
  ELSE
    RAISE EXCEPTION 'Faltan % artefactos — ROLLBACK', missing_count;
  END IF;
END $$;

COMMIT;

-- ============================================================================
-- POST-COMMIT: pendiente fuera de este script
-- ============================================================================
-- 1) Setear env var Database__RunMigrations=false en TaskDef ECS de producción
--    (revisión nueva del task-definition sin RunMigrations=true).
-- 2) Re-deploy del backend con la imagen `20260526-0924` (o build nuevo).
-- 3) Smoke test: PUT /api/LotePosturaBase/2 con body de prueba.
-- 4) Investigar aparte: produccion_lotes vs produccion_lote (rename pendiente
--    de migración 20251014231114 marcada como aplicada pero nunca ejecutada).
