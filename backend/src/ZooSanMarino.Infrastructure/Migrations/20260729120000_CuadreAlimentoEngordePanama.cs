using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuadre del alimento de engorde en Panama: el seguimiento diario y Gestion de inventario deben
    /// mostrar el MISMO saldo, y ese saldo es el logico: <c>ingresos − consumo real</c>.
    ///
    /// <b>Causa raiz.</b> El inventario nunca descontó el consumo de los 7 dias del CRUCE de
    /// reproductora: esos dias los escribe <c>fn_cruce_reproductora_a_engorde</c> por SQL directo, sin
    /// pasar por el service, igual que pasaba con las aves. Verificado al decimal en 19 de 25 galpones:
    /// <c>consumo_seguimiento − consumo_inventario = consumo de los dias de cruce</c>. Todo ese consumo
    /// es AV. POLLITO PREINICIADOR (item 223), que es lo que comen los pollitos la primera semana.
    ///
    /// Consecuencia: el STOCK quedo inflado, no el seguimiento. La operacion venia compensandolo a mano
    /// —y en varios galpones dio exacto (G0490: llevo el item 223 de 8.935,862 a 0, y el consumo del
    /// cruce es 8.935,9)— pero en otros el numero no cerro, y en DAYLAND no se ajusto nada.
    ///
    /// <b>Que hace esta migracion.</b>
    /// 1. <b>G0486 (MENDOZA)</b>: anula las 18 filas que la 2a corrida de la carga masiva del 28/07 le
    ///    metio encima — son los ingresos de G0485 (identicas en filas y kilos a su total). Guarda
    ///    fail-safe: solo anula si el conjunto es EXACTAMENTE 18 filas por 128.302,2 kg.
    /// 2. <b>G0461 (DAYLAND)</b>: consumio 6.622,5 kg sin ningun ingreso registrado. Se registra el
    ///    ingreso faltante (si se consumio, es que entro), fechado al primer dia de seguimiento.
    /// 3. <b>Alineacion del stock</b>: por cada galpon lleva el stock del inventario al saldo logico,
    ///    con un <c>AjusteStock</c> normal —el mismo movimiento que genera la pantalla— para que quede
    ///    auditado y visible. Para BAJAR descuenta en cascada 223 → 214 → 213 (el orden del ciclo de
    ///    alimento, empezando por el preiniciador que es el que falto descontar); para SUBIR devuelve al
    ///    item con mas stock del galpon.
    ///
    /// El delta se calcula CONTRA LOS DATOS DEL MOMENTO, nunca contra constantes.
    /// Idempotente: al terminar no queda diferencia, asi que una segunda corrida no encuentra nada que
    /// ajustar. Con respaldo para que el Down revierta exactamente lo suyo.
    /// Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md
    /// </summary>
    public partial class CuadreAlimentoEngordePanama : Migration
    {
        private const string OrigenIngresoG0461 = "ingreso_faltante_cuadre_20260729";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS _backup_cuadre_alimento_20260729 (
    id           bigserial PRIMARY KEY,
    accion       text   NOT NULL,
    historico_id bigint,
    stock_id     integer,
    movimiento_id integer,
    valor_anterior numeric(18,3),
    creado_at    timestamptz NOT NULL DEFAULT now()
);");

            // ── 1) G0486: anular la corrida espuria (los ingresos de G0485) ──────────
            migrationBuilder.Sql(@"
WITH candidatas AS (
    SELECT h.id, h.cantidad_kg
      FROM lote_registro_historico_unificado h
     WHERE h.company_id = 5
       AND h.farm_id    = 108
       AND COALESCE(TRIM(h.galpon_id), '') = 'G0486'
       AND h.tipo_evento = 'INV_INGRESO'
       AND NOT h.anulado
       AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
       AND h.created_at >= TIMESTAMPTZ '2026-07-28 20:55:00-05'
),
control AS (SELECT COUNT(*) AS n, COALESCE(SUM(cantidad_kg), 0) AS kg FROM candidatas),
elegidas AS (
    SELECT c.id FROM candidatas c, control ct
     WHERE ct.n = 18 AND ct.kg BETWEEN 128302.0 AND 128302.4
),
bkp AS (
    INSERT INTO _backup_cuadre_alimento_20260729 (accion, historico_id)
    SELECT 'anulado_g0486', e.id FROM elegidas e
    RETURNING 1
)
UPDATE lote_registro_historico_unificado t
   SET anulado = true
  FROM elegidas e
 WHERE t.id = e.id;");

            // ── 2) G0461: registrar el ingreso de alimento que nunca se cargo ────────
            migrationBuilder.Sql($@"
WITH objetivo AS (
    SELECT l.company_id, l.granja_id, l.nucleo_id, l.galpon_id,
           MIN(l.lote_ave_engorde_id)                                   AS lote_id,
           MIN(DATE(s.fecha))                                           AS fecha_ini,
           SUM(COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0)) AS consumo
      FROM lote_ave_engorde l
      JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id = l.lote_ave_engorde_id
     WHERE l.company_id = 5 AND l.granja_id = 107
       AND COALESCE(TRIM(l.galpon_id), '') = 'G0461'
       AND l.deleted_at IS NULL
     GROUP BY 1,2,3,4
    HAVING SUM(COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0)) > 0
       -- solo si el galpon NO tiene ningun ingreso propio registrado
       AND NOT EXISTS (
             SELECT 1 FROM lote_registro_historico_unificado h
              WHERE h.farm_id = l.granja_id
                AND COALESCE(TRIM(h.galpon_id), '') = COALESCE(TRIM(l.galpon_id), '')
                AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA')
                AND NOT h.anulado
                AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
                AND NOT (h.referencia IS NOT NULL AND (
                         h.referencia LIKE '%devolución por eliminación%'
                      OR h.referencia LIKE '%devolucion por eliminacion%')))
)
INSERT INTO lote_registro_historico_unificado (
    company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id, fecha_operacion,
    tipo_evento, origen_tabla, origen_id, item_inventario_ecuador_id,
    cantidad_kg, unidad, referencia, anulado)
SELECT o.company_id, o.lote_id, o.granja_id, o.nucleo_id, o.galpon_id, o.fecha_ini,
       'INV_INGRESO', '{OrigenIngresoG0461}', o.lote_id, 223,
       o.consumo, 'kg',
       'Ingreso de alimento no registrado, repuesto por cuadre 2026-07-29 ('
         || TO_CHAR(o.consumo, 'FM999999990.000') || ' kg consumidos sin llegada cargada)',
       false
  FROM objetivo o
ON CONFLICT (origen_tabla, origen_id) DO NOTHING;");

            // ── 3) Alinear el stock del inventario al saldo logico de cada galpon ────
            migrationBuilder.Sql(@"
-- Saldo logico por galpon = ingresos vigentes del historico − consumo real del seguimiento.
-- Tabla real (no TEMP): EF manda cada Sql() como comando propio y una temporal con ON COMMIT DROP
-- dependeria de que todo viaje en la misma transaccion. Se elimina al final del Up.
DROP TABLE IF EXISTS _cuadre_objetivo;
CREATE TABLE _cuadre_objetivo AS
WITH g AS (
    SELECT DISTINCT l.granja_id fid, COALESCE(TRIM(l.nucleo_id),'') nu,
           COALESCE(TRIM(l.galpon_id),'') ga, l.company_id
      FROM lote_ave_engorde l
     WHERE l.company_id = 5 AND l.deleted_at IS NULL
       AND EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                    WHERE s.lote_ave_engorde_id = l.lote_ave_engorde_id)
),
ing AS (
    SELECT h.farm_id fid, COALESCE(TRIM(h.nucleo_id),'') nu, COALESCE(TRIM(h.galpon_id),'') ga,
           SUM(CASE WHEN h.tipo_evento = 'INV_INGRESO'
                     AND NOT (h.referencia IS NOT NULL
                              AND h.referencia LIKE 'Seguimiento aves engorde #%')
                    THEN COALESCE(h.cantidad_kg,0)
                    WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg,0)
                    WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg,0))
                    ELSE 0 END) kg
      FROM lote_registro_historico_unificado h
     WHERE h.company_id = 5 AND NOT h.anulado
       AND h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
       -- Mismo filtro que fn_seguimiento_diario_engorde: las devoluciones por eliminacion son
       -- asientos de reversion del inventario fisico, no alimento que llega. Sin esta linea el
       -- saldo objetivo no coincide con el que muestra la pantalla (G0479: 590 kg de diferencia).
       AND NOT (h.referencia IS NOT NULL AND (
                h.referencia LIKE '%devolución por eliminación%'
             OR h.referencia LIKE '%devolucion por eliminacion%'))
     GROUP BY 1,2,3
),
cons AS (
    SELECT l.granja_id fid, COALESCE(TRIM(l.nucleo_id),'') nu, COALESCE(TRIM(l.galpon_id),'') ga,
           SUM(COALESCE(s.consumo_kg_hembras,0) + COALESCE(s.consumo_kg_machos,0)) kg
      FROM lote_ave_engorde l
      JOIN seguimiento_diario_aves_engorde s ON s.lote_ave_engorde_id = l.lote_ave_engorde_id
     WHERE l.company_id = 5 AND l.deleted_at IS NULL
     GROUP BY 1,2,3
),
stk AS (
    SELECT s.farm_id fid, COALESCE(TRIM(s.nucleo_id),'') nu, COALESCE(TRIM(s.galpon_id),'') ga,
           SUM(s.quantity) q
      FROM inventario_gestion_stock s WHERE s.company_id = 5 GROUP BY 1,2,3
)
SELECT g.fid, g.nu, g.ga, g.company_id,
       ROUND(COALESCE(ing.kg,0) - COALESCE(cons.kg,0), 3) AS saldo_logico,
       ROUND(COALESCE(stk.q,0), 3)                        AS stock_hoy,
       ROUND(COALESCE(ing.kg,0) - COALESCE(cons.kg,0) - COALESCE(stk.q,0), 3) AS delta
  FROM g
  LEFT JOIN ing  ON (ing.fid, ing.nu, ing.ga)   = (g.fid, g.nu, g.ga)
  LEFT JOIN cons ON (cons.fid, cons.nu, cons.ga) = (g.fid, g.nu, g.ga)
  LEFT JOIN stk  ON (stk.fid, stk.nu, stk.ga)   = (g.fid, g.nu, g.ga);");

            // 3a) BAJAR: descontar en cascada 223 (preiniciador, el del cruce) → 214 → 213.
            migrationBuilder.Sql(@"
WITH objetivo AS (SELECT * FROM _cuadre_objetivo WHERE delta < -0.005),
filas AS (
    SELECT s.id AS stock_id, s.quantity, o.delta, s.item_inventario_ecuador_id item,
           SUM(s.quantity) OVER (PARTITION BY s.farm_id, COALESCE(TRIM(s.nucleo_id),''),
                                              COALESCE(TRIM(s.galpon_id),'')
                                 ORDER BY CASE s.item_inventario_ecuador_id
                                            WHEN 223 THEN 1 WHEN 214 THEN 2 ELSE 3 END,
                                          s.item_inventario_ecuador_id
                                 ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS ya_cubierto
      FROM inventario_gestion_stock s
      JOIN objetivo o ON o.fid = s.farm_id
                     AND o.nu  = COALESCE(TRIM(s.nucleo_id),'')
                     AND o.ga  = COALESCE(TRIM(s.galpon_id),'')
     WHERE s.company_id = 5 AND s.quantity > 0
),
calc AS (
    SELECT stock_id, quantity, item,
           LEAST(quantity, GREATEST(0, ABS(delta) - COALESCE(ya_cubierto,0))) AS baja
      FROM filas
),
aplica AS (SELECT * FROM calc WHERE baja > 0.005),
mov AS (
    INSERT INTO inventario_gestion_movimiento (
        company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
        quantity, unit, movement_type, reason, estado, created_at, created_by_user_id)
    SELECT s.company_id, s.pais_id, s.farm_id, s.nucleo_id, s.galpon_id, s.item_inventario_ecuador_id,
           a.baja, s.unit, 'AjusteStock',
           'Ajuste manual. Anterior: ' || TRIM(TO_CHAR(a.quantity, 'FM999999990.999')) || ' ' || s.unit
             || '. Nuevo: ' || TRIM(TO_CHAR(a.quantity - a.baja, 'FM999999990.999')) || ' ' || s.unit
             || '. Motivo: cuadre 2026-07-29, consumo de los 7 dias de cruce nunca descontado.',
           'Ajuste manual', now(), NULL
      FROM aplica a JOIN inventario_gestion_stock s ON s.id = a.stock_id
    RETURNING 1
),
bkp AS (
    INSERT INTO _backup_cuadre_alimento_20260729 (accion, stock_id, valor_anterior)
    SELECT 'stock_bajado', a.stock_id, a.quantity FROM aplica a
    RETURNING 1
)
UPDATE inventario_gestion_stock s
   SET quantity = s.quantity - a.baja, updated_at = now()
  FROM aplica a WHERE s.id = a.stock_id;");

            // 3b) SUBIR: devolver al item con mas stock del galpon (o crear la fila del 223).
            migrationBuilder.Sql(@"
WITH objetivo AS (SELECT * FROM _cuadre_objetivo WHERE delta > 0.005),
destino AS (
    SELECT DISTINCT ON (o.fid, o.nu, o.ga) o.fid, o.nu, o.ga, o.delta, s.id AS stock_id, s.quantity
      FROM objetivo o
      JOIN inventario_gestion_stock s ON s.company_id = 5 AND s.farm_id = o.fid
                                     AND COALESCE(TRIM(s.nucleo_id),'') = o.nu
                                     AND COALESCE(TRIM(s.galpon_id),'') = o.ga
     ORDER BY o.fid, o.nu, o.ga, s.quantity DESC, s.id
),
mov AS (
    INSERT INTO inventario_gestion_movimiento (
        company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_ecuador_id,
        quantity, unit, movement_type, reason, estado, created_at, created_by_user_id)
    SELECT s.company_id, s.pais_id, s.farm_id, s.nucleo_id, s.galpon_id, s.item_inventario_ecuador_id,
           d.delta, s.unit, 'AjusteStock',
           'Ajuste manual. Anterior: ' || TRIM(TO_CHAR(d.quantity, 'FM999999990.999')) || ' ' || s.unit
             || '. Nuevo: ' || TRIM(TO_CHAR(d.quantity + d.delta, 'FM999999990.999')) || ' ' || s.unit
             || '. Motivo: cuadre 2026-07-29, se habia descontado de mas al compensar a mano.',
           'Ajuste manual', now(), NULL
      FROM destino d JOIN inventario_gestion_stock s ON s.id = d.stock_id
    RETURNING 1
),
bkp AS (
    INSERT INTO _backup_cuadre_alimento_20260729 (accion, stock_id, valor_anterior)
    SELECT 'stock_subido', d.stock_id, d.quantity FROM destino d
    RETURNING 1
)
UPDATE inventario_gestion_stock s
   SET quantity = s.quantity + d.delta, updated_at = now()
  FROM destino d WHERE s.id = d.stock_id;");

            migrationBuilder.Sql("DROP TABLE IF EXISTS _cuadre_objetivo;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE inventario_gestion_stock s
   SET quantity = b.valor_anterior, updated_at = now()
  FROM _backup_cuadre_alimento_20260729 b
 WHERE b.stock_id = s.id AND b.accion IN ('stock_bajado','stock_subido');");

            migrationBuilder.Sql(@"
DELETE FROM inventario_gestion_movimiento
 WHERE movement_type = 'AjusteStock'
   AND reason LIKE '%cuadre 2026-07-29%';");

            migrationBuilder.Sql($@"
DELETE FROM lote_registro_historico_unificado WHERE origen_tabla = '{OrigenIngresoG0461}';");

            migrationBuilder.Sql(@"
UPDATE lote_registro_historico_unificado t
   SET anulado = false
  FROM _backup_cuadre_alimento_20260729 b
 WHERE t.id = b.historico_id AND b.accion = 'anulado_g0486';");

            migrationBuilder.Sql("DROP TABLE IF EXISTS _backup_cuadre_alimento_20260729;");
        }
    }
}
