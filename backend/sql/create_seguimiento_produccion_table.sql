-- Script para crear la tabla seguimiento_produccion
-- Ejecutar directamente en la base de datos PostgreSQL

CREATE TABLE IF NOT EXISTS seguimiento_produccion (
    id SERIAL PRIMARY KEY,
    fecha TIMESTAMP WITH TIME ZONE NOT NULL,
    lote_id INTEGER NOT NULL,
    mortalidad_h INTEGER NOT NULL DEFAULT 0,
    mortalidad_m INTEGER NOT NULL DEFAULT 0,
    sel_h INTEGER NOT NULL DEFAULT 0,
    cons_kg_h NUMERIC(12,3) NOT NULL DEFAULT 0,
    cons_kg_m NUMERIC(12,3) NOT NULL DEFAULT 0,
    huevo_tot INTEGER NOT NULL DEFAULT 0,
    huevo_inc INTEGER NOT NULL DEFAULT 0,
    tipo_alimento VARCHAR(100) NOT NULL,
    observaciones VARCHAR(1000),
    peso_huevo NUMERIC(8,2) NOT NULL DEFAULT 0,
    etapa INTEGER NOT NULL DEFAULT 1,
    
    -- Foreign key constraint
    CONSTRAINT fk_seguimiento_produccion_lotes_lote_id 
        FOREIGN KEY (lote_id) 
        REFERENCES lotes(lote_id) 
        ON DELETE RESTRICT
);

-- Crear índice único por lote y fecha
CREATE UNIQUE INDEX IF NOT EXISTS ix_seguimiento_produccion_lote_id_fecha 
    ON seguimiento_produccion (lote_id, fecha);

-- Crear índice por lote_id para consultas rápidas
CREATE INDEX IF NOT EXISTS ix_seguimiento_produccion_lote_id 
    ON seguimiento_produccion (lote_id);

-- Comentarios para documentar la tabla
COMMENT ON TABLE seguimiento_produccion IS 'Registros diarios de seguimiento de producción avícola';
COMMENT ON COLUMN seguimiento_produccion.id IS 'Identificador único del registro';
COMMENT ON COLUMN seguimiento_produccion.fecha IS 'Fecha del registro de producción';
COMMENT ON COLUMN seguimiento_produccion.lote_id IS 'ID del lote asociado';
COMMENT ON COLUMN seguimiento_produccion.mortalidad_h IS 'Mortalidad de hembras';
COMMENT ON COLUMN seguimiento_produccion.mortalidad_m IS 'Mortalidad de machos';
COMMENT ON COLUMN seguimiento_produccion.sel_h IS 'Selección de hembras';
COMMENT ON COLUMN seguimiento_produccion.cons_kg_h IS 'Consumo de alimento hembras (kg)';
COMMENT ON COLUMN seguimiento_produccion.cons_kg_m IS 'Consumo de alimento machos (kg)';
COMMENT ON COLUMN seguimiento_produccion.huevo_tot IS 'Total de huevos';
COMMENT ON COLUMN seguimiento_produccion.huevo_inc IS 'Huevos incubables';
COMMENT ON COLUMN seguimiento_produccion.tipo_alimento IS 'Tipo de alimento utilizado';
COMMENT ON COLUMN seguimiento_produccion.observaciones IS 'Observaciones adicionales';
COMMENT ON COLUMN seguimiento_produccion.peso_huevo IS 'Peso promedio del huevo (g)';
COMMENT ON COLUMN seguimiento_produccion.etapa IS 'Etapa de producción (1, 2, 3)';



