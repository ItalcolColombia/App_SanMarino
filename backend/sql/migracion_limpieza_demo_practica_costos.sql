-- =====================================================================================================
-- migracion_limpieza_demo_practica_costos.sql
--
-- Deja la empresa DEMO en blanco para la practica de carga masiva del equipo de costos de SanMarino:
-- borra los DATOS OPERATIVOS y CONSERVA la estructura (granjas, nucleos, galpones), la configuracion,
-- los catalogos, la guia genetica, los usuarios y los roles.
--
-- Acompania a la migracion 20260828180000_DemoListaParaPracticaCargaMasivaCostos (habilitacion) y al
-- plan fase_de_desarrollo/demo_lista_practica_carga_masiva_costos_plan.md
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POR QUE ESTO **NO** VA POR MIGRACION EF
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- Una migracion que borra datos operativos es una bomba: se re-ejecutaria en CUALQUIER entorno
-- levantado desde cero (un dev que clona el repo, un ambiente de QA nuevo) y no hay Down() que la
-- deshaga. Va como operativo de UNA SOLA VEZ, que es exactamente lo que el gate
-- `verificar-sql-llega-por-migracion.js` exime por prefijo `migracion_*`:
-- "operativos de una sola vez que quedan como registro de lo que se hizo".
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- COMO SE CORRE  (NO ejecutar a ciegas)
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
--   1) ENSAYO  — con la linea de ROLLBACK activa (asi viene el archivo). Mira los conteos ANTES y
--                DESPUES y confirma que las otras 4 empresas no se mueven ni una fila.
--   2) REAL    — recien despues, cambiar `ROLLBACK;` por `COMMIT;` al final y volver a correr.
--
--   psql -h <host> -p <port> -U postgres -d <db> -v ON_ERROR_STOP=1 \
--        -f backend/sql/migracion_limpieza_demo_practica_costos.sql
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- INVARIANTES QUE RESPETA
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
--  * FAIL-CLOSED POR EMPRESA. Toda sentencia filtra por el id de Demo resuelto desde `identifier`
--    ('1111738751'), nunca por `name` (texto libre: una tilde de mas y el DELETE no encuentra nada
--    ... o encuentra otra cosa). Si la empresa no existe, `v_company_id` queda NULL y TODOS los
--    DELETE borran cero filas: ninguno corre sin el filtro.
--
--  * EL HISTORICO UNIFICADO NO QUEDA HUERFANO. `lote_registro_historico_unificado` la llena un
--    trigger AFTER INSERT: ningun UPDATE ni DELETE del origen se propaga solo. Como aca se borra el
--    lote ENTERO (no se deshace un movimiento), la fila del historico se borra explicitamente en el
--    mismo paso — no se deja "anulada" ni contando saldo.
--
--  * NO SE TOCA LA ESTRUCTURA: farms, nucleos, galpones, guia_genetica_sanmarino_colombia,
--    item_inventario_ecuador, catalogo_items, master_lists, company_*, role_*, user_*.
-- =====================================================================================================

\set ON_ERROR_STOP on
\timing off

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0) Resolver la empresa UNA vez y dejarla en una tabla temporal. Todo el script la lee de aca:
--    asi el filtro no puede olvidarse en ninguna sentencia.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
CREATE TEMP TABLE _demo(company_id int) ON COMMIT DROP;
INSERT INTO _demo(company_id)
SELECT id FROM public.companies WHERE identifier = '1111738751';

DO $$
DECLARE v_n int;
BEGIN
    SELECT count(*) INTO v_n FROM _demo;
    IF v_n <> 1 THEN
        RAISE EXCEPTION 'Se esperaba EXACTAMENTE una empresa con identifier=1111738751 y se encontraron %. Abortado sin tocar nada.', v_n;
    END IF;
    RAISE NOTICE 'Empresa Demo resuelta: company_id = %', (SELECT company_id FROM _demo);
END $$;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 1) FOTO ANTES  — queda en el log para auditoria.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
\echo ''
\echo '=============== CONTEOS ANTES (empresa Demo) ==============='
SELECT 'historico_lote_postura'            t, count(*) n FROM public.historico_lote_postura            WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'seguimiento_diario_levante', count(*) FROM public.seguimiento_diario_levante s
          WHERE EXISTS (SELECT 1 FROM public.lote_postura_levante l
                         WHERE l.lote_postura_levante_id = s.lote_postura_levante_id
                           AND l.company_id IN (SELECT company_id FROM _demo))
