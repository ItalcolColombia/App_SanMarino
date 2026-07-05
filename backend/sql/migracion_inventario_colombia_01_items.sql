-- ============================================================================
-- Unificación de inventario — Colombia · PASO 1: catálogo de ítems
--   Origen : catalogo_items          (company_id = 1)               [módulo VIEJO]
--   Destino: item_inventario_ecuador (company_id = 1, pais_id = 1)  [módulo NUEVO]
--
-- IDEMPOTENTE: INSERT ... WHERE NOT EXISTS contra el UNIQUE (company_id, pais_id, codigo).
-- Reejecutar no duplica.
--
-- Los 61 ítems de Colombia son ALIMENTO. El módulo nuevo los gestiona a NIVEL GRANJA
-- porque Colombia tiene companies.maneja_alimento_por_galpon = false (no requieren
-- núcleo/galpón). tipo_item se normaliza a minúscula ('alimento') = default de la tabla
-- y valor con el que el módulo detecta alimento. concepto/grupo/etc. quedan NULL (no aplican
-- a Colombia; son campos del catálogo Ecuador).
-- ============================================================================
INSERT INTO public.item_inventario_ecuador
    (codigo, nombre, tipo_item, unidad, activo, company_id, pais_id, concepto, created_at, updated_at)
SELECT
    c.codigo,
    c.nombre,
    lower(c.item_type),   -- 'Alimento'/'alimento' -> 'alimento' (faithful, un solo valor)
    'kg',
    c.activo,
    1,                    -- company_id Colombia
    1,                    -- pais_id   Colombia
    NULL,                 -- concepto (no aplica a Colombia)
    now(), now()
FROM public.catalogo_items c
WHERE c.company_id = 1
  AND NOT EXISTS (
      SELECT 1 FROM public.item_inventario_ecuador i
      WHERE i.company_id = 1 AND i.pais_id = 1 AND i.codigo = c.codigo
  );
