-- Añadir logo de empresa guardado en BD
ALTER TABLE public.companies
ADD COLUMN IF NOT EXISTS logo_bytes BYTEA NULL,
ADD COLUMN IF NOT EXISTS logo_content_type VARCHAR(100) NULL;

COMMENT ON COLUMN public.companies.logo_bytes IS 'Logo de la empresa en bytes (para mostrar en menú y reportes).';
COMMENT ON COLUMN public.companies.logo_content_type IS 'Content-Type del logo (image/png, image/jpeg, etc.).';