UNION ALL SELECT 'seguimiento_diario_produccion',     count(*) FROM public.seguimiento_diario_produccion     WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_base',                 count(*) FROM public.lote_postura_base                 WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_levante',              count(*) FROM public.lote_postura_levante              WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_produccion',           count(*) FROM public.lote_postura_produccion           WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lotes',                             count(*) FROM public.lotes                             WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_registro_historico_unificado', count(*) FROM public.lote_registro_historico_unificado WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_gestion_movimiento',     count(*) FROM public.inventario_gestion_movimiento     WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_gestion_stock',          count(*) FROM public.inventario_gestion_stock          WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'farm_inventory_movements',          count(*) FROM public.farm_inventory_movements          WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'farm_product_inventory',            count(*) FROM public.farm_product_inventory            WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'espejo_huevo_produccion',           count(*) FROM public.espejo_huevo_produccion           WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'liquidacion_cierre_lote_levante',   count(*) FROM public.liquidacion_cierre_lote_levante   WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_aves',                   count(*) FROM public.inventario_aves                   WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'migracion_masiva',                  count(*) FROM public.migracion_masiva                  WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT '-- ESTRUCTURA (debe SOBREVIVIR) --', NULL
UNION ALL SELECT 'farms',    count(*) FROM public.farms    WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'galpones', count(*) FROM public.galpones WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'nucleos',  count(*) FROM public.nucleos n
          WHERE EXISTS (SELECT 1 FROM public.farms f WHERE f.id = n.granja_id
                          AND f.company_id IN (SELECT company_id FROM _demo));

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 2) BORRADO. El orden respeta las FK: lo que apunta con RESTRICT va ANTES que su padre.
--    Medido con pg_constraint el 28-ago-2026 sobre la copia de produccion.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

-- 2.1 Hojas que apuntan con RESTRICT a los lotes (hoy 0 filas en Demo, van igual por si cambia).
DELETE FROM public.vacunacion_cronograma_item vci
 WHERE EXISTS (SELECT 1 FROM public.lote_postura_levante l
                WHERE l.lote_postura_levante_id = vci.lote_postura_levante_id
                  AND l.company_id IN (SELECT company_id FROM _demo))
    OR EXISTS (SELECT 1 FROM public.lote_postura_produccion p
                WHERE p.lote_postura_produccion_id = vci.lote_postura_produccion_id
                  AND p.company_id IN (SELECT company_id FROM _demo));

DELETE FROM public.traslado_huevos WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.2 Seguimientos diarios. El de levante se borra por su propio vinculo, sin depender del CASCADE
--     via `lote_id_int`: esa columna es el legado y puede venir NULL (ver memoria lote_id_int).
DELETE FROM public.seguimiento_diario_levante s
 WHERE EXISTS (SELECT 1 FROM public.lote_postura_levante l
                WHERE l.lote_postura_levante_id = s.lote_postura_levante_id
                  AND l.company_id IN (SELECT company_id FROM _demo))
    OR EXISTS (SELECT 1 FROM public.lote_postura_produccion p
                WHERE p.lote_postura_produccion_id = s.lote_postura_produccion_id
                  AND p.company_id IN (SELECT company_id FROM _demo))
    OR EXISTS (SELECT 1 FROM public.lotes lo
                WHERE lo.lote_id = s.lote_id_int
                  AND lo.company_id IN (SELECT company_id FROM _demo));

DELETE FROM public.seguimiento_diario_produccion WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.3 Reservas del seguimiento (hoy 0 en Demo; el DELETE es la garantia de que no queden colgadas).
DELETE FROM public.seguimiento_reserva_alimento WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.seguimiento_reserva_aves     WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.4 Historicos y espejos. El unificado se borra EXPLICITAMENTE: lo llena un trigger AFTER INSERT
--     y ningun DELETE del origen se propaga solo.
DELETE FROM public.lote_registro_historico_unificado WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.historico_lote_postura            WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.espejo_huevo_produccion           WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.liquidacion_cierre_lote_levante   WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.5 Inventario: el nuevo (unificado) y el viejo. Los dos, porque los reportes leen uno u otro
--     segun `reportes_alimento_desde_inventario_unificado` y la practica tiene que arrancar en cero
--     mire donde mire.
DELETE FROM public.inventario_gestion_movimiento WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.inventario_gestion_stock      WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.farm_inventory_movements      WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.farm_product_inventory        WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.inventario_aves               WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.inventario_gasto              WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.historial_inventario          WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.6 Movimientos de aves.
DELETE FROM public.movimiento_aves WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.7 Los lotes. Produccion primero (apunta con RESTRICT a levante), despues levante, despues el
--     maestro `lotes` y por ultimo `lote_postura_base` (al que `lotes` apunta con RESTRICT).
DELETE FROM public.lote_postura_produccion WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.lote_postura_levante    WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.lote_aves_cohortes      WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.lotes                   WHERE company_id IN (SELECT company_id FROM _demo);
DELETE FROM public.lote_postura_base       WHERE company_id IN (SELECT company_id FROM _demo);

