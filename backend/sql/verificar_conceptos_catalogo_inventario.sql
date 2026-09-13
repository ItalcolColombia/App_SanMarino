-- =====================================================================================
-- Control del `concepto` del catálogo de inventario: detecta lo que hace que el desplegable
-- «Concepto» de Gestión de Inventario liste la misma opción dos veces.
--
-- Se corre SOLO LECTURA, antes y después de tocar el catálogo (o de una carga masiva de
-- ítems por Excel, que es por donde vuelve a entrar el problema).
--
--   psql ... -f backend/sql/verificar_conceptos_catalogo_inventario.sql
--
-- Las consultas 1 a 3 son el invariante: las tres tienen que dar CERO filas. La 4 y la 5 son
-- informativas (deuda conocida y foto del catálogo).
-- Corrección: backend/sql/normalizar_concepto_catalogo_inventario.sql
-- =====================================================================================

\echo '=== 1) Conceptos duplicados por capitalizacion DENTRO de una empresa (esperado: 0 filas) ==='
-- Este es el defecto que ve el usuario: dos opciones que devuelven las mismas filas.
SELECT company_id,
       lower(btrim(concepto))                 AS concepto_normalizado,
       string_agg(DISTINCT btrim(concepto), ' | ' ORDER BY btrim(concepto)) AS variantes,
       count(*)                               AS items
FROM item_inventario
WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
GROUP BY 1, 2
HAVING count(DISTINCT btrim(concepto)) > 1
ORDER BY 1, 2;

\echo '=== 2) Mismo codigo con conceptos divergentes entre empresas (esperado: 0 filas) ==='
-- Los catálogos de las empresas clonadas deben coincidir; una divergencia es carga manual.
SELECT codigo,
       string_agg(DISTINCT btrim(concepto), ' | ' ORDER BY btrim(concepto)) AS conceptos,
       count(*) AS items
FROM item_inventario
WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
GROUP BY 1
HAVING count(DISTINCT lower(btrim(concepto))) > 1
ORDER BY 1;

\echo '=== 3) Snapshot de gastos desincronizado por capitalizacion (esperado: 0 filas) ==='
-- `inventario_gasto_detalle.concepto` se filtra con igualdad EXACTA mientras el desplegable
-- sale del catálogo: si difieren solo en mayúsculas, esas líneas quedan infiltrables.
SELECT d.id, d.inventario_gasto_id, d.concepto AS concepto_detalle, i.concepto AS concepto_item
FROM inventario_gasto_detalle d
JOIN item_inventario i ON i.id = d.item_inventario_id
WHERE d.concepto IS NOT NULL AND i.concepto IS NOT NULL
  AND lower(btrim(d.concepto)) = lower(btrim(i.concepto))
  AND d.concepto IS DISTINCT FROM i.concepto
ORDER BY d.id;

\echo '=== 4) INFORMATIVO: snapshot con un concepto REALMENTE distinto al del item ==='
-- No lo corrige la migración: cambiar el valor sería reescribir una categorización histórica.
-- Deuda conocida al 05-ago-2026: 10 líneas con 'insumo' sobre un ítem que es 'Otros insumos'.
SELECT d.concepto AS concepto_detalle, i.concepto AS concepto_item, count(*) AS lineas
FROM inventario_gasto_detalle d
JOIN item_inventario i ON i.id = d.item_inventario_id
WHERE lower(btrim(coalesce(d.concepto, ''))) <> lower(btrim(coalesce(i.concepto, '')))
GROUP BY 1, 2
ORDER BY 3 DESC;

\echo '=== 5) INFORMATIVO: foto del catalogo (conceptos por empresa) ==='
-- Los ítems con concepto NULL caen a `tipo_item` por el `Concepto ?? TipoItem` del backend:
-- aparecen acá como '(sin concepto → tipo_item)' porque así los ve el desplegable.
SELECT company_id,
       CASE WHEN concepto IS NULL OR btrim(concepto) = ''
            THEN '(sin concepto -> ' || btrim(tipo_item) || ')'
            ELSE btrim(concepto) END AS opcion_del_desplegable,
       count(*) AS items
FROM item_inventario
GROUP BY 1, 2
ORDER BY 1, 2;
