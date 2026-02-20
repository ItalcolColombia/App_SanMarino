-- Tabla: public.lote_reproductora_ave_engorde
-- Registros de "lote reproductora" asociados a un Lote Aves de Engorde (pollo de engorde).
-- Permite crear varios lotes reproductora por lote ave engorde, distribuyendo las aves encasetadas.
-- Requiere haber ejecutado antes: create_lote_ave_engorde.sql

CREATE TABLE IF NOT EXISTS public.lote_reproductora_ave_engorde (
    id                          SERIAL PRIMARY KEY,
    lote_ave_engorde_id         INTEGER NOT NULL,
    reproductora_id             VARCHAR(64) NOT NULL,
    nombre_lote                 VARCHAR(200) NOT NULL,
    fecha_encasetamiento       TIMESTAMPTZ NULL,

    m                           INTEGER NULL,
    h                           INTEGER NULL,
    aves_inicio_hembras         INTEGER NULL,
    aves_inicio_machos          INTEGER NULL,
    mixtas                      INTEGER NULL,
    mort_caja_h                 INTEGER NULL,
    mort_caja_m                 INTEGER NULL,
    unif_h                      INTEGER NULL,
    unif_m                      INTEGER NULL,

    peso_inicial_m              NUMERIC(10,3) NULL,
    peso_inicial_h              NUMERIC(10,3) NULL,
    peso_mixto                  NUMERIC(10,3) NULL,

    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),

    CONSTRAINT fk_lrae_lote_ave_engorde
        FOREIGN KEY (lote_ave_engorde_id) REFERENCES public.lote_ave_engorde(lote_ave_engorde_id) ON DELETE RESTRICT,
    CONSTRAINT uq_lrae_lote_reproductora
        UNIQUE (lote_ave_engorde_id, reproductora_id),
    CONSTRAINT ck_lrae_nonneg
        CHECK (
            (m IS NULL OR m >= 0) AND (h IS NULL OR h >= 0) AND
            (mixtas IS NULL OR mixtas >= 0) AND
            (peso_inicial_m IS NULL OR peso_inicial_m >= 0) AND
            (peso_inicial_h IS NULL OR peso_inicial_h >= 0) AND
            (peso_mixto IS NULL OR peso_mixto >= 0)
        )
);

CREATE INDEX IF NOT EXISTS ix_lote_reproductora_ave_engorde_lote
    ON public.lote_reproductora_ave_engorde(lote_ave_engorde_id);
CREATE INDEX IF NOT EXISTS ix_lote_reproductora_ave_engorde_reproductora
    ON public.lote_reproductora_ave_engorde(reproductora_id);
CREATE INDEX IF NOT EXISTS ix_lote_reproductora_ave_engorde_fecha
    ON public.lote_reproductora_ave_engorde(fecha_encasetamiento);

COMMENT ON TABLE public.lote_reproductora_ave_engorde IS 'Lotes reproductora creados a partir de un Lote Aves de Engorde; cada registro distribuye aves del lote ave engorde.';
