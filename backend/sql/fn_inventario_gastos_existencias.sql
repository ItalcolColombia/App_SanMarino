-- backend/sql/fn_inventario_gastos_existencias.sql
--
-- Hoja "Existencias" del reporte de Gastos de inventario.
--
-- El reporte viejo partía de inventario_gasto_detalle: un ítem sin consumo NO existía en el archivo,
-- así que no servía para controlar el inventario. Esta función devuelve el universo COMPLETO del
-- catálogo no-alimento activo de la empresa (desinfectantes, empaques, gas, insumos, medicamento,
-- otros insumos, pollinaza, vacunas, ...) por granja, con:
--   * saldo_actual    -> stock vivo a nivel granja (0 si el ítem nunca entró a esa granja)
--   * consumido_rango -> consumo del módulo en el rango, contando SOLO gastos Activos
--                        (los eliminados ya devolvieron su stock: contarlos duplicaría el gasto)
--   * gastos_rango    -> cuántas cabeceras de gasto activas lo consumieron en el rango
--
-- Universo de granjas: la del filtro; sin filtro, las granjas de la empresa que manejan inventario
-- a nivel granja o registraron gastos (evita el producto cartesiano con granjas sin inventario).
--
-- ⚠️ Columnas snake_case en RETURNS TABLE: EF aplica la naming convention a las props del row-class
-- también en SqlQueryRaw<T> (mismo gotcha que fn_inventario_gastos_search / fn_kardex_farm_inventory).
--
-- Idempotente (CREATE OR REPLACE). Fuente/spec de la migración AddFnInventarioGastosExistencias.

CREATE OR REPLACE FUNCTION fn_inventario_gastos_existencias(
    p_company_id  integer,
    p_farm_id     integer DEFAULT NULL,
    p_fecha_desde date    DEFAULT NULL,
    p_fecha_hasta date    DEFAULT NULL,
    p_concepto    text    DEFAULT NULL
)
RETURNS TABLE(
    farm_id                    integer,
    granja_nombre              text,
    item_inventario_ecuador_id integer,
    codigo                     text,
    nombre                     text,
    tipo_item                  text,
    unidad                     text,
    concepto                   text,
    saldo_actual               numeric,
    consumido_rango            numeric,
    gastos_rango               integer
)
LANGUAGE sql STABLE AS $fn$
    WITH granjas AS (
        -- Granja del filtro; sin filtro, las de la empresa con inventario a nivel granja o con gastos.
        SELECT f.id, f.name
        FROM farms f
        WHERE f.company_id = p_company_id
          AND (p_farm_id IS NOT NULL AND f.id = p_farm_id
               OR p_farm_id IS NULL AND (
                    EXISTS (SELECT 1 FROM inventario_gestion_stock s
                             WHERE s.farm_id = f.id AND s.nucleo_id IS NULL AND s.galpon_id IS NULL)
                 OR EXISTS (SELECT 1 FROM inventario_gasto g WHERE g.farm_id = f.id)
               ))
    ),
    items AS (
        -- Catálogo NO-alimento activo de la empresa (el módulo no consume alimento).
        SELECT i.id, i.codigo, i.nombre, i.tipo_item, i.unidad, i.concepto
        FROM item_inventario_ecuador i
        WHERE i.company_id = p_company_id
          AND i.activo
          AND lower(i.tipo_item) <> 'alimento'
          AND (p_concepto IS NULL OR btrim(i.concepto) = btrim(p_concepto))
    )
    SELECT
        gr.id,
        gr.name,
        it.id,
        it.codigo,
        it.nombre,
        it.tipo_item,
        it.unidad,
        it.concepto,
        COALESCE(st.quantity, 0)::numeric   AS saldo_actual,
        COALESCE(cs.consumido, 0)::numeric  AS consumido_rango,
        COALESCE(cs.gastos, 0)::int         AS gastos_rango
    FROM granjas gr
    CROSS JOIN items it
    LEFT JOIN inventario_gestion_stock st
           ON st.farm_id = gr.id
          AND st.item_inventario_ecuador_id = it.id
          AND st.nucleo_id IS NULL
          AND st.galpon_id IS NULL
    LEFT JOIN LATERAL (
        SELECT SUM(d.cantidad)              AS consumido,
               COUNT(DISTINCT g.id)         AS gastos
        FROM inventario_gasto g
        JOIN inventario_gasto_detalle d ON d.inventario_gasto_id = g.id
        WHERE g.company_id = p_company_id
          AND g.farm_id    = gr.id
          AND d.item_inventario_ecuador_id = it.id
          AND g.estado <> 'Eliminado'
          AND (p_fecha_desde IS NULL OR g.fecha >= p_fecha_desde)
          AND (p_fecha_hasta IS NULL OR g.fecha <= p_fecha_hasta)
    ) cs ON TRUE
    -- Orden por concepto NORMALIZADO (agrupa 'Otros insumos' / 'Otros Insumos', que conviven en el
    -- catálogo); los sin concepto van al final. El concepto se DEVUELVE tal cual está en el catálogo.
    ORDER BY gr.name,
             CASE WHEN COALESCE(btrim(it.concepto), '') = '' THEN '~' ELSE lower(btrim(it.concepto)) END,
             it.nombre;
$fn$;
