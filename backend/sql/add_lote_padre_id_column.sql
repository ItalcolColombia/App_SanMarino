-- ============================================================
-- Agregar columna lote_padre_id a la tabla lotes
-- ============================================================

-- Verificar si la columna ya existe
DO $$
DECLARE
    pk_exists BOOLEAN;
    unique_exists BOOLEAN;
BEGIN
    -- Verificar si existe PRIMARY KEY en lote_id
    SELECT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu 
            ON tc.constraint_name = kcu.constraint_name
        WHERE tc.table_schema = 'public' 
        AND tc.table_name = 'lotes'
        AND tc.constraint_type = 'PRIMARY KEY'
        AND kcu.column_name = 'lote_id'
    ) INTO pk_exists;
    
    -- Verificar si existe UNIQUE constraint en lote_id
    SELECT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu 
            ON tc.constraint_name = kcu.constraint_name
        WHERE tc.table_schema = 'public' 
        AND tc.table_name = 'lotes'
        AND tc.constraint_type = 'UNIQUE'
        AND kcu.column_name = 'lote_id'
    ) INTO unique_exists;
    
    -- Si no existe PRIMARY KEY ni UNIQUE, crear PRIMARY KEY
    IF NOT pk_exists AND NOT unique_exists THEN
        -- Verificar que no haya valores NULL en lote_id
        IF EXISTS (SELECT 1 FROM public.lotes WHERE lote_id IS NULL) THEN
            RAISE EXCEPTION 'No se puede crear PRIMARY KEY: existen valores NULL en lote_id';
        END IF;
        
        -- Verificar que no haya valores duplicados en lote_id
        IF EXISTS (
            SELECT lote_id, COUNT(*) 
            FROM public.lotes 
            WHERE lote_id IS NOT NULL
            GROUP BY lote_id 
            HAVING COUNT(*) > 1
        ) THEN
            RAISE EXCEPTION 'No se puede crear PRIMARY KEY: existen valores duplicados en lote_id';
        END IF;
        
        -- Crear PRIMARY KEY si no existe
        ALTER TABLE public.lotes 
        ADD CONSTRAINT pk_lotes_lote_id PRIMARY KEY (lote_id);
        RAISE NOTICE 'PRIMARY KEY creado en lote_id';
    END IF;
    
    -- Ahora agregar la columna lote_padre_id si no existe
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lotes' 
        AND column_name = 'lote_padre_id'
    ) THEN
        -- Agregar la columna
        ALTER TABLE public.lotes 
        ADD COLUMN lote_padre_id INTEGER NULL;
        
        -- Agregar comentario a la columna
        COMMENT ON COLUMN public.lotes.lote_padre_id IS 'ID del lote padre para consolidación de reportes';
        
        -- Crear índice para mejorar el rendimiento de las consultas
        CREATE INDEX IF NOT EXISTS ix_lote_padre 
        ON public.lotes(lote_padre_id);
        
        -- Agregar foreign key constraint
        ALTER TABLE public.lotes
        ADD CONSTRAINT fk_lote_padre 
        FOREIGN KEY (lote_padre_id) 
        REFERENCES public.lotes(lote_id) 
        ON DELETE RESTRICT;
        
        RAISE NOTICE 'Columna lote_padre_id agregada exitosamente';
    ELSE
        RAISE NOTICE 'La columna lote_padre_id ya existe';
    END IF;
END $$;

-- Verificar que la columna se creó correctamente
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns 
WHERE table_schema = 'public' 
AND table_name = 'lotes' 
AND column_name = 'lote_padre_id';

