-- Añadir columna codigo_plantilla a mapa (plantillas: granjas_huevos_alimento, entrada_ciesa)
ALTER TABLE public.mapa
ADD COLUMN IF NOT EXISTS codigo_plantilla VARCHAR(80) NULL;

COMMENT ON COLUMN public.mapa.codigo_plantilla IS 'Código de plantilla para estructura de encabezado: granjas_huevos_alimento, entrada_ciesa, etc.';
