using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase D del plan <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>:
    /// <c>fn_inventario_gastos_existencias</c> agrega el saldo con <c>SUM</c> + <c>GROUP BY</c>.
    ///
    /// <para>
    /// Desde la Fase B, una empresa con <c>maneja_inventario_por_silo</c> guarda una fila de
    /// <c>inventario_gestion_stock</c> <b>por silo/bodega</b>, todas con <c>nucleo_id</c> y
    /// <c>galpon_id</c> en NULL. El <c>LEFT JOIN</c> directo de la versión anterior asumía UNA fila
    /// por (granja, ítem): con N silos habría devuelto N filas del mismo ítem, cada una con un saldo
    /// PARCIAL, y la hoja "Existencias" del reporte de Gastos habría quedado ilegible y mal sumada.
    /// </para>
    ///
    /// <para>
    /// Con el flag apagado el índice único garantiza exactamente una fila (<c>silo_id</c> NULL), así
    /// que el <c>SUM</c> devuelve el mismo número que antes: para Ecuador, Panamá y Colombia el
    /// reporte no cambia ni una celda. Es un arreglo <b>preventivo</b> — Gastos de inventario todavía
    /// no está en los <c>company_menus</c> de Santa Reyes y esta fn es requisito para habilitarlo.
    /// </para>
    ///
    /// Idempotente (CREATE OR REPLACE). Migración hecha a mano (no altera el ModelSnapshot).
    /// Fuente/spec: <c>backend/sql/fn_inventario_gastos_existencias.sql</c>.
    /// </summary>
    public partial class FnGastosExistenciasSaldoPorSilo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
    ),
    saldos AS (
        -- Saldo a nivel granja: la SUMA de lo que hay en cada silo/bodega. Sin este GROUP BY una
        -- empresa por silo multiplicaría las filas del reporte (una por silo, con saldo parcial).
        -- Se acota a las granjas del universo para no escanear el stock de toda la BD.
        SELECT s.farm_id, s.item_inventario_ecuador_id, SUM(s.quantity) AS quantity
        FROM inventario_gestion_stock s
        WHERE s.farm_id IN (SELECT id FROM granjas)
          AND s.nucleo_id IS NULL
          AND s.galpon_id IS NULL
        GROUP BY s.farm_id, s.item_inventario_ecuador_id
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
    LEFT JOIN saldos st
           ON st.farm_id = gr.id
          AND st.item_inventario_ecuador_id = it.id
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
    -- El desempate por codigo+id NO es cosmético: hay ítems distintos con el MISMO nombre (p. ej.
    -- AV0342 y SM0272, ambos 'AV. HEPA INMUNO BROILER NB 2500DS' en Ecuador). Sin él, Postgres los
    -- devuelve en el orden que le convenga al plan y dos corridas seguidas del reporte salen con las
    -- filas intercambiadas — ruido puro al comparar exportaciones.
    ORDER BY gr.name,
             CASE WHEN COALESCE(btrim(it.concepto), '') = '' THEN '~' ELSE lower(btrim(it.concepto)) END,
             it.nombre,
             it.codigo,
             it.id;
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a la versión de AddFnInventarioGastosExistencias (LEFT JOIN directo al stock).
            // NO se hace DROP: la fn la creó la migración anterior y el reporte dejaría de responder.
            migrationBuilder.Sql(@"
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
    ORDER BY gr.name,
             CASE WHEN COALESCE(btrim(it.concepto), '') = '' THEN '~' ELSE lower(btrim(it.concepto)) END,
             it.nombre;
$fn$;
");
        }
    }
}
