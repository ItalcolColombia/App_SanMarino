-- Script para agregar el campo estado_traslado a la tabla lotes
-- Ejecutar este script en la base de datos antes de usar la funcionalidad de traslado

-- Agregar columna estado_traslado si no existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'lotes' 
        AND column_name = 'estado_traslado'
    ) THEN
        ALTER TABLE public.lotes 
        ADD COLUMN estado_traslado VARCHAR(50) NULL;
        
        -- Crear índice para mejorar consultas por estado
        CREATE INDEX IF NOT EXISTS idx_lotes_estado_traslado 
        ON public.lotes(estado_traslado) 
        WHERE estado_traslado IS NOT NULL;
        
        RAISE NOTICE 'Columna estado_traslado agregada exitosamente a la tabla lotes';
    ELSE
        RAISE NOTICE 'La columna estado_traslado ya existe en la tabla lotes';
    END IF;
END $$;

-- Comentario en la columna
COMMENT ON COLUMN public.lotes.estado_traslado IS 'Estado del traslado: NULL/"normal" = lote normal, "trasladado" = lote original que fue trasladado, "en_transferencia" = nuevo lote en granja destino';

