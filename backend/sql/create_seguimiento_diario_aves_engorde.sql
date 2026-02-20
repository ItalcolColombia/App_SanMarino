-- Seguimiento diario aves de engorde: un registro por lote_ave_engorde por fecha.
-- Los filtros del módulo "Seguimiento Diario Aves de Engorde" muestran lotes de lote_ave_engorde.
-- Ejecutar después de create_lote_ave_engorde.sql

CREATE TABLE IF NOT EXISTS public.seguimiento_diario_aves_engorde (
    id                      BIGSERIAL PRIMARY KEY,
    lote_ave_engorde_id     INTEGER NOT NULL,

    fecha                   TIMESTAMPTZ NOT NULL,

    mortalidad_hembras      INT NULL,
    mortalidad_machos       INT NULL,
    sel_h                   INT NULL,
    sel_m                   INT NULL,
    error_sexaje_hembras    INT NULL,
    error_sexaje_machos     INT NULL,
    consumo_kg_hembras      NUMERIC(12, 3) NULL,
    consumo_kg_machos       NUMERIC(12, 3) NULL,
    tipo_alimento           VARCHAR(100) NULL,
    observaciones           TEXT NULL,
    ciclo                   VARCHAR(50) NULL DEFAULT 'Normal',

    peso_prom_hembras       DOUBLE PRECISION NULL,
    peso_prom_machos        DOUBLE PRECISION NULL,
    uniformidad_hembras     DOUBLE PRECISION NULL,
    uniformidad_machos      DOUBLE PRECISION NULL,
    cv_hembras              DOUBLE PRECISION NULL,
    cv_machos               DOUBLE PRECISION NULL,

    consumo_agua_diario    DOUBLE PRECISION NULL,
    consumo_agua_ph         DOUBLE PRECISION NULL,
    consumo_agua_orp        DOUBLE PRECISION NULL,
    consumo_agua_temperatura DOUBLE PRECISION NULL,

    metadata                JSONB NULL,
    items_adicionales       JSONB NULL,

    kcal_al_h               DOUBLE PRECISION NULL,
    prot_al_h               DOUBLE PRECISION NULL,
    kcal_ave_h              DOUBLE PRECISION NULL,
    prot_ave_h              DOUBLE PRECISION NULL,

    created_by_user_id      VARCHAR(64) NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at              TIMESTAMPTZ NULL,

    CONSTRAINT fk_seg_diario_aves_engorde_lote
        FOREIGN KEY (lote_ave_engorde_id) REFERENCES public.lote_ave_engorde(lote_ave_engorde_id) ON DELETE RESTRICT,
    CONSTRAINT uq_seg_diario_aves_engorde_lote_fecha
        UNIQUE (lote_ave_engorde_id, fecha)
);

CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_aves_engorde_lote
    ON public.seguimiento_diario_aves_engorde (lote_ave_engorde_id);
CREATE INDEX IF NOT EXISTS ix_seguimiento_diario_aves_engorde_fecha
    ON public.seguimiento_diario_aves_engorde (fecha);

COMMENT ON TABLE public.seguimiento_diario_aves_engorde IS
'Seguimiento diario por lote aves de engorde. Un registro por lote_ave_engorde por fecha.';
