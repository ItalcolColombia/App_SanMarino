using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// <c>fn_inventario_gastos_search</c> devuelve además el LOTE PROGRAMADO del gasto
    /// (<c>lote_base_engorde_id</c> / <c>lote_base_nombre</c>) y acepta el filtro <c>p_lote_base_id</c>,
    /// para que un gasto cargado a una programación aún sin encasetar se vea en el listado.
    /// <para>
    /// Cambia <c>RETURNS TABLE</c> ⇒ <b>no basta CREATE OR REPLACE</b>: el cuerpo incluye el
    /// <c>DROP FUNCTION IF EXISTS</c> de la firma vieja y de la nueva (idempotente en re-deploy).
    /// </para>
    /// <para>
    /// El cuerpo es una copia literal de <c>backend/sql/fn_inventario_gastos_search.sql</c> (generado
    /// desde ese archivo) para que el espejo y la migración no puedan divergir.
    /// </para>
    /// </summary>
    public partial class FnInventarioGastosSearchConLoteProgramado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"-- ============================================================================
-- fn_inventario_gastos_search(...) — lista de gastos de inventario (Ecuador).
-- Reemplaza el armado en memoria de InventarioGastoService.SearchAsync:
--   * joins de granja/núcleo/galpón/lote (antes: diccionarios en C# con N round-trips)
--   * agregación de líneas + total + unidad (antes: subconsultas por fila en C#)
--   * filtro por concepto (antes: query aparte + HashSet en C#)
-- Replica EXACTO el comportamiento del C# (fuente de verdad: InventarioGastoService.cs):
--   * company_id obligatorio; resto de filtros opcionales (NULL = sin filtro).
--   * núcleo/galpón se resuelven por (id, granja_id = farm_id) igual que los diccionarios.
--   * unidad = primera línea (FirstOrDefault sin orden) -> subquery LIMIT 1.
--   * total_cantidad = SUM(cantidad) o 0 ; lineas = COUNT.
--   * búsqueda = observaciones lower CONTAINS search lower -> lower LIKE %..%.
--   * orden: fecha DESC, id DESC.
-- Columnas en PascalCase citado para mapear directo a InventarioGastoListRow (SqlQueryRaw).
-- Lote PROGRAMADO (jul-2026): un gasto puede colgar de lote_base_engorde cuando el lote real todavía
-- no existe (desinsectación previa al encaset). Se devuelven lote_base_engorde_id/lote_base_nombre y
-- se filtra por p_lote_base_id. Cambia RETURNS TABLE ⇒ DROP + CREATE (no basta CREATE OR REPLACE).
-- ============================================================================
DROP FUNCTION IF EXISTS fn_inventario_gastos_search(integer, integer, text, text, integer, date, date, text, text, text);
DROP FUNCTION IF EXISTS fn_inventario_gastos_search(integer, integer, text, text, integer, date, date, text, text, text, integer);

CREATE FUNCTION fn_inventario_gastos_search(
    p_company_id  integer,
    p_farm_id     integer DEFAULT NULL,
    p_nucleo_id   text    DEFAULT NULL,
    p_galpon_id   text    DEFAULT NULL,
    p_lote_id     integer DEFAULT NULL,
    p_fecha_desde date    DEFAULT NULL,
    p_fecha_hasta date    DEFAULT NULL,
    p_concepto    text    DEFAULT NULL,
    p_search      text    DEFAULT NULL,
    p_estado      text    DEFAULT NULL,
    p_lote_base_id integer DEFAULT NULL
)
-- Columnas snake_case: EF (EFCore.NamingConventions) mapea las props PascalCase de
-- InventarioGastoListRow a snake_case también en SqlQueryRaw (p.ej. CreatedAt -> created_at).
RETURNS TABLE(
    id                  integer,
    fecha               date,
    farm_id             integer,
    granja_nombre       text,
    nucleo_id           text,
    nucleo_nombre       text,
    galpon_id           text,
    galpon_nombre       text,
    lote_ave_engorde_id integer,
    lote_nombre         text,
    lote_base_engorde_id integer,
    lote_base_nombre    text,
    observaciones       text,
    estado              text,
    lineas              integer,
    total_cantidad      numeric,
    unidad              text,
    created_at          timestamptz,
    created_by_user_id  text,
    items               text
)
LANGUAGE sql STABLE AS $fn$
    SELECT
        g.id,
        g.fecha,
        g.farm_id,
        f.name,
        g.nucleo_id,
        n.nucleo_nombre,
        g.galpon_id,
        gp.galpon_nombre,
        g.lote_ave_engorde_id,
        l.lote_nombre,
        g.lote_base_engorde_id,
        lb.nombre,
        g.observaciones,
        g.estado,
        COALESCE(d.lineas, 0)::int,
        COALESCE(d.total_cantidad, 0)::numeric,
        d.unidad,
        g.created_at,
        g.created_by_user_id,
        d.items::text
    FROM inventario_gasto g
    LEFT JOIN farms f            ON f.id = g.farm_id
    LEFT JOIN nucleos n          ON n.nucleo_id = g.nucleo_id AND n.granja_id = g.farm_id
    LEFT JOIN galpones gp        ON gp.galpon_id = g.galpon_id AND gp.granja_id = g.farm_id
    LEFT JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = g.lote_ave_engorde_id
    LEFT JOIN lote_base_engorde lb ON lb.id = g.lote_base_engorde_id
    LEFT JOIN LATERAL (
        SELECT
            COUNT(*)          AS lineas,
            SUM(det.cantidad) AS total_cantidad,
            (SELECT det2.unidad
               FROM inventario_gasto_detalle det2
              WHERE det2.inventario_gasto_id = g.id
              LIMIT 1)        AS unidad,
            -- Ítems consumidos (para mostrarlos inline en la tabla, sin abrir el detalle).
            -- Ordenados por nombre igual que el detalle. codigo/nombre desde el catálogo de ítems.
            COALESCE(
                json_agg(
                    json_build_object(
                        'codigo',   it.codigo,
                        'nombre',   it.nombre,
                        'cantidad', det.cantidad,
                        'unidad',   det.unidad
                    ) ORDER BY it.nombre
                ),
                '[]'::json
            )                 AS items
        FROM inventario_gasto_detalle det
        LEFT JOIN item_inventario_ecuador it ON it.id = det.item_inventario_ecuador_id
        WHERE det.inventario_gasto_id = g.id
    ) d ON TRUE
    WHERE g.company_id = p_company_id
      AND (p_farm_id     IS NULL OR g.farm_id = p_farm_id)
      AND (p_nucleo_id   IS NULL OR g.nucleo_id = p_nucleo_id)
      AND (p_galpon_id   IS NULL OR g.galpon_id = p_galpon_id)
      AND (p_lote_id     IS NULL OR g.lote_ave_engorde_id = p_lote_id)
      AND (p_lote_base_id IS NULL OR g.lote_base_engorde_id = p_lote_base_id)
      AND (p_fecha_desde IS NULL OR g.fecha >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR g.fecha <= p_fecha_hasta)
      AND (p_estado      IS NULL OR g.estado = p_estado)
      AND (p_search      IS NULL OR lower(COALESCE(g.observaciones, '')) LIKE '%' || lower(p_search) || '%')
      AND (p_concepto    IS NULL OR EXISTS (
            SELECT 1 FROM inventario_gasto_detalle dc
             WHERE dc.inventario_gasto_id = g.id AND dc.concepto = p_concepto
      ))
    ORDER BY g.fecha DESC, g.id DESC;
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La firma anterior (sin p_lote_base_id) vive en el historial de migraciones; revertir
            // solo borra la función nueva para no dejar dos firmas conviviendo.
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS fn_inventario_gastos_search(integer, integer, text, text, integer, date, date, text, text, text, integer);");
        }
    }
}
