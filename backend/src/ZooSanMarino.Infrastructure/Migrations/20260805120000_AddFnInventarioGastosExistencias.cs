using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Crea fn_inventario_gastos_existencias: hoja "Existencias" del reporte de Gastos de inventario.
    /// Devuelve el universo COMPLETO del catálogo no-alimento activo por granja (un ítem sin consumo
    /// aparece igual con su saldo) más el consumo del rango contando SOLO gastos Activos.
    /// Idempotente (CREATE OR REPLACE / DROP IF EXISTS). Migración hecha a mano (no altera el
    /// ModelSnapshot). Fuente/spec: backend/sql/fn_inventario_gastos_existencias.sql.
    /// </summary>
    public partial class AddFnInventarioGastosExistencias : Migration
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
        SELECT SUM(d.cantidad)      AS consumido,
               COUNT(DISTINCT g.id) AS gastos
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS fn_inventario_gastos_existencias(integer, integer, date, date, text);
");
        }
    }
}
