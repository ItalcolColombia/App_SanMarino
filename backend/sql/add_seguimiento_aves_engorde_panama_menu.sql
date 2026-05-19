-- ============================================================
-- Menú: Seguimiento Pollo Engorde — Panamá
-- Ruta frontend : /daily-log/aves-engorde-panama
-- Padre         : "Registros Diarios" (route = '/daily-log')
-- ============================================================

-- ── 1. INSERTAR ÍTEM DE MENÚ ─────────────────────────────────────────────────
-- Se coloca inmediatamente después del ítem de Ecuador (/daily-log/aves-engorde).
-- Si el padre no existe, el INSERT no ejecuta (EXISTS guard en el subquery).

WITH parent_daily AS (
  SELECT id
  FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  ORDER BY id
  LIMIT 1
),
ecuador_order AS (
  -- Tomamos el order del ítem de Ecuador como referencia
  SELECT COALESCE(m."order", 4) AS num
  FROM menus m
  WHERE m.route = '/daily-log/aves-engorde'
  LIMIT 1
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Seg. Pollo Engorde Panamá',
  'clipboard-list',
  '/daily-log/aves-engorde-panama',
  (SELECT id FROM parent_daily),
  -- Orden = Ecuador + 1 para quedar justo después
  (SELECT num FROM ecuador_order) + 1,
  true,
  NOW(),
  NOW()
WHERE EXISTS (SELECT 1 FROM parent_daily)
  AND NOT EXISTS (
    SELECT 1 FROM menus WHERE route = '/daily-log/aves-engorde-panama'
  );


-- ── 2. CORREGIR ORDEN DEL ÍTEM QUE ESTABA EN ESA POSICIÓN ───────────────────
-- Si algún ítem ya tenía order = Ecuador_order + 1, incrementamos los que venían después
-- para evitar colisión de orden (opcional si el frontend solo ordena, no exige unicidad).

WITH parent_daily AS (
  SELECT id
  FROM menus
  WHERE (label ILIKE '%Registros Diarios%' OR route = '/daily-log')
    AND (parent_id IS NULL OR parent_id = 0)
  ORDER BY id LIMIT 1
),
ecuador_order AS (
  SELECT COALESCE(m."order", 4) AS num
  FROM menus m
  WHERE m.route = '/daily-log/aves-engorde'
  LIMIT 1
)
UPDATE menus
SET "order" = "order" + 1
WHERE parent_id = (SELECT id FROM parent_daily)
  AND route <> '/daily-log/aves-engorde-panama'
  AND "order" > (SELECT num FROM ecuador_order);


-- ── 3. ASIGNAR A ROLES ───────────────────────────────────────────────────────
-- Heredamos los mismos roles que tienen acceso al módulo Ecuador de pollo engorde.
-- Si un rol tiene /daily-log/aves-engorde, recibe también /daily-log/aves-engorde-panama.

INSERT INTO role_menus (role_id, menu_id)
SELECT DISTINCT r.id, nuevo.id
FROM roles r
CROSS JOIN menus nuevo
-- Subquery: roles que ya tienen acceso a aves-engorde Ecuador
JOIN role_menus rm_ref ON rm_ref.role_id = r.id
JOIN menus ecuador     ON ecuador.id = rm_ref.menu_id
                      AND ecuador.route = '/daily-log/aves-engorde'
WHERE nuevo.route = '/daily-log/aves-engorde-panama'
  AND NOT EXISTS (
    SELECT 1 FROM role_menus rm_exist
    WHERE rm_exist.role_id = r.id
      AND rm_exist.menu_id = nuevo.id
  );


-- ── 4. ASIGNAR A EMPRESAS (company_menus) ───────────────────────────────────
-- Solo para empresas que ya tienen habilitado el módulo Ecuador de pollo engorde.
-- sort_order = sort_order Ecuador + 1; parent_menu_id hereda el mismo padre.

INSERT INTO company_menus (company_id, menu_id, is_enabled, sort_order, parent_menu_id)
SELECT
  cm_ec.company_id,
  nuevo.id,
  true,
  cm_ec.sort_order + 1,
  cm_ec.parent_menu_id
FROM company_menus cm_ec
JOIN menus ecuador ON ecuador.id = cm_ec.menu_id
                   AND ecuador.route = '/daily-log/aves-engorde'
JOIN menus nuevo   ON nuevo.route = '/daily-log/aves-engorde-panama'
WHERE NOT EXISTS (
  SELECT 1 FROM company_menus cm_exist
  WHERE cm_exist.company_id = cm_ec.company_id
    AND cm_exist.menu_id = nuevo.id
);


-- ── 5. VERIFICACIÓN FINAL ────────────────────────────────────────────────────

SELECT
  m.id,
  m.label,
  m.icon,
  m.route,
  m.parent_id,
  m."order",
  m.is_active,
  p.label AS parent_label
FROM menus m
LEFT JOIN menus p ON p.id = m.parent_id
WHERE m.route IN ('/daily-log/aves-engorde', '/daily-log/aves-engorde-panama')
ORDER BY m."order";
