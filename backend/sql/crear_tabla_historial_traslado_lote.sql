-- Script para crear la tabla de historial de traslados de lotes
-- Ejecutar después de agregar la columna estado_traslado a la tabla lotes

CREATE TABLE IF NOT EXISTS public.historial_traslado_lote (
    id SERIAL PRIMARY KEY,
    lote_original_id INTEGER NOT NULL,
    lote_nuevo_id INTEGER NOT NULL,
    granja_origen_id INTEGER NOT NULL,
    granja_destino_id INTEGER NOT NULL,
    nucleo_destino_id VARCHAR(50),
    galpon_destino_id VARCHAR(50),
    observaciones VARCHAR(1000),
    company_id INTEGER NOT NULL,
    created_by_user_id INTEGER NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Nota: Las foreign keys se crean después de verificar que las tablas referenciadas existen
    -- y que las columnas referenciadas son claves primarias o tienen restricciones únicas
);

-- Índices adicionales
CREATE INDEX IF NOT EXISTS idx_historial_traslado_lote_original ON public.historial_traslado_lote(lote_original_id);
CREATE INDEX IF NOT EXISTS idx_historial_traslado_lote_nuevo ON public.historial_traslado_lote(lote_nuevo_id);
CREATE INDEX IF NOT EXISTS idx_historial_traslado_company ON public.historial_traslado_lote(company_id);
CREATE INDEX IF NOT EXISTS idx_historial_traslado_created_at ON public.historial_traslado_lote(created_at);

-- Foreign keys (creadas después de verificar que las tablas y columnas existen)
-- Verificar que lote_id es la clave primaria de lotes
DO $$
BEGIN
    -- Verificar si existe la tabla lotes y si lote_id es PK
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE table_schema = 'public' 
        AND table_name = 'lotes' 
        AND constraint_type = 'PRIMARY KEY'
    ) THEN
        -- Agregar foreign key para lote_original_id
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints 
            WHERE constraint_name = 'fk_historial_lote_original'
        ) THEN
            ALTER TABLE public.historial_traslado_lote
            ADD CONSTRAINT fk_historial_lote_original 
            FOREIGN KEY (lote_original_id) 
            REFERENCES public.lotes(lote_id) ON DELETE RESTRICT;
        END IF;

        -- Agregar foreign key para lote_nuevo_id
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints 
            WHERE constraint_name = 'fk_historial_lote_nuevo'
        ) THEN
            ALTER TABLE public.historial_traslado_lote
            ADD CONSTRAINT fk_historial_lote_nuevo 
            FOREIGN KEY (lote_nuevo_id) 
            REFERENCES public.lotes(lote_id) ON DELETE RESTRICT;
        END IF;
    END IF;

    -- Verificar que farms existe y agregar foreign keys
    IF EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name = 'farms'
    ) THEN
        -- Foreign key para granja_origen_id
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints 
            WHERE constraint_name = 'fk_historial_granja_origen'
        ) THEN
            ALTER TABLE public.historial_traslado_lote
            ADD CONSTRAINT fk_historial_granja_origen 
            FOREIGN KEY (granja_origen_id) 
            REFERENCES public.farms(id) ON DELETE RESTRICT;
        END IF;

        -- Foreign key para granja_destino_id
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.table_constraints 
            WHERE constraint_name = 'fk_historial_granja_destino'
        ) THEN
            ALTER TABLE public.historial_traslado_lote
            ADD CONSTRAINT fk_historial_granja_destino 
            FOREIGN KEY (granja_destino_id) 
            REFERENCES public.farms(id) ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;

-- Comentarios en la tabla
COMMENT ON TABLE public.historial_traslado_lote IS 'Registra el historial de traslados de lotes entre granjas';
COMMENT ON COLUMN public.historial_traslado_lote.lote_original_id IS 'ID del lote original que fue trasladado';
COMMENT ON COLUMN public.historial_traslado_lote.lote_nuevo_id IS 'ID del nuevo lote creado en la granja destino';
COMMENT ON COLUMN public.historial_traslado_lote.granja_origen_id IS 'ID de la granja de origen';
COMMENT ON COLUMN public.historial_traslado_lote.granja_destino_id IS 'ID de la granja destino';
COMMENT ON COLUMN public.historial_traslado_lote.nucleo_destino_id IS 'ID del núcleo destino (opcional)';
COMMENT ON COLUMN public.historial_traslado_lote.galpon_destino_id IS 'ID del galpón destino (opcional)';
COMMENT ON COLUMN public.historial_traslado_lote.observaciones IS 'Observaciones adicionales sobre el traslado';

