using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Prevencion de descuadres de alimento de engorde: el invariante lo garantiza y lo verifica la BD.
    /// <para><b>1. Coherencia del historico (triggers).</b>
    /// <c>trg_inventario_gestion_movimiento_lote_hist</c> era el UNICO de los 8 triggers de la base que
    /// es solo AFTER INSERT, y la tabla borra filas fisicamente. Mantener alineado el historico dependia
    /// de que cada camino del C# se acordara de marcar <c>anulado</c> a mano: habia cuatro y dos se
    /// olvidaban. Se agregan dos triggers —AFTER DELETE y AFTER UPDATE del movement_type a un tipo
    /// cancelado— que anulan la fila. Es el mismo patron que el modulo gemelo
    /// <c>movimiento_pollo_engorde</c> ya usaba con <c>trg_..._lote_hist_anula</c>. Con esto ningun
    /// camino futuro, ni un DELETE manual por SQL, puede dejar el saldo contando movimientos deshechos.
    /// </para>
    /// <para><b>2. El invariante como funcion verificable.</b>
    /// <c>fn_cuadre_alimento_engorde(company_id)</c> devuelve, por galpon, si
    /// <c>saldo del ciclo activo == stock fisico − movimientos posteriores al ultimo seguimiento</c>.
    /// Es la misma verificacion con la que se validaron los fixes de jul-2026 (Ecuador 35/35 y Panama
    /// 25/25 con error 0,0). El descuadre que origino todo ese trabajo lo detecto un humano de
    /// operacion semanas despues; nada en el sistema lo verificaba.
    /// </para>
    /// <para>
    /// Idempotente: <c>CREATE OR REPLACE FUNCTION</c> y <c>DROP TRIGGER IF EXISTS</c> antes de crear.
    /// SQL sincronizado con backend/sql/trg_inventario_gestion_anular_historico.sql y
    /// backend/sql/fn_cuadre_alimento_engorde.sql.
    /// </para>
    /// Plan: fase_de_desarrollo/prevencion_descuadres_alimento_engorde_plan.md
    /// </summary>
    public partial class PrevencionDescuadresAlimentoEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- =============================================================================
-- Coherencia del histórico unificado cuando un movimiento de inventario se DESHACE.
--
-- EL PROBLEMA (jul-2026)
-- `lote_registro_historico_unificado` la llena el trigger
-- `trg_inventario_gestion_movimiento_lote_hist`, que es **AFTER INSERT y nada más**: ningún UPDATE ni
-- DELETE del movimiento se propagaba. Mantener la coherencia dependía de que cada camino del C# se
-- acordara de marcar `anulado = true` a mano. Había cuatro y dos se olvidaban:
--   * `AnularMovimientoHistoricoAsync` borraba el movimiento y dejaba la fila HUÉRFANA, así que el
--     saldo de alimento seguía contando un ingreso que ya había salido del stock.
--   * `RechazarTransitoPendienteAsync` cambiaba el `movement_type`, pero la fila conservaba su
--     `tipo_evento` (`TrasladoInterGranjaPendiente` mapea a INV_TRASLADO_SALIDA), así que el galpón de
--     origen seguía descontando una salida que nunca ocurrió.
--
-- LA SOLUCIÓN
-- Que el invariante lo garantice la BASE DE DATOS, no la disciplina del código. Es exactamente lo que
-- ya hace el módulo gemelo `movimiento_pollo_engorde` con `trg_..._lote_hist_anula`.
-- Con estos dos triggers ningún camino futuro —ni un DELETE manual por SQL— puede dejar el histórico
-- desalineado. El C# que anula a mano queda redundante pero se conserva: es idempotente y explícito.
--
-- POR QUÉ NO SE PASÓ A BORRADO LÓGICO
-- Dejar de borrar físicamente obligaría a auditar TODAS las lecturas de la tabla (GetStockAsync,
-- GetMovimientosAsync, GetTrasladosAsync, GetIngresosAsync, GetTransitosPendientesAsync…) y una sola
-- omitida resucita movimientos anulados. El AFTER DELETE cierra el agujero igual de bien para la
-- correctitud del saldo, con una fracción del riesgo. El borrado lógico aporta trazabilidad, no
-- correctitud, y queda como trabajo aparte.
-- =============================================================================

CREATE OR REPLACE FUNCTION trg_lote_hist_anular_desde_inventario_gestion()
RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    v_mov_id INTEGER;
BEGIN
    v_mov_id := CASE TG_OP WHEN 'DELETE' THEN OLD.id ELSE NEW.id END;

    UPDATE public.lote_registro_historico_unificado
       SET anulado = TRUE
     WHERE origen_tabla = 'inventario_gestion_movimiento'
       AND origen_id    = v_mov_id
       AND NOT anulado;

    RETURN CASE TG_OP WHEN 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

