-- =====================================================================================
-- Normalización del `concepto` del catálogo de inventario (copia trazable de la migración
-- 20260805180000_NormalizarConceptoCatalogoInventario).
--
-- PROBLEMA: el mismo concepto convive escrito con distinta capitalización, así que el
-- desplegable «Concepto» de Gestión de Inventario lo lista DOS veces con opciones que
-- devuelven exactamente las mismas filas. Todos los filtros del módulo comparan normalizado
-- (`Trim().ToLower()`); solo las LISTAS de opciones usaban igualdad exacta.
--
-- Estado en el dump del 05-ago-2026 (empresas 3 y 5, catálogos clonados):
--     empresa 3: 'Otros insumos' 36 ítems  ·  'Otros Insumos'  6 ítems
--     empresa 5: 'Otros insumos' 34 ítems  ·  'Otros Insumos'  6 ítems
--     empresa 5: 'insumo' 1 ítem (AV0351, que en la empresa 3 es 'Otros insumos')
--
-- REGLA 1 — capitalización, POR EMPRESA. Gana la variante más usada. Es por empresa a
-- propósito: el catálogo se sirve acotado a la empresa activa, así que un duplicado solo
-- existe dentro de una misma empresa. Hacerlo global CREARÍA uno nuevo — el único ítem con
-- concepto = 'alimento' vive en una empresa donde los otros 61 no tienen concepto y caen a
-- tipo_item = 'alimento' por el `Concepto ?? TipoItem`; capitalizarlo pondría las dos
-- opciones en ese desplegable. Por empresa, ese ítem queda intacto.
--
-- REGLA 2 — el snapshot de Gastos de inventario. `inventario_gasto_detalle.concepto` se
-- filtra con igualdad EXACTA (fn_inventario_gastos_search y InventarioGastoService) mientras
-- su desplegable se arma desde el catálogo: sin esta regla, las líneas que quedaron con la
-- capitalización vieja dejarían de ser filtrables. Solo alinea las que coinciden en
-- minúsculas con el concepto de SU ítem ⇒ nunca recategoriza un consumo histórico.
--
-- REGLA 3 — el concepto suelto. Mismo `codigo` con conceptos divergentes entre empresas
-- (catálogos clonados que deberían coincidir) adopta el más usado del catálogo, con guardas:
-- el perdedor tiene que ser marginal (<= 1 ítem) y el ganador estar usado por varios.
--
-- FUERA DE ALCANCE a propósito: (a) los 167 ítems con concepto NULL y tipo_item 'alimento'
-- (el fallback los resuelve y en Colombia el concepto no aplica por diseño); (b) 10 líneas de
-- gasto con concepto = 'insumo' que apuntan a un ítem cuyo concepto siempre fue
-- 'Otros insumos' (corregirlas sería reescribir una categorización histórica sobre una
-- hipótesis).
--
-- Ninguna regla nombra ids ni etiquetas de negocio. Idempotente (`IS DISTINCT FROM`).
-- Simular siempre con BEGIN ... ROLLBACK antes de aplicar. Efecto medido: 12 / 0 / 1 filas.
-- Control posterior: backend/sql/verificar_conceptos_catalogo_inventario.sql
-- =====================================================================================

-- ── Regla 1: por empresa, gana la variante más usada de cada concepto ────────────────────
WITH variantes AS (
    SELECT company_id,
           lower(btrim(concepto)) AS norm,
           btrim(concepto)        AS etiqueta,
           count(*)               AS usos
    FROM item_inventario
    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
    GROUP BY 1, 2, 3
), canonico AS (
    SELECT DISTINCT ON (company_id, norm) company_id, norm, etiqueta
    FROM variantes
    ORDER BY company_id, norm, usos DESC, etiqueta
)
UPDATE item_inventario i
SET concepto = c.etiqueta, updated_at = now()
FROM canonico c
WHERE c.company_id = i.company_id
  AND c.norm = lower(btrim(i.concepto))
  AND i.concepto IS DISTINCT FROM c.etiqueta;

-- ── Regla 2: el snapshot de gastos sigue al catálogo, SOLO en la capitalización ──────────
UPDATE inventario_gasto_detalle d
SET concepto = i.concepto
FROM item_inventario i
WHERE i.id = d.item_inventario_id
  AND d.concepto IS NOT NULL AND btrim(d.concepto) <> ''
  AND i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
  AND lower(btrim(d.concepto)) = lower(btrim(i.concepto))
  AND d.concepto IS DISTINCT FROM i.concepto;

-- ── Regla 3: mismo código con conceptos divergentes ⇒ gana el más usado del catálogo ─────
WITH usos AS (
    SELECT lower(btrim(concepto)) AS norm, count(*) AS n
    FROM item_inventario
    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
    GROUP BY 1
), divergentes AS (
    SELECT codigo
    FROM item_inventario
    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
    GROUP BY codigo
    HAVING count(DISTINCT lower(btrim(concepto))) > 1
), ganador AS (
    SELECT DISTINCT ON (i.codigo) i.codigo, btrim(i.concepto) AS etiqueta, u.n
    FROM item_inventario i
    JOIN divergentes d ON d.codigo = i.codigo
    JOIN usos u        ON u.norm   = lower(btrim(i.concepto))
    WHERE i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
    ORDER BY i.codigo, u.n DESC, btrim(i.concepto)
)
UPDATE item_inventario i
SET concepto = g.etiqueta, updated_at = now()
FROM ganador g
WHERE g.codigo = i.codigo
  AND i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
  AND lower(btrim(i.concepto)) <> lower(btrim(g.etiqueta))
  AND g.n > 1
  AND (SELECT u2.n FROM usos u2 WHERE u2.norm = lower(btrim(i.concepto))) <= 1
  AND i.concepto IS DISTINCT FROM g.etiqueta;
