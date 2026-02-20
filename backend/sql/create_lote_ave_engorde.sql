-- Crear tabla lote_ave_engorde (misma estructura que lotes pero tabla independiente para lotes de engorde)
-- Ejecutar en la base de datos antes de usar el módulo Lote de Engorde

CREATE TABLE IF NOT EXISTS public.lote_ave_engorde (
    lote_ave_engorde_id SERIAL PRIMARY KEY,
    lote_nombre         VARCHAR(200) NOT NULL,
    granja_id           INTEGER NOT NULL,
    nucleo_id           VARCHAR(64) NULL,
    galpon_id           VARCHAR(64) NULL,
    regional            VARCHAR(100) NULL,
    fecha_encaset       TIMESTAMPTZ NULL,

    hembras_l           INTEGER NULL,
    machos_l            INTEGER NULL,
    peso_inicial_h      DOUBLE PRECISION NULL,
    peso_inicial_m      DOUBLE PRECISION NULL,
    unif_h              DOUBLE PRECISION NULL,
    unif_m              DOUBLE PRECISION NULL,

    mort_caja_h         INTEGER NULL,
    mort_caja_m         INTEGER NULL,
    raza                VARCHAR(80) NULL,
    ano_tabla_genetica  INTEGER NULL,
    linea               VARCHAR(80) NULL,
    tipo_linea          VARCHAR(80) NULL,
    codigo_guia_genetica VARCHAR(80) NULL,
    linea_genetica_id   INTEGER NULL,
    tecnico             VARCHAR(120) NULL,

    mixtas              INTEGER NULL,
    peso_mixto          DOUBLE PRECISION NULL,
    aves_encasetadas    INTEGER NULL,
    edad_inicial        INTEGER NULL,
    lote_erp            VARCHAR(80) NULL,
    estado_traslado     VARCHAR(50) NULL,

    pais_id             INTEGER NULL,
    pais_nombre         VARCHAR(120) NULL,
    empresa_nombre      VARCHAR(200) NULL,

    -- Auditoría (AuditableEntity)
    company_id          INTEGER NOT NULL,
    created_by_user_id  INTEGER NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id   INTEGER NULL,
    updated_at          TIMESTAMPTZ NULL,
    deleted_at          TIMESTAMPTZ NULL,

    CONSTRAINT fk_lote_ave_engorde_granja FOREIGN KEY (granja_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_lote_ave_engorde_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT ck_lae_nonneg_counts CHECK (
        (hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL)
        AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)
    ),
    CONSTRAINT ck_lae_nonneg_pesos CHECK (
        (peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL)
        AND (peso_mixto >= 0 OR peso_mixto IS NULL)
    )
);

-- Índices
CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_granja ON public.lote_ave_engorde(granja_id);
CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_nucleo ON public.lote_ave_engorde(nucleo_id) WHERE nucleo_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_galpon ON public.lote_ave_engorde(galpon_id) WHERE galpon_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_company_deleted ON public.lote_ave_engorde(company_id) WHERE deleted_at IS NULL;

-- FK opcionales a nucleos y galpones (composite/unique según tu esquema)
-- Si nucleos tiene PK (nucleo_id, granja_id):
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_lote_ave_engorde_nucleo'
    ) THEN
        ALTER TABLE public.lote_ave_engorde
        ADD CONSTRAINT fk_lote_ave_engorde_nucleo
        FOREIGN KEY (nucleo_id, granja_id) REFERENCES public.nucleos(nucleo_id, granja_id) ON DELETE RESTRICT;
    END IF;
EXCEPTION
    WHEN undefined_table OR undefined_column THEN NULL; -- ignorar si nucleos no existe o PK distinta
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_lote_ave_engorde_galpon'
    ) THEN
        ALTER TABLE public.lote_ave_engorde
        ADD CONSTRAINT fk_lote_ave_engorde_galpon
        FOREIGN KEY (galpon_id) REFERENCES public.galpones(galpon_id) ON DELETE RESTRICT;
    END IF;
EXCEPTION
    WHEN undefined_table OR undefined_column THEN NULL;
END $$;

COMMENT ON TABLE public.lote_ave_engorde IS 'Lotes de ave de engorde; estructura análoga a lotes pero tabla independiente.';
