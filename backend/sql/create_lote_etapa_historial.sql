-- =============================================================================
-- HISTORIAL POR ETAPAS DEL LOTE (Levante, Producción, Reproductora)
-- =============================================================================
-- Objetivo: Mantener el historial de aves con que se abrió cada etapa y con
-- cuántas se cerró, sin modificar los datos originales del lote (lotes.hembras_l,
-- lotes.machos_l permanecen como creación).
--
-- Flujo:
-- 1. CREACIÓN LOTE → Se inserta en lote_etapa_levante (aves con que inicia Levante).
-- 2. LEVANTE → Los descuentos (mortalidad, sel, etc.) se registran en
--    seguimiento_lote_levante. Aves actuales = aves_inicio - sum(descuentos).
-- 3. PASO A PRODUCCIÓN (semana 26) → Se cierra Levante (fecha_fin, aves_fin) y
--    se crea produccion_lotes (aves con que inicia Producción).
-- 4. PRODUCCIÓN → Seguimientos diarios descontando; aves actuales = aves_iniciales - sum(descuentos).
-- 5. REPRODUCTORA → lote_reproductoras con aves_inicio y aves actuales (h/m).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Tabla: lote_etapa_levante (historial etapa Levante)
-- Una fila por lote: con cuántas aves inició Levante y con cuántas terminó
-- (cuando pasa a Producción).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.lote_etapa_levante (
  id                    SERIAL PRIMARY KEY,
  lote_id               INTEGER NOT NULL,
  aves_inicio_hembras   INTEGER NOT NULL DEFAULT 0,
  aves_inicio_machos    INTEGER NOT NULL DEFAULT 0,
  fecha_inicio          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  fecha_fin             TIMESTAMPTZ NULL,
  aves_fin_hembras      INTEGER NULL,
  aves_fin_machos       INTEGER NULL,
  created_at            TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  updated_at             TIMESTAMPTZ NULL,
  CONSTRAINT uq_lote_etapa_levante_lote UNIQUE (lote_id),
  CONSTRAINT fk_lote_etapa_levante_lote FOREIGN KEY (lote_id)
    REFERENCES public.lotes (lote_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_lote_etapa_levante_lote_id
  ON public.lote_etapa_levante (lote_id);
CREATE INDEX IF NOT EXISTS ix_lote_etapa_levante_fecha_inicio
  ON public.lote_etapa_levante (fecha_inicio);
CREATE INDEX IF NOT EXISTS ix_lote_etapa_levante_fecha_fin
  ON public.lote_etapa_levante (fecha_fin) WHERE fecha_fin IS NOT NULL;

COMMENT ON TABLE public.lote_etapa_levante IS
  'Historial etapa Levante: aves con que inicia y termina el lote en Levante. Una fila por lote.';
COMMENT ON COLUMN public.lote_etapa_levante.aves_inicio_hembras IS
  'Hembras con que se abrió el lote (mismo que lotes.hembras_l al crear).';
COMMENT ON COLUMN public.lote_etapa_levante.aves_fin_hembras IS
  'Hembras vivas al cerrar Levante (al pasar a Producción).';

-- -----------------------------------------------------------------------------
-- 2. Producción: agregar cierre de etapa (opcional)
-- Cuando se da por terminada la etapa de producción.
-- -----------------------------------------------------------------------------
ALTER TABLE public.produccion_lotes
  ADD COLUMN IF NOT EXISTS fecha_fin TIMESTAMPTZ NULL;
ALTER TABLE public.produccion_lotes
  ADD COLUMN IF NOT EXISTS aves_fin_hembras INTEGER NULL;
ALTER TABLE public.produccion_lotes
  ADD COLUMN IF NOT EXISTS aves_fin_machos INTEGER NULL;

COMMENT ON COLUMN public.produccion_lotes.fecha_fin IS
  'Fecha en que se cierra la etapa de producción (depopulación/fin de ciclo).';
COMMENT ON COLUMN public.produccion_lotes.aves_fin_hembras IS
  'Hembras al cierre de la etapa de producción.';

-- -----------------------------------------------------------------------------
-- 3. Lote reproductora: aves con que se abrió el lote reproductora
-- (h/m son las cantidades actuales; aves_inicio_* es el historial al abrir.)
-- -----------------------------------------------------------------------------
ALTER TABLE public.lote_reproductoras
  ADD COLUMN IF NOT EXISTS aves_inicio_hembras INTEGER NULL;
ALTER TABLE public.lote_reproductoras
  ADD COLUMN IF NOT EXISTS aves_inicio_machos INTEGER NULL;

COMMENT ON COLUMN public.lote_reproductoras.aves_inicio_hembras IS
  'Hembras con que se abrió el lote reproductora (historial).';
COMMENT ON COLUMN public.lote_reproductoras.aves_inicio_machos IS
  'Machos con que se abrió el lote reproductora (historial).';

-- -----------------------------------------------------------------------------
-- 4. Poblar lote_etapa_levante para lotes existentes (opcional, una sola vez)
-- Solo para lotes que aún no tienen fila en lote_etapa_levante.
-- -----------------------------------------------------------------------------
INSERT INTO public.lote_etapa_levante (lote_id, aves_inicio_hembras, aves_inicio_machos, fecha_inicio, created_at)
SELECT l.lote_id,
       COALESCE(l.hembras_l, 0),
       COALESCE(l.machos_l, 0),
       COALESCE(l.fecha_encaset, l.created_at, NOW() AT TIME ZONE 'utc'),
       NOW() AT TIME ZONE 'utc'
FROM public.lotes l
WHERE l.lote_id IS NOT NULL
  AND (l.deleted_at IS NULL)
  AND NOT EXISTS (
    SELECT 1 FROM public.lote_etapa_levante lel WHERE lel.lote_id = l.lote_id
  )
ON CONFLICT (lote_id) DO NOTHING;
