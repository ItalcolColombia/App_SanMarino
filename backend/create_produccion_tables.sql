-- Crear tabla produccion_lote
CREATE TABLE IF NOT EXISTS produccion_lote (
    id SERIAL PRIMARY KEY,
    lote_id INTEGER NOT NULL,
    fecha_inicio TIMESTAMP WITH TIME ZONE NOT NULL,
    aves_iniciales_h INTEGER NOT NULL,
    aves_iniciales_m INTEGER NOT NULL,
    observaciones TEXT,
    company_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uk_produccion_lote_lote_id UNIQUE (lote_id)
);

-- Crear tabla produccion_seguimiento
CREATE TABLE IF NOT EXISTS produccion_seguimiento (
    id SERIAL PRIMARY KEY,
    produccion_lote_id INTEGER NOT NULL,
    fecha_registro TIMESTAMP WITH TIME ZONE NOT NULL,
    mortalidad_h INTEGER NOT NULL,
    mortalidad_m INTEGER NOT NULL,
    consumo_kg NUMERIC(18,2) NOT NULL,
    huevos_totales INTEGER NOT NULL,
    huevos_incubables INTEGER NOT NULL,
    peso_huevo NUMERIC(18,2) NOT NULL,
    observaciones TEXT,
    company_id INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uk_produccion_seguimiento_lote_fecha UNIQUE (produccion_lote_id, fecha_registro),
    CONSTRAINT fk_produccion_seguimiento_lote FOREIGN KEY (produccion_lote_id) REFERENCES produccion_lote(id) ON DELETE CASCADE
);

-- Crear índices
CREATE INDEX IF NOT EXISTS idx_produccion_lote_lote_id ON produccion_lote(lote_id);
CREATE INDEX IF NOT EXISTS idx_produccion_lote_company_id ON produccion_lote(company_id);
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_lote_id ON produccion_seguimiento(produccion_lote_id);
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_fecha ON produccion_seguimiento(fecha_registro);
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_company_id ON produccion_seguimiento(company_id);



