\echo 'Este archivo NO se corre entero: se pega por BLOQUES en DB Studio (ver instrucciones).'
\quit
-- =====================================================================================================
-- migracion_limpieza_lotes_santa_reyes_capacitacion.sql
--
-- Deja la empresa SANTA REYES sin los LOTES creados en la aplicacion durante la capacitacion (y todo
-- lo que cuelga de ellos) para que el equipo los registre de nuevo desde cero.
--
-- Plan: fase_de_desarrollo/limpieza_lotes_santa_reyes_capacitacion_plan.md
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- ALCANCE (decidido por el usuario el 14-sep-2026)
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
--  BORRA   lotes, lote_postura_levante, lote_postura_produccion, cohortes, seguimientos diarios de
--          levante y produccion, reservas del seguimiento, historicos/espejos/liquidaciones de cierre,
--          traslados de huevo, movimientos de aves, cronograma de vacunacion de esos lotes, historial
--          de cargas masivas, e INVENTARIO A CERO (ingresos, consumos, traslados y stock de alimento).
--          Por CASCADE caen solos: lote_etapa_levante, lote_huevo_items, lote_silos,
--          reporte_tecnico_guia, user_farm_scopes (alcance por lote), vacunacion_registro_aplicacion.
--
--  CONSERVA  los 10 LOTE BASE (lote_postura_base: sembrados por migracion desde el Excel del cliente),
--          empresa y flags, granjas/nucleos/galpones, silos (farm_silos, galpon_silos, silo_catalogo),
--          catalogos, guia genetica propia, usuarios/roles/permisos, tickets.
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POR QUE ESTA ARMADO ASI
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
--  * DB Studio (el SQL de la web) acepta UNA sola sentencia por ejecucion: rechaza cualquier texto
--    que tenga un punto y coma antes del final, asi que tampoco pasa un bloque DO. Por eso el borrado
--    es UNA sentencia WITH con DELETEs encadenados: Postgres la ejecuta de forma ATOMICA. Si falla
--    cualquier parte (una FK, un timeout) no se borra NADA.
--  * FAIL-CLOSED POR EMPRESA: la empresa se resuelve por identifier '901000001-1' Y name
--    'Santa Reyes', y solo si hay exactamente UNA que coincida con cualquiera de los dos. Si no,
--    el CTE `sr` queda vacio y todos los DELETE borran cero filas.
--  * No va por migracion EF: se re-ejecutaria en cualquier entorno nuevo y no tiene Down().
--    Prefijo `migracion_*` = operativo de una sola vez (exento del gate verificar-sql-llega-por-migracion).
--  * El historico unificado se BORRA (no se anula) porque se borra el lote entero: no queda nada
--    que lo cuente. El trigger AFTER DELETE de inventario_gestion_movimiento que intenta anularlo no
--    encuentra filas (no-op). Los triggers de lapida (sync_tombstones) si registran lo borrado: es lo
--    que hace que la app movil lo quite de su copia local.
--
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- COMO SE CORRE EN LA WEB (DB Studio > consola SQL)
-- ─────────────────────────────────────────────────────────────────────────────────────────────────
--   0) Hacer un BACKUP antes (DB Studio > Backup). Esto no tiene deshacer.
--   1) Pegar el BLOQUE A (CONTEO) desde la palabra WITH hasta su punto y coma final. Ejecutar.
--      Guardar el resultado: es la foto ANTES. Revisar que la fila 'EMPRESA RESUELTA' diga 1.
--   2) Avisar a los usuarios de Santa Reyes que no registren nada durante la limpieza.
--   3) Pegar el BLOQUE B (BORRADO) desde WITH hasta su punto y coma final. Ejecutar UNA vez.
--      Devuelve cuantas filas borro por tabla. Tienen que coincidir con el conteo del paso 1.
--   4) Volver a pegar el BLOQUE A. Todo lo de la seccion 1-SANTA REYES tiene que dar 0, la seccion
--      2-SE CONSERVA igual que antes, y la seccion 3-OTRAS EMPRESAS identica a la foto del paso 1.
--   5) Pedirle a Santa Reyes: PRIMERO registrar los ingresos de alimento, DESPUES los lotes y los
--      seguimientos (un consumo sin stock se pierde en silencio).
--
--   Pegar cada bloque SIN los comentarios de arriba: tiene que empezar con WITH para que la consola
--   muestre la tabla de resultados.
--
-- Validado el 14-sep-2026 contra la copia local de produccion dentro de BEGIN/ROLLBACK.
-- =====================================================================================================


