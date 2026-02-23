-- ============================================================
-- TABLAS LOTE POSTURA LEVANTE Y PRODUCCIÓN
-- Módulo de lotes postura: distribución Levante y Producción
-- ============================================================

-- 1. LOTE_POSTURA_LEVANTE (todos los campos de lotes + campos específicos postura)
CREATE TABLE IF NOT EXISTS public.lote_postura_levante (
    lote_postura_levante_id SERIAL PRIMARY KEY,
    lote_nombre             VARCHAR(200) NOT NULL,
    granja_id               INTEGER NOT NULL,
    nucleo_id               VARCHAR(64) NULL,
    galpon_id               VARCHAR(64) NULL,
    regional                VARCHAR(100) NULL,
    fecha_encaset           TIMESTAMPTZ NULL,

    hembras_l               INTEGER NULL,
    machos_l                INTEGER NULL,
    peso_inicial_h          DOUBLE PRECISION NULL,
    peso_inicial_m          DOUBLE PRECISION NULL,
    unif_h                  DOUBLE PRECISION NULL,
    unif_m                  DOUBLE PRECISION NULL,

    mort_caja_h             INTEGER NULL,
    mort_caja_m             INTEGER NULL,
    raza                    VARCHAR(80) NULL,
    ano_tabla_genetica      INTEGER NULL,
    linea                   VARCHAR(80) NULL,
    tipo_linea              VARCHAR(80) NULL,
    codigo_guia_genetica    VARCHAR(80) NULL,
    linea_genetica_id       INTEGER NULL,
    tecnico                 VARCHAR(120) NULL,

    mixtas                  INTEGER NULL,
    peso_mixto              DOUBLE PRECISION NULL,
    aves_encasetadas        INTEGER NULL,
    edad_inicial            INTEGER NULL,
    lote_erp                VARCHAR(80) NULL,
    estado_traslado         VARCHAR(50) NULL,

    pais_id                 INTEGER NULL,
    pais_nombre             VARCHAR(120) NULL,
    empresa_nombre          VARCHAR(200) NULL,

    -- Campos específicos postura levante
    lote_id                 INTEGER NULL,
    lote_padre_id           INTEGER NULL,
    lote_postura_levante_padre_id INTEGER NULL,  -- FK a lote_postura_levante (padre)
    aves_h_inicial          INTEGER NULL,
    aves_m_inicial          INTEGER NULL,
    aves_h_actual           INTEGER NULL,
    aves_m_actual           INTEGER NULL,
    empresa_id              INTEGER NULL,
    usuario_id              INTEGER NULL,
    estado                  VARCHAR(50) NULL,
    etapa                   VARCHAR(50) NULL,
    edad                    INTEGER NULL,

    -- Auditoría (AuditableEntity)
    company_id              INTEGER NOT NULL,
    created_by_user_id      INTEGER NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id      INTEGER NULL,
    updated_at              TIMESTAMPTZ NULL,
    deleted_at              TIMESTAMPTZ NULL,

    CONSTRAINT fk_lpl_granja FOREIGN KEY (granja_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_lpl_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_lpl_lote FOREIGN KEY (lote_id) REFERENCES public.lotes(lote_id) ON DELETE RESTRICT,
    CONSTRAINT ck_lpl_nonneg_counts CHECK (
        (hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL)
        AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)
    ),
    CONSTRAINT ck_lpl_nonneg_pesos CHECK (
        (peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL)
        AND (peso_mixto >= 0 OR peso_mixto IS NULL)
    )
);

-- Índices lote_postura_levante
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_granja ON public.lote_postura_levante(granja_id);
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_nucleo ON public.lote_postura_levante(nucleo_id) WHERE nucleo_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_galpon ON public.lote_postura_levante(galpon_id) WHERE galpon_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_lote ON public.lote_postura_levante(lote_id) WHERE lote_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_padre ON public.lote_postura_levante(lote_postura_levante_padre_id) WHERE lote_postura_levante_padre_id IS NOT NULL;

