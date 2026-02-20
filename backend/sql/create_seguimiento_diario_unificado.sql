-- =============================================================================
-- Tabla unificada: seguimiento_diario
-- Contiene todos los campos para los tres tipos de seguimiento:
--   'levante'     = Seguimiento diario lote levante
--   'produccion'  = Seguimiento diario producción (produccion_diaria)
--   'reproductora' = Seguimiento diario lote reproductora (lote_seguimientos)
-- Los campos no usados por un tipo quedan en NULL.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.seguimiento_diario (
  id                    BIGSERIAL PRIMARY KEY,

  -- Identificación del tipo y clave natural
  tipo_seguimiento      VARCHAR(20) NOT NULL
    CHECK (tipo_seguimiento IN ('levante', 'produccion', 'reproductora')),
  lote_id               VARCHAR(64) NOT NULL,
  reproductora_id       VARCHAR(64) NULL,  -- Solo para tipo 'reproductora'
  fecha                 TIMESTAMPTZ NOT NULL,

  -- ---------- Campos comunes (todos los tipos) ----------
  mortalidad_hembras    INT NULL,
  mortalidad_machos     INT NULL,
  sel_h                 INT NULL,
  sel_m                 INT NULL,
  error_sexaje_hembras  INT NULL,
  error_sexaje_machos   INT NULL,
  consumo_kg_hembras    NUMERIC(12, 3) NULL,
  consumo_kg_machos     NUMERIC(12, 3) NULL,
  tipo_alimento         VARCHAR(100) NULL,
  observaciones         TEXT NULL,
  ciclo                 VARCHAR(50) NULL DEFAULT 'Normal',

  -- Peso promedio y uniformidad (levante / reproductora: H y M; producción pesaje: ver peso_h, peso_m, uniformidad)
  peso_prom_hembras     DOUBLE PRECISION NULL,
  peso_prom_machos      DOUBLE PRECISION NULL,
  uniformidad_hembras  DOUBLE PRECISION NULL,
  uniformidad_machos    DOUBLE PRECISION NULL,
  cv_hembras            DOUBLE PRECISION NULL,
  cv_machos             DOUBLE PRECISION NULL,

  -- Agua (Ecuador / Panamá)
  consumo_agua_diario   DOUBLE PRECISION NULL,
  consumo_agua_ph       DOUBLE PRECISION NULL,
  consumo_agua_orp      DOUBLE PRECISION NULL,
  consumo_agua_temperatura DOUBLE PRECISION NULL,

  -- JSONB
  metadata              JSONB NULL,
  items_adicionales     JSONB NULL,

  -- ---------- Solo reproductora ----------
  peso_inicial          NUMERIC(10, 3) NULL,
  peso_final            NUMERIC(10, 3) NULL,

  -- ---------- Solo levante ----------
  kcal_al_h             DOUBLE PRECISION NULL,
  prot_al_h             DOUBLE PRECISION NULL,
  kcal_ave_h            DOUBLE PRECISION NULL,
  prot_ave_h            DOUBLE PRECISION NULL,

  -- ---------- Solo producción ----------
  huevo_tot             INT NULL,
  huevo_inc             INT NULL,
  huevo_limpio          INT NULL,
  huevo_tratado         INT NULL,
  huevo_sucio           INT NULL,
  huevo_deforme         INT NULL,
  huevo_blanco          INT NULL,
  huevo_doble_yema      INT NULL,
  huevo_piso            INT NULL,
  huevo_pequeno         INT NULL,
  huevo_roto            INT NULL,
  huevo_desecho         INT NULL,
  huevo_otro            INT NULL,
  peso_huevo            DOUBLE PRECISION NULL,
  etapa                 INT NULL,
  -- Pesaje semanal producción
  peso_h                NUMERIC(8, 2) NULL,
  peso_m                NUMERIC(8, 2) NULL,
  uniformidad           NUMERIC(5, 2) NULL,
  coeficiente_variacion NUMERIC(5, 2) NULL,
  observaciones_pesaje  TEXT NULL,

  -- Auditoría (usuario que crea el seguimiento)
  created_by_user_id    VARCHAR(64) NULL,
  created_at            TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
  updated_at            TIMESTAMPTZ NULL
);

-- Unicidad: por tipo, lote, reproductora (si aplica) y fecha (índice único con expresión)
CREATE UNIQUE INDEX uq_seguimiento_diario_tipo_lote_rep_fecha
  ON public.seguimiento_diario (tipo_seguimiento, lote_id, COALESCE(reproductora_id, ''), fecha);

-- Índices para consultas por tipo y fecha
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_tipo
  ON public.seguimiento_diario (tipo_seguimiento);

CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_lote_id
  ON public.seguimiento_diario (lote_id);

CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_fecha
  ON public.seguimiento_diario (fecha);

CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_tipo_lote_fecha
  ON public.seguimiento_diario (tipo_seguimiento, lote_id, fecha);

-- Para reproductora: búsqueda por lote + reproductora
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_lote_reproductora_fecha
  ON public.seguimiento_diario (lote_id, reproductora_id, fecha)
  WHERE tipo_seguimiento = 'reproductora' AND reproductora_id IS NOT NULL;

-- Comentarios
COMMENT ON TABLE public.seguimiento_diario IS
  'Tabla unificada de seguimiento diario: levante, producción y reproductora. tipo_seguimiento indica el módulo.';
COMMENT ON COLUMN public.seguimiento_diario.tipo_seguimiento IS
  'Valores: levante | produccion | reproductora';
COMMENT ON COLUMN public.seguimiento_diario.reproductora_id IS
  'Obligatorio solo cuando tipo_seguimiento = reproductora (FK conceptual a lote_reproductoras).';
COMMENT ON COLUMN public.seguimiento_diario.metadata IS
  'Detalle de ítems (alimentos, etc.). Uso por tipo igual que en tablas actuales.';
COMMENT ON COLUMN public.seguimiento_diario.items_adicionales IS
  'Vacunas, medicamentos, etc. (levante y reproductora).';
COMMENT ON COLUMN public.seguimiento_diario.created_by_user_id IS
  'ID del usuario (string) que crea el registro de seguimiento.';

-- =============================================================================
-- Si la tabla ya existía sin created_by_user_id, ejecutar solo:
-- =============================================================================
-- ALTER TABLE public.seguimiento_diario
--   ADD COLUMN IF NOT EXISTS created_by_user_id VARCHAR(64) NULL;
-- COMMENT ON COLUMN public.seguimiento_diario.created_by_user_id IS
--   'ID del usuario (string) que crea el registro de seguimiento.';
