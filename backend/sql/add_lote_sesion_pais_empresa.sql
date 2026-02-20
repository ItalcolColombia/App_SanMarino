-- Guardar en registro de lote: usuario en sesión, país y empresa (desde storage/headers)
-- Ejecutar una sola vez sobre la tabla lotes.

ALTER TABLE public.lotes
  ADD COLUMN IF NOT EXISTS pais_id integer NULL,
  ADD COLUMN IF NOT EXISTS pais_nombre character varying(120) NULL,
  ADD COLUMN IF NOT EXISTS empresa_nombre character varying(200) NULL;

COMMENT ON COLUMN public.lotes.pais_id IS 'ID del país en sesión al crear el lote (desde storage/header X-Active-Pais)';
COMMENT ON COLUMN public.lotes.pais_nombre IS 'Nombre del país en sesión al crear el lote';
COMMENT ON COLUMN public.lotes.empresa_nombre IS 'Nombre de la empresa en sesión al crear el lote (X-Active-Company)';
-- company_id (heredado de auditable) = ID de la empresa en sesión (X-Active-Company-Id / GetEffectiveCompanyIdAsync).