-- FK self-reference (después de crear la tabla)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_lpl_lote_postura_levante_padre') THEN
        ALTER TABLE public.lote_postura_levante
            ADD CONSTRAINT fk_lpl_lote_postura_levante_padre
            FOREIGN KEY (lote_postura_levante_padre_id) REFERENCES public.lote_postura_levante(lote_postura_levante_id) ON DELETE RESTRICT;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_lote_postura_levante_company_deleted ON public.lote_postura_levante(company_id) WHERE deleted_at IS NULL;

COMMENT ON TABLE public.lote_postura_levante IS 'Lotes de postura etapa Levante. Estructura de lotes más campos específicos para distribución.';

-- 2. LOTE_POSTURA_PRODUCCION (campos de lotes + clasificación huevos + específicos postura)
CREATE TABLE IF NOT EXISTS public.lote_postura_produccion (
    lote_postura_produccion_id SERIAL PRIMARY KEY,
    lote_nombre             VARCHAR(200) NOT NULL,
    granja_id               INTEGER NOT NULL,
    nucleo_id               VARCHAR(64) NULL,
    galpon_id               VARCHAR(64) NULL,
    regional                VARCHAR(100) NULL,
    fecha_encaset           TIMESTAMPTZ NULL,

    hembras_l               INTEGER NULL,
    machos_l                INTEGER NULL,
    peso_inicial_h          DOUBLE PRECISION NULL,
    peso_inicial_m          DOUBLE PRECISION NULL,
    unif_h                  DOUBLE PRECISION NULL,
    unif_m                  DOUBLE PRECISION NULL,

    mort_caja_h             INTEGER NULL,
    mort_caja_m             INTEGER NULL,
    raza                    VARCHAR(80) NULL,
    ano_tabla_genetica      INTEGER NULL,
    linea                   VARCHAR(80) NULL,
    tipo_linea              VARCHAR(80) NULL,
    codigo_guia_genetica    VARCHAR(80) NULL,
    linea_genetica_id       INTEGER NULL,
    tecnico                 VARCHAR(120) NULL,

    mixtas                  INTEGER NULL,
    peso_mixto              DOUBLE PRECISION NULL,
    aves_encasetadas        INTEGER NULL,
    edad_inicial            INTEGER NULL,
    lote_erp                VARCHAR(80) NULL,
    estado_traslado         VARCHAR(50) NULL,

    pais_id                 INTEGER NULL,
    pais_nombre             VARCHAR(120) NULL,
    empresa_nombre          VARCHAR(200) NULL,

    -- Campos Producción
    fecha_inicio_produccion TIMESTAMPTZ NULL,
    hembras_iniciales_prod  INTEGER NULL,
    machos_iniciales_prod   INTEGER NULL,
    huevos_iniciales        INTEGER NULL,
    tipo_nido               VARCHAR(50) NULL,
    nucleo_p                VARCHAR(100) NULL,
    ciclo_produccion        VARCHAR(50) NULL,
    fecha_fin_produccion    TIMESTAMPTZ NULL,
    aves_fin_hembras_prod   INTEGER NULL,
    aves_fin_machos_prod    INTEGER NULL,

    -- Clasificación de huevos
    huevo_tot               INTEGER NULL,
    huevo_inc               INTEGER NULL,
    huevo_limpio            INTEGER NULL,
    huevo_tratado           INTEGER NULL,
    huevo_sucio             INTEGER NULL,
    huevo_deforme           INTEGER NULL,
    huevo_blanco            INTEGER NULL,
    huevo_doble_yema        INTEGER NULL,
    huevo_piso              INTEGER NULL,
    huevo_pequeno           INTEGER NULL,
    huevo_roto              INTEGER NULL,
    huevo_desecho           INTEGER NULL,
    huevo_otro              INTEGER NULL,
    peso_huevo              NUMERIC(18,4) NULL,

    -- Campos específicos postura producción
    lote_id                 INTEGER NULL,
    lote_padre_id           INTEGER NULL,
    lote_postura_levante_id INTEGER NULL,
    aves_h_inicial          INTEGER NULL,
    aves_m_inicial          INTEGER NULL,
    aves_h_actual           INTEGER NULL,
    aves_m_actual           INTEGER NULL,
    empresa_id              INTEGER NULL,
    usuario_id              INTEGER NULL,
    estado                  VARCHAR(50) NULL,
    etapa                   VARCHAR(50) NULL,
    edad                    INTEGER NULL,

    -- Auditoría (AuditableEntity)
    company_id              INTEGER NOT NULL,
    created_by_user_id      INTEGER NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id      INTEGER NULL,
    updated_at              TIMESTAMPTZ NULL,
    deleted_at              TIMESTAMPTZ NULL,

    CONSTRAINT fk_lpp_granja FOREIGN KEY (granja_id) REFERENCES public.farms(id) ON DELETE RESTRICT,
    CONSTRAINT fk_lpp_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_lpp_lote FOREIGN KEY (lote_id) REFERENCES public.lotes(lote_id) ON DELETE RESTRICT,
    CONSTRAINT fk_lpp_lote_postura_levante FOREIGN KEY (lote_postura_levante_id) REFERENCES public.lote_postura_levante(lote_postura_levante_id) ON DELETE RESTRICT,
    CONSTRAINT ck_lpp_nonneg_counts CHECK (
        (hembras_l >= 0 OR hembras_l IS NULL) AND (machos_l >= 0 OR machos_l IS NULL)
        AND (mixtas >= 0 OR mixtas IS NULL) AND (aves_encasetadas >= 0 OR aves_encasetadas IS NULL)
    ),
    CONSTRAINT ck_lpp_nonneg_pesos CHECK (
        (peso_inicial_h >= 0 OR peso_inicial_h IS NULL) AND (peso_inicial_m >= 0 OR peso_inicial_m IS NULL)
        AND (peso_mixto >= 0 OR peso_mixto IS NULL)
    )
);

