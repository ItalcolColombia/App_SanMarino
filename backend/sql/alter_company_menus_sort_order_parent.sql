-- Añadir orden y padre por empresa para poder reordenar y reparentar menús por compañía.
-- Ejecutar sobre la tabla company_menus ya creada.

ALTER TABLE company_menus
  ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS parent_menu_id integer NULL;

COMMENT ON COLUMN company_menus.sort_order IS 'Orden de visualización del ítem en el menú de esta empresa.';
COMMENT ON COLUMN company_menus.parent_menu_id IS 'ID del menú padre en esta empresa; NULL = usar padre global del ítem.';

-- Opcional: FK para parent_menu_id (referencia a menus)
-- ALTER TABLE company_menus
--   ADD CONSTRAINT fk_company_menus_parent
--   FOREIGN KEY (parent_menu_id) REFERENCES menus (id) ON DELETE SET NULL;
