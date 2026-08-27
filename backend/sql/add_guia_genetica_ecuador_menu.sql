-- ============================================================================
-- ⛔ OBSOLETO — NO EJECUTAR. Se conserva solo como registro de como llego a
--    existir esta fila en produccion.
--
-- DOS motivos:
--
-- 1. NO FUNCIONA. `menus.key` es NOT NULL + UNIQUE (constraint `uq_menus_key`) y
--    este INSERT no la provee: hoy revienta. Medido el 26-ago-2026 contra la copia
--    de produccion.
--
-- 2. ESTA SUPERADO por la migracion
--    `20260826160000_SeedMenusGuiaGeneticaTresModulos`, que deja las TRES filas de
--    guia genetica con sus rotulos definitivos, provee `key`, y ademas resuelve
--    `company_menus` y `role_menus` — que este script declaraba explicitamente NO
--    hacer.
--
-- 🔴 Y sobre todo: correr un .sql a mano contra produccion es exactamente lo que
--    CLAUDE.md prohibe («el .sql es el ESPEJO; la migracion es el VEHICULO»). Estas
--    dos filas de `menus` entraron asi, y por eso durante meses el repo NO PUDO
--    PROBAR que existia realmente en produccion. Espejo nuevo:
--    backend/sql/add_guia_genetica_tres_modulos_menus.sql
-- ============================================================================

-- Ítem de menú: Guía genética Ecuador (Configuración)
-- Ruta frontend: /config/guia-genetica-ecuador
-- Asignar permisos/role_menus según política del proyecto.

WITH parent_config AS (
  SELECT id
  FROM menus
  WHERE (route = '/config' AND parent_id IS NULL)
     OR (label ILIKE '%config%' AND parent_id IS NULL)
  ORDER BY id
  LIMIT 1
)
INSERT INTO menus (label, icon, route, parent_id, "order", is_active, created_at, updated_at)
SELECT
  'Guía genética Ecuador',
  'dna',
  '/config/guia-genetica-ecuador',
  (SELECT id FROM parent_config),
  100,
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM menus WHERE route = '/config/guia-genetica-ecuador'
);

SELECT m.id, m.label, m.icon, m.route, m.parent_id, m."order", m.is_active
FROM menus m
WHERE m.route = '/config/guia-genetica-ecuador';