-- Índices lote_postura_produccion
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_granja ON public.lote_postura_produccion(granja_id);
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_nucleo ON public.lote_postura_produccion(nucleo_id) WHERE nucleo_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_galpon ON public.lote_postura_produccion(galpon_id) WHERE galpon_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_lote ON public.lote_postura_produccion(lote_id) WHERE lote_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_levante ON public.lote_postura_produccion(lote_postura_levante_id) WHERE lote_postura_levante_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_lote_postura_produccion_company_deleted ON public.lote_postura_produccion(company_id) WHERE deleted_at IS NULL;

COMMENT ON TABLE public.lote_postura_produccion IS 'Lotes de postura etapa Producción. Incluye clasificación de huevos.';

-- 3. HISTORICO_LOTE_POSTURA (auditoría: cada registro creado genera entrada)
CREATE TABLE IF NOT EXISTS public.historico_lote_postura (
    id                          SERIAL PRIMARY KEY,
    company_id                  INTEGER NOT NULL,
    tipo_lote                   VARCHAR(32) NOT NULL,
    lote_postura_levante_id     INTEGER NULL,
    lote_postura_produccion_id  INTEGER NULL,
    tipo_registro               VARCHAR(24) NOT NULL DEFAULT 'Creacion',
    fecha_registro              TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    usuario_id                  INTEGER NULL,
    snapshot                    JSONB NULL,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),

    CONSTRAINT fk_hlp_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_hlp_lote_postura_levante FOREIGN KEY (lote_postura_levante_id) REFERENCES public.lote_postura_levante(lote_postura_levante_id) ON DELETE CASCADE,
    CONSTRAINT fk_hlp_lote_postura_produccion FOREIGN KEY (lote_postura_produccion_id) REFERENCES public.lote_postura_produccion(lote_postura_produccion_id) ON DELETE CASCADE,
    CONSTRAINT ck_hlp_tipo_lote CHECK (tipo_lote IN ('LotePosturaLevante', 'LotePosturaProduccion')),
    CONSTRAINT ck_hlp_tipo_registro CHECK (tipo_registro IN ('Creacion', 'Actualizacion')),
    CONSTRAINT ck_hlp_lote_ref CHECK (
        (tipo_lote = 'LotePosturaLevante' AND lote_postura_levante_id IS NOT NULL AND lote_postura_produccion_id IS NULL)
        OR (tipo_lote = 'LotePosturaProduccion' AND lote_postura_levante_id IS NULL AND lote_postura_produccion_id IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_historico_lote_postura_company ON public.historico_lote_postura(company_id);
CREATE INDEX IF NOT EXISTS ix_historico_lote_postura_tipo ON public.historico_lote_postura(tipo_lote);
CREATE INDEX IF NOT EXISTS ix_historico_lote_postura_levante ON public.historico_lote_postura(lote_postura_levante_id) WHERE lote_postura_levante_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_historico_lote_postura_produccion ON public.historico_lote_postura(lote_postura_produccion_id) WHERE lote_postura_produccion_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_historico_lote_postura_fecha ON public.historico_lote_postura(fecha_registro);

COMMENT ON TABLE public.historico_lote_postura IS 'Historial de lotes postura. Cada registro creado/actualizado en levante o producción genera entrada aquí.';
COMMENT ON COLUMN public.historico_lote_postura.tipo_lote IS 'LotePosturaLevante o LotePosturaProduccion.';
COMMENT ON COLUMN public.historico_lote_postura.tipo_registro IS 'Creacion = al crear; Actualizacion = al modificar.';
COMMENT ON COLUMN public.historico_lote_postura.snapshot IS 'JSON con snapshot del registro al momento de la operación.';

-- 4. Triggers para insertar en historico_lote_postura al crear/actualizar
CREATE OR REPLACE FUNCTION trg_historico_lote_postura_levante()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.historico_lote_postura (
        company_id, tipo_lote, lote_postura_levante_id, lote_postura_produccion_id,
        tipo_registro, fecha_registro, usuario_id, snapshot
    ) VALUES (
        NEW.company_id, 'LotePosturaLevante', NEW.lote_postura_levante_id, NULL,
        CASE WHEN TG_OP = 'INSERT' THEN 'Creacion' ELSE 'Actualizacion' END,
        NOW() AT TIME ZONE 'utc',
        COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
        to_jsonb(NEW)
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION trg_historico_lote_postura_produccion()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.historico_lote_postura (
        company_id, tipo_lote, lote_postura_levante_id, lote_postura_produccion_id,
        tipo_registro, fecha_registro, usuario_id, snapshot
    ) VALUES (
        NEW.company_id, 'LotePosturaProduccion', NULL, NEW.lote_postura_produccion_id,
        CASE WHEN TG_OP = 'INSERT' THEN 'Creacion' ELSE 'Actualizacion' END,
        NOW() AT TIME ZONE 'utc',
        COALESCE(NEW.updated_by_user_id, NEW.created_by_user_id),
        to_jsonb(NEW)
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_hlp_lote_postura_levante ON public.lote_postura_levante;
CREATE TRIGGER trg_hlp_lote_postura_levante
    AFTER INSERT OR UPDATE ON public.lote_postura_levante
    FOR EACH ROW EXECUTE PROCEDURE trg_historico_lote_postura_levante();

DROP TRIGGER IF EXISTS trg_hlp_lote_postura_produccion ON public.lote_postura_produccion;
CREATE TRIGGER trg_hlp_lote_postura_produccion
    AFTER INSERT OR UPDATE ON public.lote_postura_produccion
    FOR EACH ROW EXECUTE PROCEDURE trg_historico_lote_postura_produccion();
