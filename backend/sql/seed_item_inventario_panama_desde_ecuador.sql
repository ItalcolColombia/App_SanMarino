-- backend/sql/seed_item_inventario_panama_desde_ecuador.sql
-- Clona el catálogo de ítems de inventario de Ecuador (company 3 / país 2) a Panamá
-- (ItalcolPanama, company 5 / país 3) como BASE editable.
--
-- Contexto: Panamá es engorde y no tenía ítems (0). Este catálogo de origen es de postura/insumos
-- de Ecuador → queda como punto de partida a CURAR (renombrar/borrar los que no apliquen a engorde).
--
-- IDEMPOTENTE: no duplica gracias al guard NOT EXISTS sobre la unique (company_id, pais_id, codigo)
-- (uq_item_inv_ecuador_company_pais_codigo). Reejecutarlo inserta 0 filas.
-- id: se genera por secuencia (item_inventario_ecuador_id_seq) → NO se especifica.
--
-- Aplicar LOCAL: revisar el conteo abajo. PROD: solo con OK explícito (data en prod).

INSERT INTO item_inventario_ecuador
    (codigo, nombre, tipo_item, unidad, descripcion, activo,
     grupo, tipo_inventario_codigo, descripcion_tipo_inventario, referencia, descripcion_item, concepto,
     company_id, pais_id, created_at, updated_at)
SELECT
    src.codigo, src.nombre, src.tipo_item, src.unidad, src.descripcion, src.activo,
    src.grupo, src.tipo_inventario_codigo, src.descripcion_tipo_inventario, src.referencia, src.descripcion_item, src.concepto,
    5 AS company_id, 3 AS pais_id, now() AS created_at, now() AS updated_at
FROM item_inventario_ecuador AS src
WHERE src.company_id = 3
  AND src.pais_id = 2
  AND NOT EXISTS (
      SELECT 1
      FROM item_inventario_ecuador AS dst
      WHERE dst.company_id = 5
        AND dst.pais_id = 3
        AND dst.codigo = src.codigo
  );
