-- =============================================================================
-- Tablas del módulo Mapas (documentos de mapeo para ERP/CIESA)
-- =============================================================================

-- mapa: definición del mapa (nombre, descripción, alcance company/pais)
CREATE TABLE IF NOT EXISTS public.mapa (
    id                  SERIAL PRIMARY KEY,
    nombre              VARCHAR(200) NOT NULL,
    descripcion         TEXT,
    company_id          INTEGER NOT NULL,
    pais_id             INTEGER,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    created_by_user_id  UUID NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_by_user_id  UUID,
    updated_at          TIMESTAMPTZ,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT fk_mapa_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mapa_pais FOREIGN KEY (pais_id) REFERENCES public.paises(pais_id) ON DELETE SET NULL,
    CONSTRAINT fk_mapa_created_by FOREIGN KEY (created_by_user_id) REFERENCES public.users(id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_mapa_company ON public.mapa(company_id);
CREATE INDEX IF NOT EXISTS ix_mapa_deleted_at ON public.mapa(deleted_at) WHERE deleted_at IS NULL;

COMMENT ON TABLE public.mapa IS 'Definición de un mapa (documento de mapeo para exportar datos a ERP/CIESA)';

-- mapa_paso: pasos del mapa (head, extraction, transformation, execute, export)
CREATE TABLE IF NOT EXISTS public.mapa_paso (
    id                  SERIAL PRIMARY KEY,
    mapa_id             INTEGER NOT NULL,
    orden               INTEGER NOT NULL DEFAULT 1,
    tipo                VARCHAR(30) NOT NULL,  -- 'head','extraction','transformation','execute','export'
    nombre_etiqueta     VARCHAR(100),         -- ej. extraction_1, transformation_1
    script_sql          TEXT,
    opciones            JSONB DEFAULT '{}',   -- formato export pdf/excel, etc.
    created_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    updated_at          TIMESTAMPTZ,
    CONSTRAINT fk_mapa_paso_mapa FOREIGN KEY (mapa_id) REFERENCES public.mapa(id) ON DELETE CASCADE,
    CONSTRAINT chk_mapa_paso_tipo CHECK (tipo IN ('head','extraction','transformation','execute','export'))
);

CREATE INDEX IF NOT EXISTS ix_mapa_paso_mapa ON public.mapa_paso(mapa_id);

COMMENT ON TABLE public.mapa_paso IS 'Pasos de un mapa: head, extraction, transformation, execute, export';

-- mapa_ejecucion: historial de cada ejecución del mapa
CREATE TABLE IF NOT EXISTS public.mapa_ejecucion (
    id                  SERIAL PRIMARY KEY,
    mapa_id             INTEGER NOT NULL,
    usuario_id          UUID NOT NULL,
    company_id          INTEGER NOT NULL,
    parametros          JSONB NOT NULL DEFAULT '{}',  -- rango fechas, granjas, tipo dato
    tipo_archivo        VARCHAR(10),                   -- 'pdf','excel'
    resultado_json      JSONB,                        -- payload con el que se generó el archivo
    estado              VARCHAR(20) NOT NULL DEFAULT 'en_proceso',  -- en_proceso, completado, error
    mensaje_error       TEXT,
    mensaje_estado      VARCHAR(200),                               -- progreso actual (ej. Paso 2/5: Extracción)
    paso_actual         INTEGER,                                   -- paso actual (1-based) cuando en_proceso
    total_pasos         INTEGER,                                   -- total pasos para barra de progreso
    fecha_ejecucion     TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    CONSTRAINT fk_mapa_ejecucion_mapa FOREIGN KEY (mapa_id) REFERENCES public.mapa(id) ON DELETE CASCADE,
    CONSTRAINT fk_mapa_ejecucion_usuario FOREIGN KEY (usuario_id) REFERENCES public.users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_mapa_ejecucion_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT,
    CONSTRAINT chk_mapa_ejecucion_estado CHECK (estado IN ('en_proceso','completado','error')),
    CONSTRAINT chk_mapa_ejecucion_tipo_archivo CHECK (tipo_archivo IS NULL OR tipo_archivo IN ('pdf','excel'))
);

CREATE INDEX IF NOT EXISTS ix_mapa_ejecucion_mapa ON public.mapa_ejecucion(mapa_id);
CREATE INDEX IF NOT EXISTS ix_mapa_ejecucion_fecha ON public.mapa_ejecucion(fecha_ejecucion DESC);

COMMENT ON TABLE public.mapa_ejecucion IS 'Historial de ejecuciones de un mapa: usuario, parámetros, resultado, estado';