-- >>> BLOQUE A: CONTEO (solo lectura, correr ANTES y DESPUES del borrado)
WITH sr AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.identifier = '901000001-1'
       AND c.name = 'Santa Reyes'
       AND (SELECT count(*) FROM public.companies x
             WHERE x.identifier = '901000001-1' OR x.name = 'Santa Reyes') = 1
),
lo  AS (SELECT l.lote_id FROM public.lotes l WHERE l.company_id IN (SELECT id FROM sr)),
lpl AS (SELECT l.lote_postura_levante_id AS id FROM public.lote_postura_levante l WHERE l.company_id IN (SELECT id FROM sr)),
lpp AS (SELECT p.lote_postura_produccion_id AS id FROM public.lote_postura_produccion p WHERE p.company_id IN (SELECT id FROM sr))
SELECT 0 AS orden, '0-CONTROL' AS seccion, 'EMPRESA RESUELTA (debe ser 1)' AS tabla, (SELECT count(*) FROM sr) AS filas
UNION ALL SELECT 10, '1-SANTA REYES', 'lotes', (SELECT count(*) FROM lo)
UNION ALL SELECT 11, '1-SANTA REYES', 'lote_postura_levante', (SELECT count(*) FROM lpl)
UNION ALL SELECT 12, '1-SANTA REYES', 'lote_postura_produccion', (SELECT count(*) FROM lpp)
UNION ALL SELECT 13, '1-SANTA REYES', 'lote_aves_cohortes', (SELECT count(*) FROM public.lote_aves_cohortes x WHERE x.company_id IN (SELECT id FROM sr) OR x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 14, '1-SANTA REYES', 'lote_etapa_levante (cascade)', (SELECT count(*) FROM public.lote_etapa_levante x WHERE x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 15, '1-SANTA REYES', 'lote_huevo_items (cascade)', (SELECT count(*) FROM public.lote_huevo_items x WHERE x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 16, '1-SANTA REYES', 'lote_silos (cascade)', (SELECT count(*) FROM public.lote_silos x WHERE x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 17, '1-SANTA REYES', 'reporte_tecnico_guia (cascade)', (SELECT count(*) FROM public.reporte_tecnico_guia x WHERE x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 18, '1-SANTA REYES', 'user_farm_scopes por lote (cascade)', (SELECT count(*) FROM public.user_farm_scopes x WHERE x.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 20, '1-SANTA REYES', 'seguimiento_diario_levante', (SELECT count(*) FROM public.seguimiento_diario_levante s WHERE s.company_id IN (SELECT id FROM sr) OR s.lote_postura_levante_id IN (SELECT id FROM lpl) OR s.lote_postura_produccion_id IN (SELECT id FROM lpp) OR s.lote_id_int IN (SELECT lote_id FROM lo))
UNION ALL SELECT 21, '1-SANTA REYES', 'seguimiento_diario_produccion', (SELECT count(*) FROM public.seguimiento_diario_produccion s WHERE s.company_id IN (SELECT id FROM sr) OR s.lote_postura_produccion_id IN (SELECT id FROM lpp) OR s.lote_id IN (SELECT lote_id FROM lo))
UNION ALL SELECT 22, '1-SANTA REYES', 'seguimiento_reserva_alimento', (SELECT count(*) FROM public.seguimiento_reserva_alimento x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 23, '1-SANTA REYES', 'seguimiento_reserva_aves', (SELECT count(*) FROM public.seguimiento_reserva_aves x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 30, '1-SANTA REYES', 'lote_registro_historico_unificado', (SELECT count(*) FROM public.lote_registro_historico_unificado x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 31, '1-SANTA REYES', 'historico_lote_postura', (SELECT count(*) FROM public.historico_lote_postura x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 32, '1-SANTA REYES', 'espejo_huevo_produccion', (SELECT count(*) FROM public.espejo_huevo_produccion x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 33, '1-SANTA REYES', 'liquidacion_cierre_lote_levante', (SELECT count(*) FROM public.liquidacion_cierre_lote_levante x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 34, '1-SANTA REYES', 'traslado_huevos', (SELECT count(*) FROM public.traslado_huevos x WHERE x.company_id IN (SELECT id FROM sr) OR x.lote_postura_produccion_id IN (SELECT id FROM lpp))
UNION ALL SELECT 35, '1-SANTA REYES', 'movimiento_aves', (SELECT count(*) FROM public.movimiento_aves x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 36, '1-SANTA REYES', 'vacunacion_cronograma_item', (SELECT count(*) FROM public.vacunacion_cronograma_item v WHERE v.lote_postura_levante_id IN (SELECT id FROM lpl) OR v.lote_postura_produccion_id IN (SELECT id FROM lpp))
UNION ALL SELECT 37, '1-SANTA REYES', 'historial_traslado_lote', (SELECT count(*) FROM public.historial_traslado_lote x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 38, '1-SANTA REYES', 'lesiones', (SELECT count(*) FROM public.lesiones x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 39, '1-SANTA REYES', 'lote_seguimientos', (SELECT count(*) FROM public.lote_seguimientos x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 40, '1-SANTA REYES', 'produccion_lotes', (SELECT count(*) FROM public.produccion_lotes x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 41, '1-SANTA REYES', 'migracion_masiva', (SELECT count(*) FROM public.migracion_masiva x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 50, '1-SANTA REYES', 'inventario_gestion_movimiento', (SELECT count(*) FROM public.inventario_gestion_movimiento x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 51, '1-SANTA REYES', 'inventario_gestion_stock', (SELECT count(*) FROM public.inventario_gestion_stock x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 52, '1-SANTA REYES', 'farm_inventory_movements', (SELECT count(*) FROM public.farm_inventory_movements x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 53, '1-SANTA REYES', 'farm_product_inventory', (SELECT count(*) FROM public.farm_product_inventory x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 54, '1-SANTA REYES', 'inventario_aves', (SELECT count(*) FROM public.inventario_aves x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 55, '1-SANTA REYES', 'inventario_gasto', (SELECT count(*) FROM public.inventario_gasto x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 56, '1-SANTA REYES', 'historial_inventario', (SELECT count(*) FROM public.historial_inventario x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 60, '2-SE CONSERVA', 'lote_postura_base (lote base)', (SELECT count(*) FROM public.lote_postura_base x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 61, '2-SE CONSERVA', 'farms', (SELECT count(*) FROM public.farms x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 62, '2-SE CONSERVA', 'nucleos', (SELECT count(*) FROM public.nucleos n WHERE n.granja_id IN (SELECT f.id FROM public.farms f WHERE f.company_id IN (SELECT id FROM sr)))
UNION ALL SELECT 63, '2-SE CONSERVA', 'galpones', (SELECT count(*) FROM public.galpones x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 64, '2-SE CONSERVA', 'farm_silos', (SELECT count(*) FROM public.farm_silos x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 65, '2-SE CONSERVA', 'galpon_silos', (SELECT count(*) FROM public.galpon_silos x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 66, '2-SE CONSERVA', 'item_inventario', (SELECT count(*) FROM public.item_inventario x WHERE x.company_id IN (SELECT id FROM sr))
UNION ALL SELECT 70, '3-OTRAS EMPRESAS', 'lotes', (SELECT count(*) FROM public.lotes x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 71, '3-OTRAS EMPRESAS', 'lote_postura_levante', (SELECT count(*) FROM public.lote_postura_levante x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 72, '3-OTRAS EMPRESAS', 'lote_postura_produccion', (SELECT count(*) FROM public.lote_postura_produccion x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 73, '3-OTRAS EMPRESAS', 'seguimiento_diario_levante', (SELECT count(*) FROM public.seguimiento_diario_levante x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 74, '3-OTRAS EMPRESAS', 'seguimiento_diario_produccion', (SELECT count(*) FROM public.seguimiento_diario_produccion x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 75, '3-OTRAS EMPRESAS', 'inventario_gestion_movimiento', (SELECT count(*) FROM public.inventario_gestion_movimiento x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 76, '3-OTRAS EMPRESAS', 'inventario_gestion_stock', (SELECT count(*) FROM public.inventario_gestion_stock x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 77, '3-OTRAS EMPRESAS', 'lote_registro_historico_unificado', (SELECT count(*) FROM public.lote_registro_historico_unificado x WHERE x.company_id NOT IN (SELECT id FROM sr))
UNION ALL SELECT 78, '3-OTRAS EMPRESAS', 'lote_ave_engorde', (SELECT count(*) FROM public.lote_ave_engorde x WHERE x.company_id NOT IN (SELECT id FROM sr))
ORDER BY orden;
-- <<< FIN BLOQUE A


-- >>> BLOQUE B: BORRADO (una sola sentencia atomica, correr UNA vez)
WITH sr AS (
    SELECT c.id
      FROM public.companies c
     WHERE c.identifier = '901000001-1'
       AND c.name = 'Santa Reyes'
       AND (SELECT count(*) FROM public.companies x
             WHERE x.identifier = '901000001-1' OR x.name = 'Santa Reyes') = 1
),
lo  AS (SELECT l.lote_id FROM public.lotes l WHERE l.company_id IN (SELECT id FROM sr)),
lpl AS (SELECT l.lote_postura_levante_id AS id FROM public.lote_postura_levante l WHERE l.company_id IN (SELECT id FROM sr)),
lpp AS (SELECT p.lote_postura_produccion_id AS id FROM public.lote_postura_produccion p WHERE p.company_id IN (SELECT id FROM sr)),
d_vac AS (DELETE FROM public.vacunacion_cronograma_item v WHERE v.lote_postura_levante_id IN (SELECT id FROM lpl) OR v.lote_postura_produccion_id IN (SELECT id FROM lpp) RETURNING 1),
d_th  AS (DELETE FROM public.traslado_huevos x WHERE x.company_id IN (SELECT id FROM sr) OR x.lote_postura_produccion_id IN (SELECT id FROM lpp) RETURNING 1),
d_sdl AS (DELETE FROM public.seguimiento_diario_levante s WHERE s.company_id IN (SELECT id FROM sr) OR s.lote_postura_levante_id IN (SELECT id FROM lpl) OR s.lote_postura_produccion_id IN (SELECT id FROM lpp) OR s.lote_id_int IN (SELECT lote_id FROM lo) RETURNING 1),
d_sdp AS (DELETE FROM public.seguimiento_diario_produccion s WHERE s.company_id IN (SELECT id FROM sr) OR s.lote_postura_produccion_id IN (SELECT id FROM lpp) OR s.lote_id IN (SELECT lote_id FROM lo) RETURNING 1),
d_rsa AS (DELETE FROM public.seguimiento_reserva_alimento x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_rsv AS (DELETE FROM public.seguimiento_reserva_aves x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_hu  AS (DELETE FROM public.lote_registro_historico_unificado x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_hlp AS (DELETE FROM public.historico_lote_postura x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_esp AS (DELETE FROM public.espejo_huevo_produccion x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_liq AS (DELETE FROM public.liquidacion_cierre_lote_levante x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_ma  AS (DELETE FROM public.movimiento_aves x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_htl AS (DELETE FROM public.historial_traslado_lote x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_les AS (DELETE FROM public.lesiones x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_ls  AS (DELETE FROM public.lote_seguimientos x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_pl  AS (DELETE FROM public.produccion_lotes x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_mm  AS (DELETE FROM public.migracion_masiva x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_igm AS (DELETE FROM public.inventario_gestion_movimiento x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_igs AS (DELETE FROM public.inventario_gestion_stock x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_fim AS (DELETE FROM public.farm_inventory_movements x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_fpi AS (DELETE FROM public.farm_product_inventory x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_ia  AS (DELETE FROM public.inventario_aves x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_ig  AS (DELETE FROM public.inventario_gasto x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_hi  AS (DELETE FROM public.historial_inventario x WHERE x.company_id IN (SELECT id FROM sr) RETURNING 1),
d_lpp AS (DELETE FROM public.lote_postura_produccion p WHERE p.lote_postura_produccion_id IN (SELECT id FROM lpp) RETURNING 1),
d_lpl AS (DELETE FROM public.lote_postura_levante l WHERE l.lote_postura_levante_id IN (SELECT id FROM lpl) RETURNING 1),
d_coh AS (DELETE FROM public.lote_aves_cohortes x WHERE x.company_id IN (SELECT id FROM sr) OR x.lote_id IN (SELECT lote_id FROM lo) RETURNING 1),
d_lot AS (DELETE FROM public.lotes l WHERE l.lote_id IN (SELECT lote_id FROM lo) RETURNING 1)
SELECT 0 AS orden, 'EMPRESA RESUELTA (debe ser 1)' AS tabla, (SELECT count(*) FROM sr) AS borradas
UNION ALL SELECT 10, 'lotes', (SELECT count(*) FROM d_lot)
UNION ALL SELECT 11, 'lote_postura_levante', (SELECT count(*) FROM d_lpl)
UNION ALL SELECT 12, 'lote_postura_produccion', (SELECT count(*) FROM d_lpp)
UNION ALL SELECT 13, 'lote_aves_cohortes', (SELECT count(*) FROM d_coh)
UNION ALL SELECT 20, 'seguimiento_diario_levante', (SELECT count(*) FROM d_sdl)
UNION ALL SELECT 21, 'seguimiento_diario_produccion', (SELECT count(*) FROM d_sdp)
UNION ALL SELECT 22, 'seguimiento_reserva_alimento', (SELECT count(*) FROM d_rsa)
UNION ALL SELECT 23, 'seguimiento_reserva_aves', (SELECT count(*) FROM d_rsv)
UNION ALL SELECT 30, 'lote_registro_historico_unificado', (SELECT count(*) FROM d_hu)
UNION ALL SELECT 31, 'historico_lote_postura', (SELECT count(*) FROM d_hlp)
UNION ALL SELECT 32, 'espejo_huevo_produccion', (SELECT count(*) FROM d_esp)
UNION ALL SELECT 33, 'liquidacion_cierre_lote_levante', (SELECT count(*) FROM d_liq)
UNION ALL SELECT 34, 'traslado_huevos', (SELECT count(*) FROM d_th)
UNION ALL SELECT 35, 'movimiento_aves', (SELECT count(*) FROM d_ma)
UNION ALL SELECT 36, 'vacunacion_cronograma_item', (SELECT count(*) FROM d_vac)
UNION ALL SELECT 37, 'historial_traslado_lote', (SELECT count(*) FROM d_htl)
UNION ALL SELECT 38, 'lesiones', (SELECT count(*) FROM d_les)
UNION ALL SELECT 39, 'lote_seguimientos', (SELECT count(*) FROM d_ls)
UNION ALL SELECT 40, 'produccion_lotes', (SELECT count(*) FROM d_pl)
UNION ALL SELECT 41, 'migracion_masiva', (SELECT count(*) FROM d_mm)
UNION ALL SELECT 50, 'inventario_gestion_movimiento', (SELECT count(*) FROM d_igm)
UNION ALL SELECT 51, 'inventario_gestion_stock', (SELECT count(*) FROM d_igs)
UNION ALL SELECT 52, 'farm_inventory_movements', (SELECT count(*) FROM d_fim)
UNION ALL SELECT 53, 'farm_product_inventory', (SELECT count(*) FROM d_fpi)
UNION ALL SELECT 54, 'inventario_aves', (SELECT count(*) FROM d_ia)
UNION ALL SELECT 55, 'inventario_gasto', (SELECT count(*) FROM d_ig)
UNION ALL SELECT 56, 'historial_inventario', (SELECT count(*) FROM d_hi)
ORDER BY orden;
-- <<< FIN BLOQUE B
