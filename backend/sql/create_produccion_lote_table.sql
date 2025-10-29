-- ============================================================
-- Crear tabla produccion_lote completamente
-- ============================================================

-- Verificar si la tabla existe
SELECT EXISTS (
   SELECT FROM information_schema.tables 
   WHERE table_name = 'produccion_lotes'
);

-- Si la tabla NO existe, crearla
CREATE TABLE IF NOT EXISTS produccion_lotes (
    id SERIAL PRIMARY KEY,
    lote_id INTEGER NOT NULL,
    fecha_inicio DATE NOT NULL,
    aves_iniciales_h INTEGER NOT NULL DEFAULT 0,
    aves_iniciales_m INTEGER NOT NULL DEFAULT 0,
    huevos_iniciales INTEGER NOT NULL DEFAULT 0,
    tipo_nido VARCHAR(50) NOT NULL DEFAULT 'Manual',
    nucleo_p VARCHAR(100),
    ciclo VARCHAR(50) NOT NULL DEFAULT 'normal',
    observaciones VARCHAR(1000),
    company_id INTEGER,
    created_by_user_id INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by_user_id INTEGER,
    updated_at TIMESTAMP WITH TIME ZONE,
    deleted_at TIMESTAMP WITH TIME ZONE,
    
    -- Foreign key constraint
    CONSTRAINT fk_produccion_lote_lote 
        FOREIGN KEY (lote_id) 
        REFERENCES lotes(lote_id) 
        ON DELETE RESTRICT,
    
    -- Índice único para asegurar un solo registro inicial por lote
    CONSTRAINT uq_produccion_lotes_lote_id 
        UNIQUE (lote_id)
);

-- Índices adicionales
CREATE INDEX IF NOT EXISTS idx_produccion_lotes_fecha_inicio 
    ON produccion_lotes(fecha_inicio);

CREATE INDEX IF NOT EXISTS idx_produccion_lotes_lote_id 
    ON produccion_lotes(lote_id);

-- Verificar que la tabla se creó correctamente
SELECT column_name, data_type, column_default, is_nullable
FROM information_schema.columns 
WHERE table_name = 'produccion_lotes' 
ORDER BY ordinal_position;

-- Marcar la migración como aplicada
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251014231114_AddProduccionLoteYSeguimiento', '7.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251028224534_AddFieldsToProduccionLote', '7.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

