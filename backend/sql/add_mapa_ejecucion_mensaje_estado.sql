-- Añade columnas de progreso a mapa_ejecucion (mensaje y paso actual/total para barra de progreso)
ALTER TABLE public.mapa_ejecucion
ADD COLUMN IF NOT EXISTS mensaje_estado VARCHAR(200),
ADD COLUMN IF NOT EXISTS paso_actual INTEGER,
ADD COLUMN IF NOT EXISTS total_pasos INTEGER;

COMMENT ON COLUMN public.mapa_ejecucion.mensaje_estado IS 'Progreso actual durante la ejecución (ej. Paso 2/5: Extracción)';
COMMENT ON COLUMN public.mapa_ejecucion.paso_actual IS 'Número de paso actual (1-based) cuando estado es en_proceso';
COMMENT ON COLUMN public.mapa_ejecucion.total_pasos IS 'Total de pasos para barra de progreso';
