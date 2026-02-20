-- =====================================================
-- Script para agregar campos company_id y pais_id a catalogo_items
-- =====================================================
-- Este script agrega los campos necesarios para filtrar productos del catálogo por empresa y país

-- 1. Agregar columnas company_id y pais_id
ALTER TABLE public.catalogo_items
ADD COLUMN IF NOT EXISTS company_id INTEGER,
ADD COLUMN IF NOT EXISTS pais_id INTEGER;

-- 2. Comentarios para documentación
COMMENT ON COLUMN public.catalogo_items.company_id IS 'ID de la empresa a la que pertenece el producto del catálogo';
COMMENT ON COLUMN public.catalogo_items.pais_id IS 'ID del país asociado al producto del catálogo';

-- 3. Actualizar registros existentes (si hay datos)
-- Si no hay empresa/pais en sesión, se puede dejar NULL temporalmente
-- O asignar un valor por defecto según tu lógica de negocio
-- UPDATE public.catalogo_items SET company_id = 1, pais_id = 1 WHERE company_id IS NULL;

-- 4. Eliminar el índice único global del código (si existe) para permitir códigos duplicados entre empresas
DROP INDEX IF EXISTS public.ux_catalogo_items_codigo;

-- 5. Crear nuevo índice único compuesto: código debe ser único por empresa y país
CREATE UNIQUE INDEX IF NOT EXISTS ux_catalogo_items_codigo_company_pais 
ON public.catalogo_items(company_id, pais_id, codigo);

-- 6. Agregar índices para mejorar el rendimiento de las consultas
CREATE INDEX IF NOT EXISTS ix_catalogo_items_company_id ON public.catalogo_items(company_id);
CREATE INDEX IF NOT EXISTS ix_catalogo_items_pais_id ON public.catalogo_items(pais_id);
CREATE INDEX IF NOT EXISTS ix_catalogo_items_company_pais ON public.catalogo_items(company_id, pais_id);
CREATE INDEX IF NOT EXISTS ix_catalogo_items_company_activo ON public.catalogo_items(company_id, activo) WHERE activo = true;

-- 5. Agregar foreign keys (opcional, según tu modelo de datos)
-- ALTER TABLE public.catalogo_items
-- ADD CONSTRAINT fk_catalogo_items_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE RESTRICT;
-- ALTER TABLE public.catalogo_items
-- ADD CONSTRAINT fk_catalogo_items_pais FOREIGN KEY (pais_id) REFERENCES public.paises(id) ON DELETE RESTRICT;

-- 6. Hacer las columnas NOT NULL después de actualizar los datos existentes
-- IMPORTANTE: Descomenta estas líneas solo después de actualizar todos los registros existentes
-- ALTER TABLE public.catalogo_items ALTER COLUMN company_id SET NOT NULL;
-- ALTER TABLE public.catalogo_items ALTER COLUMN pais_id SET NOT NULL;

-- Verificar que se agregaron correctamente
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public' 
  AND table_name = 'catalogo_items'
  AND column_name IN ('company_id', 'pais_id')
ORDER BY column_name;
