-- Menú: Seguimiento Reproductora Aves Engorde (pollo engorde; distinto del seguimiento reproductora levante)
-- Ruta frontend: /daily-log/seguimiento-diario-lote-reproductora_pollo_engorde
-- La ruta /daily-log/seguimiento-diario-lote-reproductora queda para el seguimiento normal (levante).
-- Debe existir el menú padre "Registros Diarios" (o el que tenga los hijos de daily-log).

-- Opción A: Si el padre es "Registros Diarios" por label
WITH parent_daily AS (
  SELECT id FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR label ILIKE '%Registro Diario%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  ORDER BY id LIMIT 1
),
next_ord AS (
  SELECT COALESCE(MAX(m."order"), 0) + 1 AS num
  FROM menus m
  WHERE m.parent_id = (SELECT id FROM parent_daily)
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Seguimiento Reproductora Aves Engorde',
  'clipboard-list',
  '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde',
  (SELECT id FROM parent_daily),
  (SELECT num FROM next_ord),
  true,
  NOW(),
  NOW()
WHERE EXISTS (SELECT 1 FROM parent_daily)
  AND NOT EXISTS (SELECT 1 FROM menus WHERE route = '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde');

-- Asignar a roles que tienen el padre (Registros Diarios)
INSERT INTO role_menus (role_id, menu_id)
SELECT r.id, m.id
FROM roles r
CROSS JOIN menus m
WHERE m.route = '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde'
  AND NOT EXISTS (SELECT 1 FROM role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = m.id)
  AND EXISTS (
    SELECT 1 FROM menus parent
    WHERE parent.id = m.parent_id
      AND EXISTS (SELECT 1 FROM role_menus rm2 WHERE rm2.role_id = r.id AND rm2.menu_id = parent.id)
  );

-- Si existía el ítem de pollo engorde con la ruta antigua, moverlo a _pollo_engorde (la ruta sin sufijo queda para levante)
UPDATE menus
SET route = '/daily-log/seguimiento-diario-lote-reproductora_pollo_engorde', label = 'Seguimiento Reproductora Aves Engorde', updated_at = NOW()
WHERE route = '/daily-log/seguimiento-diario-lote-reproductora'
  AND label ILIKE '%Reproductora Aves Engorde%';
