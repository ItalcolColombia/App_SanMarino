-- Script para agregar el módulo "Guía Genética" al menú dentro de Configuración
-- Route frontend: /config/guia-genetica
-- Nota: el menú se filtra por rol (role_menus). Este script SOLO crea el item en menus.

-- 1) Detectar menú padre de Configuración (por route o label)
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
  'Guía Genética',
  'clipboard-list',
  '/config/guia-genetica',
  (SELECT id FROM parent_config),
  99,
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM menus WHERE route = '/config/guia-genetica'
);

-- Verificar
SELECT m.id, m.label, m.icon, m.route, m.parent_id, m."order", m.is_active
FROM menus m
WHERE m.route = '/config/guia-genetica';