COMMENT ON FUNCTION trg_lote_hist_anular_desde_inventario_gestion() IS
'Marca anulada la fila del histórico unificado cuando su movimiento de inventario se borra o se cancela. '
'El trigger de alta es solo AFTER INSERT, así que sin esto el saldo de alimento contaría movimientos deshechos.';

-- 1) Cualquier DELETE, venga del C# o de un script manual.
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_del
    ON public.inventario_gestion_movimiento;
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist_del
AFTER DELETE ON public.inventario_gestion_movimiento
FOR EACH ROW
EXECUTE FUNCTION trg_lote_hist_anular_desde_inventario_gestion();

-- 2) Cancelación por cambio de tipo (hoy: rechazo de un tránsito entre granjas).
--    `fn_tipo_evento_inventario` manda los tipos cancelados a INV_OTRO, que el saldo ya ignora, pero
--    la fila vieja conserva su tipo_evento original: hay que anularla explícitamente.
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_cancel
    ON public.inventario_gestion_movimiento;
CREATE TRIGGER trg_inventario_gestion_movimiento_lote_hist_cancel
AFTER UPDATE OF movement_type ON public.inventario_gestion_movimiento
FOR EACH ROW
WHEN (NEW.movement_type = 'TrasladoInterGranjaRechazado')
EXECUTE FUNCTION trg_lote_hist_anular_desde_inventario_gestion();
");

            migrationBuilder.Sql(@"
-- =============================================================================
-- fn_cuadre_alimento_engorde(p_company_id INT DEFAULT NULL)
--
-- El INVARIANTE del alimento de pollo engorde, por galpón:
--
--     saldo del ciclo activo  ==  stock físico − movimientos posteriores al último seguimiento
--
-- Es la misma verificación con la que se validaron los fixes de jul-2026 (v11, v12, enganche del
-- recálculo y cierre de los huecos del histórico). En ese momento dio Ecuador 35/35 y Panamá 25/25
-- con error 0,0.
--
-- POR QUÉ EXISTE
-- El descuadre que originó todo ese trabajo lo detectó un humano de operación, semanas después de que
-- se produjo. Nada en el sistema lo verificaba. Con esta función el mismo defecto se ve el mismo día,
-- sin depender de que alguien abra un ticket.
--
-- POR QUÉ SE RESTAN LOS MOVIMIENTOS POSTERIORES
-- `saldo_alimento_kg` tiene una fila por día de seguimiento. El alimento que entra DESPUÉS del último
-- día cargado no tiene fila donde reflejarse: la tabla diaria lo muestra como fila de movimiento
-- suelta, pero la última fila de seguimiento no lo incluye. Restarlos es lo que hace comparable el
-- saldo contra el stock de hoy; sin eso, todo galpón con alimento recién recibido daría falso positivo.
-- =============================================================================

DROP FUNCTION IF EXISTS fn_cuadre_alimento_engorde(INT);

CREATE OR REPLACE FUNCTION fn_cuadre_alimento_engorde(p_company_id INT DEFAULT NULL)
RETURNS TABLE (
    company_id            INT,
    empresa               TEXT,
    granja_id             INT,
    granja                TEXT,
    nucleo_id             TEXT,
    galpon_id             TEXT,
    lote_ave_engorde_id   INT,
    lote_nombre           TEXT,
    estado_operativo_lote TEXT,
    ultimo_seguimiento    DATE,
    saldo_tabla_kg        DOUBLE PRECISION,
    mov_post_kg           DOUBLE PRECISION,
    stock_kg              DOUBLE PRECISION,
    esperado_kg           DOUBLE PRECISION,
    descuadre_kg          DOUBLE PRECISION,
    filas_negativas       INT
) LANGUAGE sql STABLE AS $$

WITH ciclos AS (
    SELECT l.lote_ave_engorde_id                    AS lote_id,
           l.company_id,
           l.lote_nombre,
           l.granja_id,
           COALESCE(TRIM(l.nucleo_id), '')          AS nuc,
           COALESCE(TRIM(l.galpon_id), '')          AS gal,
           COALESCE(l.estado_operativo_lote, '')    AS estado,
           sg.seg_max,
           ROW_NUMBER() OVER (PARTITION BY l.granja_id,
                                           COALESCE(TRIM(l.nucleo_id), ''),
                                           COALESCE(TRIM(l.galpon_id), '')
                              ORDER BY sg.seg_max DESC, l.lote_ave_engorde_id DESC) AS rn
    FROM lote_ave_engorde l
    JOIN LATERAL (
        SELECT MAX(s.fecha)::DATE AS seg_max
        FROM seguimiento_diario_aves_engorde s
        WHERE s.lote_ave_engorde_id = l.lote_ave_engorde_id
    ) sg ON TRUE
    WHERE l.deleted_at IS NULL
      AND sg.seg_max IS NOT NULL
      AND COALESCE(TRIM(l.galpon_id), '') <> ''
      AND (p_company_id IS NULL OR l.company_id = p_company_id)
),
activos AS (SELECT * FROM ciclos WHERE rn = 1),

-- Saldo al ÚLTIMO DÍA DE SEGUIMIENTO y cuántas filas quedan negativas, en una sola pasada por lote.
-- ⚠️ Tiene que ser el saldo en `seg_max`, NO el de la última fila que devuelve la fn: cuando hay
-- movimientos posteriores al último seguimiento, la fn agrega filas propias para ellos y ese saldo YA
-- los incluye. Tomarlo de ahí y encima restar `mov_post` los contaría dos veces y daría falsos
-- descuadres (se vio al estrenar esta función: 24/35 en Ecuador donde en realidad es 35/35).
tabla AS (
    SELECT a.lote_id, t.saldo_ultimo, t.negativas
    FROM activos a
    CROSS JOIN LATERAL (
        SELECT (ARRAY_AGG(f.saldo_alimento_kg ORDER BY f.fecha DESC)
                    FILTER (WHERE f.fecha <= a.seg_max))[1]                        AS saldo_ultimo,
               COUNT(*) FILTER (WHERE f.saldo_alimento_kg < -0.001)::INT           AS negativas
        FROM fn_seguimiento_diario_engorde(a.lote_id) f
    ) t
),

-- Alimento que se movió DESPUÉS del último seguimiento: no cabe en la tabla diaria.
post AS (
    SELECT a.lote_id,
           COALESCE((
               SELECT SUM(CASE h.tipo_evento
                          WHEN 'INV_INGRESO'          THEN COALESCE(h.cantidad_kg, 0)
                          WHEN 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
                          WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
                          ELSE 0 END)
               FROM lote_registro_historico_unificado h
               WHERE NOT h.anulado
                 AND h.farm_id = a.granja_id
                 AND COALESCE(TRIM(h.nucleo_id), '') = a.nuc
                 AND COALESCE(TRIM(h.galpon_id), '') = a.gal
                 AND DATE(h.fecha_operacion) > a.seg_max
                 AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
                 AND NOT (h.tipo_evento = 'INV_INGRESO'
                          AND h.referencia IS NOT NULL
                          AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 AND NOT (h.referencia IS NOT NULL AND (
                          h.referencia LIKE '%devolución por eliminación%'
                       OR h.referencia LIKE '%devolucion por eliminacion%'))
           ), 0)::FLOAT8 AS mov_post
    FROM activos a
),

stock AS (
    SELECT s.farm_id,
           COALESCE(TRIM(s.nucleo_id), '') AS nuc,
           COALESCE(TRIM(s.galpon_id), '') AS gal,
           SUM(s.quantity)::FLOAT8         AS stock_kg
    FROM inventario_gestion_stock s
    GROUP BY 1, 2, 3
)

SELECT a.company_id,
       co.name::TEXT,
       a.granja_id,
       f.name::TEXT,
       a.nuc::TEXT,
       a.gal::TEXT,
       a.lote_id,
       a.lote_nombre::TEXT,
       a.estado::TEXT,
       a.seg_max,
       COALESCE(t.saldo_ultimo, 0)::FLOAT8                                        AS saldo_tabla_kg,
       p.mov_post                                                                 AS mov_post_kg,
       COALESCE(st.stock_kg, 0)                                                   AS stock_kg,
       (COALESCE(st.stock_kg, 0) - p.mov_post)                                    AS esperado_kg,
       (COALESCE(t.saldo_ultimo, 0) - (COALESCE(st.stock_kg, 0) - p.mov_post))    AS descuadre_kg,
       COALESCE(t.negativas, 0)                                                   AS filas_negativas
FROM activos a
JOIN companies co ON co.id = a.company_id
JOIN farms     f  ON f.id  = a.granja_id
JOIN tabla     t  ON t.lote_id = a.lote_id
JOIN post      p  ON p.lote_id = a.lote_id
LEFT JOIN stock st ON st.farm_id = a.granja_id AND st.nuc = a.nuc AND st.gal = a.gal
ORDER BY ABS(COALESCE(t.saldo_ultimo, 0) - (COALESCE(st.stock_kg, 0) - p.mov_post)) DESC,
         co.name, f.name, a.gal;
$$;

COMMENT ON FUNCTION fn_cuadre_alimento_engorde(INT) IS
'Invariante del alimento de engorde por galpón: saldo del ciclo activo == stock físico − movimientos '
'posteriores al último seguimiento. Un descuadre distinto de 0 señala que la tabla diaria y el '
'inventario dejaron de contar lo mismo.';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_del
    ON public.inventario_gestion_movimiento;
DROP TRIGGER IF EXISTS trg_inventario_gestion_movimiento_lote_hist_cancel
    ON public.inventario_gestion_movimiento;
DROP FUNCTION IF EXISTS trg_lote_hist_anular_desde_inventario_gestion();
DROP FUNCTION IF EXISTS fn_cuadre_alimento_engorde(INT);
");
        }
    }
}
