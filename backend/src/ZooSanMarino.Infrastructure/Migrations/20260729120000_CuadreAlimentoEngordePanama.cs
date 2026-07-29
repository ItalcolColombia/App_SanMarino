using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cuadre del saldo de alimento del seguimiento diario de engorde contra Gestion de inventario
    /// (Panama). Dos correcciones de DATOS, ambas autorizadas explicitamente por el usuario.
    ///
    /// <b>1) G0486 (MENDOZA): ingresos de G0485 cargados encima.</b>
    /// La carga masiva del 2026-07-28 corrio dos veces (20:53 y 20:56). Solo G0486 recibio filas de
    /// las DOS pasadas, y la segunda (18 filas, 128.302,2 kg) es identica en cantidad de filas y en
    /// kilos al total de G0485 — son sus ingresos, cargados en el galpon equivocado. El seguimiento
    /// mostraba 135.339,1 kg contra 8.170,9 reales. Se ANULAN esas 18 filas del historico (el
    /// inventario NO se toca: su stock ya fue corregido a mano). El galpon pasa a −1.134,0.
    /// Guarda fail-safe: solo anula si el conjunto es EXACTAMENTE 18 filas por 128.302,2 kg; si en
    /// produccion los datos difieren no hace nada, en vez de anular lo que no corresponde.
    ///
    /// <b>2) DAYLAND: ajuste del seguimiento al stock actual.</b>
    /// Sus 5 galpones (G0460, G0461, G0463, G0464, G0465) son los unicos con lotes de engorde que
    /// NO tienen ningun AjusteStock: el desfase entre el consumo que descuenta el inventario y el
    /// que registra el seguimiento nunca se compenso ahi. Se inserta un ingreso de cuadre datado en
    /// el ultimo seguimiento del galpon, con la diferencia contra el stock. Es semanticamente un
    /// ingreso: el consumo del seguimiento supero a los ingresos registrados porque faltaron kilos
    /// por registrar. El delta se calcula CONTRA LOS DATOS DEL MOMENTO, no contra constantes.
    ///
    /// <b>Lo que NO se toca (decision explicita del usuario):</b> los 11 galpones cuyo residuo viene
    /// de los ajustes manuales de stock (−816 a +1.860 kg sobre saldos de 6.000 a 21.000). El
    /// <c>NOT EXISTS</c> sobre AjusteStock los excluye por construccion.
    ///
    /// Idempotente: la anulacion deja de encontrar candidatas y el INSERT se protege con la clave
    /// unica (origen_tabla, origen_id). Con respaldo para que el Down revierta solo lo suyo.
    /// Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md
    /// </summary>
    public partial class CuadreAlimentoEngordePanama : Migration
    {
        private const string OrigenAjuste = "ajuste_cuadre_alimento_20260729";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS _backup_cuadre_alimento_20260729 (
    historico_id bigint PRIMARY KEY,
    accion       text   NOT NULL,
    anulado_at   timestamptz NOT NULL DEFAULT now()
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
-- Fail-safe: si el conjunto no es exactamente el que se audito, no se anula NADA.
control AS (
    SELECT COUNT(*) AS n, COALESCE(SUM(cantidad_kg), 0) AS kg FROM candidatas
),
elegidas AS (
    SELECT c.id FROM candidatas c, control ct
     WHERE ct.n = 18 AND ct.kg BETWEEN 128302.0 AND 128302.4
),
bkp AS (
    INSERT INTO _backup_cuadre_alimento_20260729 (historico_id, accion)
    SELECT e.id, 'anulado_g0486' FROM elegidas e
    ON CONFLICT (historico_id) DO NOTHING
    RETURNING 1
)
UPDATE lote_registro_historico_unificado t
   SET anulado = true
  FROM elegidas e
 WHERE t.id = e.id;");

            // ── 2) DAYLAND: ingreso de cuadre contra el stock ────────────────────────
            migrationBuilder.Sql($@"
WITH rep AS (
    -- Un lote representativo por galpon (en DAYLAND hay uno solo por galpon).
    SELECT DISTINCT ON (l.granja_id, COALESCE(TRIM(l.nucleo_id), ''), COALESCE(TRIM(l.galpon_id), ''))
           l.lote_ave_engorde_id      AS lote_id,
           l.company_id, l.granja_id, l.nucleo_id, l.galpon_id,
           COALESCE(TRIM(l.nucleo_id), '') AS nu,
           COALESCE(TRIM(l.galpon_id), '') AS ga
      FROM lote_ave_engorde l
     WHERE l.company_id = 5
       AND l.granja_id  = 107          -- DAYLAND
       AND l.deleted_at IS NULL
       AND EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                    WHERE s.lote_ave_engorde_id = l.lote_ave_engorde_id)
     ORDER BY l.granja_id, COALESCE(TRIM(l.nucleo_id), ''), COALESCE(TRIM(l.galpon_id), ''),
              l.lote_ave_engorde_id
),
saldo AS (
    SELECT r.*,
           (SELECT f.saldo_alimento_kg
              FROM fn_seguimiento_diario_engorde(r.lote_id) f
             ORDER BY f.fecha DESC LIMIT 1)::numeric        AS saldo_seg,
           (SELECT MAX(DATE(s.fecha))
              FROM seguimiento_diario_aves_engorde s
              JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s.lote_ave_engorde_id
                                      AND l2.deleted_at IS NULL
             WHERE l2.granja_id = r.granja_id
               AND COALESCE(TRIM(l2.nucleo_id), '') = r.nu
               AND COALESCE(TRIM(l2.galpon_id), '') = r.ga) AS fecha_ajuste
      FROM rep r
),
stk AS (
    SELECT s.farm_id,
           COALESCE(TRIM(s.nucleo_id), '') AS nu,
           COALESCE(TRIM(s.galpon_id), '') AS ga,
           SUM(s.quantity) AS q
      FROM inventario_gestion_stock s
     WHERE s.company_id = 5
     GROUP BY 1, 2, 3
),
ajustes AS (
    SELECT sa.*, (COALESCE(st.q, 0) - sa.saldo_seg) AS delta
      FROM saldo sa
      LEFT JOIN stk st ON st.farm_id = sa.granja_id AND st.nu = sa.nu AND st.ga = sa.ga
     WHERE sa.saldo_seg IS NOT NULL
       AND sa.fecha_ajuste IS NOT NULL
       AND ABS(COALESCE(st.q, 0) - sa.saldo_seg) > 1
       -- Excluye los galpones que el usuario YA ajusto a mano: su residuo se deja como esta.
       AND NOT EXISTS (
             SELECT 1
               FROM lote_registro_historico_unificado h
               JOIN inventario_gestion_movimiento m
                 ON m.id = h.origen_id AND h.origen_tabla = 'inventario_gestion_movimiento'
              WHERE m.movement_type = 'AjusteStock'
                AND h.farm_id = sa.granja_id
                AND COALESCE(TRIM(h.nucleo_id), '') = sa.nu
                AND COALESCE(TRIM(h.galpon_id), '') = sa.ga)
)
INSERT INTO lote_registro_historico_unificado (
    company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id, fecha_operacion,
    tipo_evento, origen_tabla, origen_id, cantidad_kg, unidad, referencia, anulado)
SELECT a.company_id, a.lote_id, a.granja_id, a.nucleo_id, a.galpon_id, a.fecha_ajuste,
       CASE WHEN a.delta > 0 THEN 'INV_INGRESO' ELSE 'INV_TRASLADO_SALIDA' END,
       '{OrigenAjuste}', a.lote_id,
       ABS(a.delta), 'kg',
       'Ajuste de cuadre con Gestion de inventario 2026-07-29 ('
         || TO_CHAR(ABS(a.delta), 'FM999999990.000') || ' kg no registrados)',
       false
  FROM ajustes a
ON CONFLICT (origen_tabla, origen_id) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM lote_registro_historico_unificado WHERE origen_tabla = '{OrigenAjuste}';");

            migrationBuilder.Sql(@"
UPDATE lote_registro_historico_unificado t
   SET anulado = false
  FROM _backup_cuadre_alimento_20260729 b
 WHERE t.id = b.historico_id AND b.accion = 'anulado_g0486';");

            migrationBuilder.Sql("DROP TABLE IF EXISTS _backup_cuadre_alimento_20260729;");
        }
    }
}
