-- ============================================================
-- Crear tabla produccion_seguimiento
-- ============================================================

CREATE TABLE IF NOT EXISTS produccion_seguimiento (
    id SERIAL PRIMARY KEY,
    produccion_lote_id INTEGER NOT NULL,
    fecha_registro DATE NOT NULL,
    mortalidad_h INTEGER NOT NULL DEFAULT 0,
    mortalidad_m INTEGER NOT NULL DEFAULT 0,
    consumo_kg NUMERIC(10, 2) NOT NULL DEFAULT 0,
    huevos_totales INTEGER NOT NULL DEFAULT 0,
    huevos_incubables INTEGER NOT NULL DEFAULT 0,
    peso_huevo NUMERIC(8, 2) NOT NULL DEFAULT 0,
    observaciones VARCHAR(1000),
    
    -- Campos de auditoría
    company_id INTEGER,
    created_by_user_id INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Foreign key constraint
    CONSTRAINT fk_produccion_seguimiento_lote 
        FOREIGN KEY (produccion_lote_id) 
        REFERENCES produccion_lotes(id) 
        ON DELETE CASCADE
);

-- Índice único para asegurar un solo registro por lote y fecha
CREATE UNIQUE INDEX IF NOT EXISTS uq_produccion_seguimiento_lote_fecha 
    ON produccion_seguimiento(produccion_lote_id, fecha_registro);

-- Índices adicionales
CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_fecha_registro 
    ON produccion_seguimiento(fecha_registro);

CREATE INDEX IF NOT EXISTS idx_produccion_seguimiento_lote_id 
    ON produccion_seguimiento(produccion_lote_id);

-- Verificar que la tabla se creó correctamente
SELECT column_name, data_type, column_default, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_seguimiento' 
ORDER BY ordinal_position;
