-- =====================================================
-- Script para agregar columna item_type a catalogo_items
-- =====================================================
-- Este script agrega la columna item_type como campo separado (no en JSONB)
-- para facilitar el filtrado y consultas

-- 1. Agregar columna item_type
ALTER TABLE public.catalogo_items
ADD COLUMN IF NOT EXISTS item_type VARCHAR(50) DEFAULT 'alimento';

-- 2. Comentario para documentación
COMMENT ON COLUMN public.catalogo_items.item_type IS 'Tipo de item del catálogo: alimento, vacuna, medicamento, accesorio, biologico, consumible, otro';

-- 3. Actualizar registros existentes que tengan type_item en metadata
UPDATE public.catalogo_items
SET item_type = COALESCE(
    (metadata->>'type_item')::VARCHAR,
    'alimento'
)
WHERE item_type IS NULL OR item_type = 'alimento';

-- 4. Agregar índice para mejorar el rendimiento de las consultas
CREATE INDEX IF NOT EXISTS ix_catalogo_items_item_type ON public.catalogo_items(item_type);
CREATE INDEX IF NOT EXISTS ix_catalogo_items_company_type ON public.catalogo_items(company_id, item_type);
CREATE INDEX IF NOT EXISTS ix_catalogo_items_company_type_activo ON public.catalogo_items(company_id, item_type, activo) WHERE activo = true;

-- 5. Hacer la columna NOT NULL después de actualizar los datos existentes
ALTER TABLE public.catalogo_items ALTER COLUMN item_type SET NOT NULL;

-- Verificar que se agregó correctamente
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'catalogo_items'
  AND column_name = 'item_type';
