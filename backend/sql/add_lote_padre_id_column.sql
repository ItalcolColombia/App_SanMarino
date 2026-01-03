-- Script SQL para agregar la columna lote_padre_id a la tabla lotes
-- Ejecutar este script en tu base de datos PostgreSQL

-- Agregar columna lote_padre_id si no existe
DO $$
DECLARE
    pk_exists BOOLEAN;
    unique_exists BOOLEAN;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lotes' 
        AND column_name = 'lote_padre_id'
    ) THEN
        -- Agregar la columna como nullable integer
        ALTER TABLE public.lotes 
        ADD COLUMN lote_padre_id INTEGER NULL;
        
        -- Agregar índice para mejorar el rendimiento de las consultas
        CREATE INDEX IF NOT EXISTS ix_lote_padre ON public.lotes(lote_padre_id);
        
        -- Agregar foreign key constraint para la relación self-referencial
        ALTER TABLE public.lotes 
        ADD CONSTRAINT fk_lote_lote_padre 
        FOREIGN KEY (lote_padre_id) 
        REFERENCES public.lotes(lote_id) 
        ON DELETE RESTRICT;
        
        RAISE NOTICE 'Columna lote_padre_id agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna lote_padre_id ya existe';
    END IF;
END $$;