-- 2.8 Historial de corridas previas de carga masiva: la bandeja del modulo arranca limpia.
DELETE FROM public.migracion_masiva WHERE company_id IN (SELECT company_id FROM _demo);

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 3) FOTO DESPUES  — los operativos tienen que dar 0 y la estructura NO tiene que moverse.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
\echo ''
\echo '=============== CONTEOS DESPUES (empresa Demo) ==============='
SELECT 'historico_lote_postura'            t, count(*) n FROM public.historico_lote_postura            WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'seguimiento_diario_levante', count(*) FROM public.seguimiento_diario_levante s
          WHERE EXISTS (SELECT 1 FROM public.lote_postura_levante l
                         WHERE l.lote_postura_levante_id = s.lote_postura_levante_id
                           AND l.company_id IN (SELECT company_id FROM _demo))
UNION ALL SELECT 'seguimiento_diario_produccion',     count(*) FROM public.seguimiento_diario_produccion     WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_base',                 count(*) FROM public.lote_postura_base                 WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_levante',              count(*) FROM public.lote_postura_levante              WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_postura_produccion',           count(*) FROM public.lote_postura_produccion           WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lotes',                             count(*) FROM public.lotes                             WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'lote_registro_historico_unificado', count(*) FROM public.lote_registro_historico_unificado WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_gestion_movimiento',     count(*) FROM public.inventario_gestion_movimiento     WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_gestion_stock',          count(*) FROM public.inventario_gestion_stock          WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'farm_inventory_movements',          count(*) FROM public.farm_inventory_movements          WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'farm_product_inventory',            count(*) FROM public.farm_product_inventory            WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'espejo_huevo_produccion',           count(*) FROM public.espejo_huevo_produccion           WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'liquidacion_cierre_lote_levante',   count(*) FROM public.liquidacion_cierre_lote_levante   WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'inventario_aves',                   count(*) FROM public.inventario_aves                   WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'migracion_masiva',                  count(*) FROM public.migracion_masiva                  WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT '-- ESTRUCTURA (debe SOBREVIVIR) --', NULL
UNION ALL SELECT 'farms',    count(*) FROM public.farms    WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'galpones', count(*) FROM public.galpones WHERE company_id IN (SELECT company_id FROM _demo)
UNION ALL SELECT 'nucleos',  count(*) FROM public.nucleos n
          WHERE EXISTS (SELECT 1 FROM public.farms f WHERE f.id = n.granja_id
                          AND f.company_id IN (SELECT company_id FROM _demo));

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 4) CONTROL MULTIEMPRESA: las otras 4 empresas NO se pueden haber movido. Comparar a ojo contra la
--    misma consulta corrida antes del script; cualquier diferencia es una regresion.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
\echo ''
\echo '=============== CONTROL: OTRAS EMPRESAS (no deben moverse) ==============='
SELECT c.id, c.name,
       (SELECT count(*) FROM public.lotes                         x WHERE x.company_id = c.id) lotes,
       (SELECT count(*) FROM public.lote_postura_levante          x WHERE x.company_id = c.id) levante,
       (SELECT count(*) FROM public.lote_postura_produccion       x WHERE x.company_id = c.id) produccion,
       (SELECT count(*) FROM public.inventario_gestion_movimiento x WHERE x.company_id = c.id) inv_mov,
       (SELECT count(*) FROM public.historico_lote_postura        x WHERE x.company_id = c.id) historico
  FROM public.companies c
 WHERE c.id NOT IN (SELECT company_id FROM _demo)
 ORDER BY c.id;

-- =====================================================================================================
-- ENSAYO por defecto. Para ejecutar DE VERDAD, cambiar la linea de abajo por  COMMIT;
-- =====================================================================================================
ROLLBACK;
