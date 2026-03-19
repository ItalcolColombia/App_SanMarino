-- Tablas: Guía genética Ecuador (por empresa + raza + año; detalle por día y sexo: mixto/hembra/macho).
-- Ejecutar en PostgreSQL si no usas migraciones EF para estas tablas.

CREATE TABLE IF NOT EXISTS public.guia_genetica_ecuador_header (
    id SERIAL PRIMARY KEY,
    raza VARCHAR(120) NOT NULL,
    anio_guia INTEGER NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'active',
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id INTEGER NULL,
    updated_at TIMESTAMPTZ NULL,
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_gge_header_company_raza_anio UNIQUE (company_id, raza, anio_guia),
    CONSTRAINT fk_gge_header_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS public.guia_genetica_ecuador_detalle (
    id SERIAL PRIMARY KEY,
    guia_genetica_ecuador_header_id INTEGER NOT NULL,
    sexo VARCHAR(20) NOT NULL,
    dia INTEGER NOT NULL,
    peso_corporal_g NUMERIC(18,3) NOT NULL DEFAULT 0,
    ganancia_diaria_g NUMERIC(18,3) NOT NULL DEFAULT 0,
    promedio_ganancia_diaria_g NUMERIC(18,3) NOT NULL DEFAULT 0,
    cantidad_alimento_diario_g NUMERIC(18,3) NOT NULL DEFAULT 0,
    alimento_acumulado_g NUMERIC(18,3) NOT NULL DEFAULT 0,
    ca NUMERIC(18,6) NOT NULL DEFAULT 0,
    mortalidad_seleccion_diaria NUMERIC(18,6) NOT NULL DEFAULT 0,
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id INTEGER NULL,
    updated_at TIMESTAMPTZ NULL,
    deleted_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_gge_det_header_sexo_dia UNIQUE (guia_genetica_ecuador_header_id, sexo, dia),
    CONSTRAINT fk_gge_det_header FOREIGN KEY (guia_genetica_ecuador_header_id)
        REFERENCES public.guia_genetica_ecuador_header(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_gge_det_header_id ON public.guia_genetica_ecuador_detalle (guia_genetica_ecuador_header_id);
CREATE INDEX IF NOT EXISTS ix_gge_header_company_id ON public.guia_genetica_ecuador_header (company_id);

COMMENT ON TABLE public.guia_genetica_ecuador_header IS 'Encabezado guía genética Ecuador (company + raza + año).';
COMMENT ON TABLE public.guia_genetica_ecuador_detalle IS 'Curva por día y sexo (mixto/hembra/macho).';
