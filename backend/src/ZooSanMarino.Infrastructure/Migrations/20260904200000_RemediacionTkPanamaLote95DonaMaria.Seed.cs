// Partial de la migracion RemediacionTkPanamaLote95DonaMaria: SOLO el SQL, para que el archivo
// principal se pueda leer. Data-only: no toca esquema ni ModelSnapshot.

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class RemediacionTkPanamaLote95DonaMaria
    {
        /// <summary>
        /// 1) Libera las reservas ACTIVA cuyos seguimientos cuelgan de un lote de engorde BORRADO.
        /// Criterio general, no una lista de ids: la misma situacion en otro galpon se corrige sola.
        /// Solo <c>ACTIVA</c> — una <c>APLICADA</c> ya descontio stock y devolverla es otra operacion.
        /// Medido sobre la copia de produccion del 04-sep-2026: 1 fila (32 kg, lote «PRUEBA - 1»).
        /// </summary>
        private const string LiberarReservasDeLotesBorrados = """
UPDATE seguimiento_reserva_alimento r
   SET estado = 'LIBERADA', liberada_at = NOW()
 WHERE r.estado = 'ACTIVA'
   AND r.origen_modulo IN ('ENGORDE', 'ENGORDE_EC')
   AND EXISTS (
        SELECT 1
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE s.id = r.origen_seguimiento_id
           AND l.deleted_at IS NOT NULL);

UPDATE seguimiento_reserva_aves r
   SET estado = 'LIBERADA', liberada_at = NOW()
 WHERE r.estado = 'ACTIVA'
   AND r.origen_modulo IN ('ENGORDE', 'ENGORDE_EC')
   AND EXISTS (
        SELECT 1
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE s.id = r.origen_seguimiento_id
           AND l.deleted_at IS NOT NULL);
""";

        /// <summary>
        /// 2) Devuelve al stock de DOÑA MARIA / A / G0475 (item 223, SM0175 preiniciador) los kilos
        /// que consumio la prueba del 28-ago-2026 sobre el lote de engorde «PRUEBA - 1», despues
        /// borrado: 7 seguimientos de reproductora precargados con fechas futuras y validados
        /// (150+100+125+34+56+31+12 = 508 kg) que descontaron stock de verdad.
        ///
        /// <para>
        /// El instrumento es <c>AjusteStock</c> y no un <c>Ingreso</c> a proposito: la TABLA DIARIA
        /// del ciclo nuevo ya esta bien (11.740 kg, porque el consumo pertenece a un lote borrado y
        /// la fn no lo cuenta) y el que quedo corto es el STOCK. <c>AjusteStock</c> se espeja como
        /// <c>INV_OTRO</c>, que la fn diaria no lee ⇒ mueve el stock sin tocar la grilla. Verificado
        /// en transaccion revertida sobre la copia: stock 11.232 → 11.740, grilla 11.740 antes y
        /// despues, y <c>esperado_kg</c> del cuadre pasa de −508 a 0.
        /// </para>
        ///
        /// <para>
        /// <b>Fail-closed.</b> Solo aplica si los 7 movimientos de consumo siguen ahi y suman
        /// exactamente 508 kg. Si produccion diverge de lo medido, no hace nada y deja el NOTICE.
        /// Idempotente por <c>reference</c>.
        /// </para>
        /// </summary>
        private const string DevolverConsumoDeLaPrueba = """
DO $$
DECLARE
    v_kg       NUMERIC;
    v_n        INT;
    v_stock_id INT;
    v_ant      NUMERIC;
    v_company  INT;
    v_pais     INT;
BEGIN
    IF EXISTS (SELECT 1 FROM inventario_gestion_movimiento
                WHERE reference = 'Remediacion TK lote 95 DONA MARIA A4 (prueba 28-ago-2026)') THEN
        RAISE NOTICE 'TK lote 95: el ajuste ya estaba aplicado, no se repite.';
        RETURN;
    END IF;

    SELECT COALESCE(SUM(quantity), 0), COUNT(*)
      INTO v_kg, v_n
      FROM inventario_gestion_movimiento
     WHERE id IN (14106, 14108, 14109, 14110, 14111, 14112, 14113)
       AND farm_id = 106
       AND COALESCE(TRIM(galpon_id), '') = 'G0475'
       AND item_inventario_id = 223
       AND movement_type = 'Consumo'
       AND reference LIKE 'Seguimiento reproductora #89%';

    IF v_n <> 7 OR v_kg <> 508 THEN
        RAISE NOTICE 'TK lote 95: el consumo de la prueba no coincide con lo medido (% movs, % kg); no se ajusta nada.', v_n, v_kg;
        RETURN;
    END IF;

    -- El lapiz de "editar stock" de la pantalla hace EXACTAMENTE este mismo AjusteStock. Si alguien
    -- corrigio los 508 kg a mano entre el diagnostico y el deploy, la guarda de arriba —que mira la
    -- CAUSA, los movimientos de consumo— seguiria pasando y sumariamos los kilos dos veces. Por eso
    -- se mira tambien el EFECTO: cualquier ajuste manual sobre ese galpon posterior al diagnostico
    -- cancela la remediacion, y se avisa para que se revise a mano.
    IF EXISTS (SELECT 1 FROM inventario_gestion_movimiento
                WHERE farm_id = 106
                  AND COALESCE(TRIM(galpon_id), '') = 'G0475'
                  AND item_inventario_id = 223
                  AND movement_type IN ('AjusteStock', 'EliminacionStock')
                  AND created_at >= TIMESTAMPTZ '2026-09-04 00:00:00-05') THEN
        RAISE NOTICE 'TK lote 95: ya hay un ajuste manual de stock en G0475 posterior al diagnostico; no se ajusta nada (revisar a mano).';
        RETURN;
    END IF;

    SELECT id, quantity, company_id, pais_id INTO v_stock_id, v_ant, v_company, v_pais
      FROM inventario_gestion_stock
     WHERE farm_id = 106
       AND COALESCE(TRIM(nucleo_id), '') = '147337'
       AND COALESCE(TRIM(galpon_id), '') = 'G0475'
       AND item_inventario_id = 223
       AND silo_id IS NULL;

    IF v_stock_id IS NULL THEN
        RAISE NOTICE 'TK lote 95: no existe la fila de stock del galpon G0475 / item 223; no se ajusta nada.';
        RETURN;
    END IF;

    -- Empresa y pais salen de la fila de stock, no de un literal: si el galpon cambiara de manos, el
    -- movimiento nace con el dueño real y no con el que estaba escrito cuando se redacto la migracion.
    INSERT INTO inventario_gestion_movimiento
        (company_id, pais_id, farm_id, nucleo_id, galpon_id, item_inventario_id, quantity, unit,
         movement_type, reference, reason, created_at, created_by_user_id, estado)
    VALUES
        (v_company, v_pais, 106, '147337', 'G0475', 223, v_kg, 'kg',
         'AjusteStock',
         'Remediacion TK lote 95 DONA MARIA A4 (prueba 28-ago-2026)',
         'Ajuste manual. Anterior: ' || v_ant || ' kg. Nuevo: ' || (v_ant + v_kg) ||
         ' kg. Motivo: devolucion del consumo de la prueba del 28-ago-2026 (lote PRUEBA - 1, borrado).',
         NOW(), NULL, 'Ajuste manual');

    UPDATE inventario_gestion_stock
       SET quantity = quantity + v_kg, updated_at = NOW()
     WHERE id = v_stock_id;

    RAISE NOTICE 'TK lote 95: devueltos % kg al stock de G0475 (% -> %).', v_kg, v_ant, v_ant + v_kg;
END $$;
""";

        /// <summary>
        /// Deshace el ajuste: resta los kilos del stock y borra el movimiento (el trigger
        /// <c>..._lote_hist_del</c> anula su fila del historico unificado, que es el patron correcto
        /// — nunca borrarla).
        /// </summary>
        private const string DeshacerDevolucion = """
DO $$
DECLARE
    v_id INT;
    v_kg NUMERIC;
BEGIN
    SELECT id, quantity INTO v_id, v_kg
      FROM inventario_gestion_movimiento
     WHERE reference = 'Remediacion TK lote 95 DONA MARIA A4 (prueba 28-ago-2026)'
     LIMIT 1;
    IF v_id IS NULL THEN RETURN; END IF;

    UPDATE inventario_gestion_stock
       SET quantity = quantity - v_kg, updated_at = NOW()
     WHERE farm_id = 106
       AND COALESCE(TRIM(nucleo_id), '') = '147337'
       AND COALESCE(TRIM(galpon_id), '') = 'G0475'
       AND item_inventario_id = 223
       AND silo_id IS NULL;

    DELETE FROM inventario_gestion_movimiento WHERE id = v_id;
END $$;
""";

        /// <summary>
        /// Devuelve a ACTIVA las reservas liberadas por esta migracion. Se localizan por el mismo
        /// criterio del <c>Up</c> (LIBERADA + lote borrado): la tabla no tiene columna donde dejar
        /// una marca propia, asi que un <c>Down</c> tambien revertiria una liberacion legitima
        /// anterior sobre un lote borrado. Es un rollback de emergencia, no una operacion de rutina.
        /// </summary>
        private const string RestaurarReservasDeLotesBorrados = """
UPDATE seguimiento_reserva_alimento r
   SET estado = 'ACTIVA', liberada_at = NULL
 WHERE r.estado = 'LIBERADA'
   AND r.origen_modulo IN ('ENGORDE', 'ENGORDE_EC')
   AND EXISTS (
        SELECT 1
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE s.id = r.origen_seguimiento_id
           AND l.deleted_at IS NOT NULL);

UPDATE seguimiento_reserva_aves r
   SET estado = 'ACTIVA', liberada_at = NULL
 WHERE r.estado = 'LIBERADA'
   AND r.origen_modulo IN ('ENGORDE', 'ENGORDE_EC')
   AND EXISTS (
        SELECT 1
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE s.id = r.origen_seguimiento_id
           AND l.deleted_at IS NOT NULL);
""";
    }
}
