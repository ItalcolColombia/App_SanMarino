-- =============================================================================
-- OPCIÓN B: Unificar Lote + Producción en tabla lotes (fase + lotes hijos)
-- =============================================================================
-- Tabla de seguimiento: seguimiento_diario (unificada para levante/produccion/reproductora)
-- No se usa produccion_seguimiento ni produccion_diaria en este script.
-- 1. Añadir columna fase y campos de producción a lotes
-- 2. Añadir lote_id_int a seguimiento_diario (solo para tipo 'produccion') y migrar
-- 3. Deprecar produccion_lotes cuando la app ya no la use
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Columnas nuevas en lotes
-- -----------------------------------------------------------------------------
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS fase VARCHAR(20) NOT NULL DEFAULT 'Levante';

ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS fecha_inicio_produccion TIMESTAMPTZ NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS hembras_iniciales_prod INTEGER NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS machos_iniciales_prod INTEGER NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS huevos_iniciales INTEGER NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS tipo_nido VARCHAR(50) NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS nucleo_p VARCHAR(100) NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS ciclo_produccion VARCHAR(50) NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS fecha_fin_produccion TIMESTAMPTZ NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS aves_fin_hembras_prod INTEGER NULL;
ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS aves_fin_machos_prod INTEGER NULL;

-- Constraint para fase
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'ck_lote_fase'
  ) THEN
    ALTER TABLE public.lotes ADD CONSTRAINT ck_lote_fase
      CHECK (fase IN ('Levante', 'Produccion'));
  END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2. Añadir lote_id_int a seguimiento_diario (nullable, solo para tipo 'produccion')
--    La tabla unificada ya tiene lote_id VARCHAR; añadimos lote_id_int para FK a lotes
-- -----------------------------------------------------------------------------
ALTER TABLE public.seguimiento_diario
  ADD COLUMN IF NOT EXISTS lote_id_int INTEGER NULL;

-- FK (solo si no existe)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_seguimiento_diario_lote_int'
  ) THEN
    ALTER TABLE public.seguimiento_diario
      ADD CONSTRAINT fk_seguimiento_diario_lote_int
      FOREIGN KEY (lote_id_int) REFERENCES public.lotes (lote_id) ON DELETE CASCADE;
  END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 3. Migrar: por cada produccion_lotes crear lote hijo y actualizar seguimiento_diario
--    En seguimiento_diario (tipo 'produccion'), lote_id puede ser:
--    - el id de produccion_lotes (pl.id) como texto, o
--    - el lote padre (pl.lote_id). Se intenta matchear por ambos.
-- -----------------------------------------------------------------------------
DO $$
DECLARE
  r RECORD;
  nuevo_id INTEGER;
  padre_id INT;
BEGIN
  FOR r IN
    SELECT pl.id AS pl_id, pl.lote_id AS lote_id_str,
           pl.fecha_inicio_produccion, pl.hembras_iniciales, pl.machos_iniciales,
           pl.huevos_iniciales, pl.tipo_nido, pl.nucleo_p, pl.galpon_id,
           pl.granja_id, pl.nucleo_id, pl.ciclo, pl.fecha_fin,
           pl.aves_fin_hembras, pl.aves_fin_machos,
           l.lote_nombre, l.company_id, l.created_by_user_id, l.created_at
    FROM public.produccion_lotes pl
    LEFT JOIN public.lotes l ON l.lote_id = NULLIF(TRIM(pl.lote_id), '')::INTEGER
    WHERE l.lote_id IS NOT NULL
  LOOP
    BEGIN
      padre_id := r.lote_id_str::INTEGER;
    EXCEPTION WHEN OTHERS THEN
      padre_id := NULL;
    END;
    IF padre_id IS NULL THEN
      CONTINUE;
    END IF;

    INSERT INTO public.lotes (
      lote_nombre, granja_id, nucleo_id, galpon_id,
      fase, lote_padre_id,
      fecha_inicio_produccion, hembras_iniciales_prod, machos_iniciales_prod,
      huevos_iniciales, tipo_nido, nucleo_p, ciclo_produccion,
      fecha_fin_produccion, aves_fin_hembras_prod, aves_fin_machos_prod,
      company_id, created_by_user_id, created_at
    ) VALUES (
      COALESCE(r.lote_nombre, '') || ' - Prod',
      r.granja_id,
      r.nucleo_id,
      r.galpon_id,
      'Produccion',
      padre_id,
      r.fecha_inicio_produccion,
      r.hembras_iniciales,
      r.machos_iniciales,
      r.huevos_iniciales,
      COALESCE(r.tipo_nido, 'Manual'),
      r.nucleo_p,
      COALESCE(r.ciclo, 'normal'),
      r.fecha_fin,
      r.aves_fin_hembras,
      r.aves_fin_machos,
      r.company_id,
      r.created_by_user_id,
      COALESCE(r.created_at, NOW() AT TIME ZONE 'utc')
    )
    RETURNING lotes.lote_id INTO nuevo_id;

    -- Actualizar seguimiento_diario: filas de tipo 'produccion' que referencian
    -- este produccion_lote por lote_id = pl.id (texto) o lote_id = pl.lote_id (padre)
    UPDATE public.seguimiento_diario
    SET lote_id_int = nuevo_id
    WHERE tipo_seguimiento = 'produccion'
      AND (lote_id = r.pl_id::TEXT OR TRIM(lote_id) = TRIM(r.lote_id_str))
      AND lote_id_int IS NULL;
  END LOOP;
END $$;

-- -----------------------------------------------------------------------------
-- 4. Índice para consultas por lote_id_int (producción)
-- -----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_lote_id_int
  ON public.seguimiento_diario (lote_id_int)
  WHERE tipo_seguimiento = 'produccion' AND lote_id_int IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_seguimiento_diario_prod_lote_fecha
  ON public.seguimiento_diario (lote_id_int, fecha)
  WHERE tipo_seguimiento = 'produccion' AND lote_id_int IS NOT NULL;

-- -----------------------------------------------------------------------------
-- 5. (Opcional) Eliminar tabla produccion_lotes tras verificar datos
--    Descomentar cuando la app ya no la use.
-- -----------------------------------------------------------------------------
-- DROP TABLE IF EXISTS public.produccion_lotes CASCADE;

COMMENT ON COLUMN public.lotes.fase IS 'Levante = lote inicial; Produccion = lote hijo al pasar a producción (puede haber varios por lote padre).';
COMMENT ON COLUMN public.seguimiento_diario.lote_id_int IS 'FK a lotes.lote_id para tipo produccion (Opción B). Sustituye referencia por lote_id varchar cuando esté rellenado.';
