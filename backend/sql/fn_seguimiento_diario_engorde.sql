-- =============================================================================
-- fn_seguimiento_diario_engorde(p_lote_id INT)
-- Devuelve la tabla diaria de seguimiento de un lote de pollo engorde.
-- Tabla fuente: seguimiento_diario_aves_engorde
--
-- v16a (2026-08-18) — La marca `para_proximo_ciclo` vuelve a ser INERTE en la fn.
--   Plan: fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md (FASE A).
--   Qué se quita: los 5 lugares donde v15 interpretaba el booleano — el disyunto marcado de
--   `apert_mov` y los 4 guards de `hist_full`, `hist_alimento`, `docs_por_fecha` y
--   `fechas_universo`. Queda el filtro de v14 exacto. **Se CONSERVAN** `apertura_alimento_kg` y
--   `apertura_documentos` (parte A de v15): son exposición pura de un escalar que la fn ya
--   calculaba desde v9, no tienen relación con la marca y ya están en producción.
--
--   POR QUÉ. v15 quita kg de una pantalla y ESPERA que otra los muestre. Medido sobre el dump
--   local: marcar un movimiento rompe la conservación en 729 de 2.210 casos reales — hasta
--   37.467 kg que no aparecen en ninguna tabla diaria — y deja 208 filas en negativo. La causa
--   es que los 4 guards filtran por UBICACIÓN y le quitan el movimiento a TODO lote con
--   seguimiento, incluidos los que CONVIVEN con el destino (R1), y en esos galpones ninguna
--   apertura lo vuelve a tomar. El rediseño por ENTREGA (v16, 08-ago) tampoco cerró: recalculaba
--   la atribución EN LECTURA sobre estado mutable, y la liquidación congela un solo extremo ⇒
--   el handoff se partía (NO-GO del gate, revertido sin llegar a un commit).
--
--   QUÉ SIGUE. La atribución pasa a ser un HECHO PERSISTIDO que la fn LEE (Fase B del plan). Esta
--   v16a es el piso limpio sobre el que entra: con 0 marcas en el histórico la salida es byte a
--   byte la de v14+apertura, y el gate multipaís lo demuestra en las dos empresas. Mientras tanto
--   la marca no se puede volver a poner: `InventarioGestionService` la rechaza con 400 (quitar una
--   marca existente sigue permitido — R3: los kilos nunca quedan sin poder corregirse).
--
--   ⚠️ La firma NO cambia respecto de v15 (siguen las 49 columnas OUT) ⇒ `CREATE OR REPLACE`
--   alcanzaría. El script conserva igual el `DROP FUNCTION IF EXISTS` de v15 por idempotencia y
--   para que reinstalarlo a mano no dependa de qué versión hubiera antes: no hay dependencias
--   (`pg_depend` vacío) y los 5 consumidores la llaman por CROSS JOIN LATERAL con columnas
--   NOMBRADAS. La migración replica este archivo byte a byte.
--
-- v15 (2026-08-08) — El «Ingreso inicial del ciclo» deja de ser invisible + marca de atribución.
--   Plan: fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md (D1 y D2).
--   Pedido de operación: el alimento llega a la granja 2-7 días ANTES del encasetamiento, pero hay
--   que decirle a cada persona que lo registre con la fecha del PRIMER DÍA DE CONSUMO para que la
--   tabla diaria «cuadre». Así se pierde la fecha real que necesita contabilidad. Medido en el dump:
--   Ecuador aplica ese workaround en 110 de 110 ciclos; Panamá ya fecha real en 9 de 30.
--
--   (A) APERTURA VISIBLE — dos columnas ADITIVAS al final del RETURNS TABLE:
--       `apertura_alimento_kg` y `apertura_documentos`, pobladas SOLO en la primera fila del ciclo
--       (la de `fecha_min`). Son el escalar `apertura_alimento` que la fn ya calcula desde v9 y el
--       STRING_AGG de los documentos de `apert_mov` que lo componen. **NO cambian el saldo, ni el
--       universo de filas, ni ninguna columna previa: es exposición pura de un número que ya existía.**
--       Por qué importa: el alimento previo al encaset YA entraba al saldo del día 1, pero
--       `ingreso_alimento_kg` mostraba 0 y `documento` vacío ⇒ el saldo «aparecía de la nada» y la
--       operación desconfiaba. Peor: mientras el lote no tiene seguimiento esos ingresos SÍ se ven
--       como fila propia, y al cargar el primer seguimiento desaparecen dentro de la apertura. Ese
--       parpadeo es justamente lo que empuja a re-fechar el ingreso al día 1.
--
--   (B) OVERRIDE POR MARCA `para_proximo_ciclo` — ⛔ RETIRADO EN v16a. Lo que hacía: el
--       movimiento marcado entraba a `apert_mov` saltando los cortes de v11/v12, y salía de
--       `hist_full`, `hist_alimento`, `docs_por_fecha` y `fechas_universo` del ciclo que lo
--       contenía. Por qué se retiró, abajo.
--
--   ⚠️ La firma CAMBIA (dos columnas OUT nuevas) ⇒ `CREATE OR REPLACE` NO alcanza: PostgreSQL
--   responde «cannot change return type of existing function». Por eso el script empieza con
--   `DROP FUNCTION IF EXISTS`. Verificado que ningún objeto depende de la fn (`pg_depend` vacío):
--   los 5 consumidores la llaman por CROSS JOIN LATERAL con columnas NOMBRADAS
--   (fn_reporte_diario_costos_engorde, fn_informe_semanal_pollo_engorde, fn_cuadre_alimento_engorde,
--   verificar_paridad_saldo_engorde y el SqlQueryRaw del service Ecuador), y
--   `vw_seguimiento_pollo_engorde` es una reimplementación set-based que no la invoca.
--
--   La rama CONGELADA (`liquidacion_lote_engorde_congelada_fila`) NO se toca: los lotes ya liquidados
--   conservan su foto y devuelven NULL en las dos columnas nuevas (la copia no las guardó). Es una
--   divergencia consciente: un lote congelado no vuelve a mostrar su apertura.
--
-- v14 (2026-08-07) — Fix: un lote que terminó SIN cerrarse absorbía el ciclo SIGUIENTE del galpón.
--   * Ticket de operación (Ecuador): «granja KM 86 lote 01 galpón 1 y 02: tenemos ingreso del mes de
--     julio cuando el lote cerró en abril». El lote 2601 de Kilometro 86 / Galpon-1 (id 2) tiene su
--     último seguimiento el 2026-04-20 y la grilla llegaba hasta el 2026-08-03, con el saldo inflado
--     de 1.600 kg a 206.450 kg. Los ingresos de julio son CORRECTOS: son del lote 2603, encasetado
--     en ese mismo galpón el 24/06. El error es a qué lote se los mostraba.
--   * Causa: `fecha_max` solo se cierra por saldo 0 (`saldo_close`, v5) o por
--     `estado_operativo_lote = 'cerrado'`. Este lote nunca se liquidó, y su saldo NUNCA llega a 0
--     justamente porque el galpón siguió recibiendo alimento para los ciclos siguientes ⇒
--     `fecha_max = NULL` ⇒ grilla sin tope, y `fechas_universo`/`hist_alimento`/`docs_por_fecha`
--     filtran por UBICACIÓN (granja+núcleo+galpón), no por lote. La v5 ya había descrito este mismo
--     problema, pero su criterio es NUMÉRICO (que el saldo toque 0) y por eso tiene este agujero.
--   * Asimetría de fondo: la fn ya sabe distinguir ciclos que no conviven —`lotes_ajenos` (v11) y
--     `corte_apertura` (v12) lo usan— pero ese conocimiento solo se aplicaba a la APERTURA.
--   * Solución: nuevo CTE `corte_ciclo_siguiente`, complemento exacto de `corte_apertura`. El galpón
--     deja de ser mío el día en que OTRO lote del mismo granja+núcleo+galpón empieza a tener
--     seguimiento después de que yo dejé de tenerlo: `fecha_max` se acota al día anterior. Criterio
--     ESTRUCTURAL (hay otro ciclo siguiéndose ahí) y no numérico, así que no depende de que alguien
--     se acuerde de liquidar el lote.
--   * `LEAST` ignora los NULL, así que un lote sin ciclo posterior conserva su `fecha_max` de v13 y
--     un lote genuinamente activo sigue con NULL (sin tope).
--   * NO toca la rama VENTA_AVES: sigue sin tope (v7), así que una venta tardía conserva su fila.
--   * NO toca lotes que CONVIVEN en el galpón (v10): `MIN(primer_seg_ajeno) > last_seg` es falso
--     cuando los ciclos se solapan ⇒ el saldo compartido queda idéntico.
--   * Verificado sobre el dump de producción: de 140 lotes solo cambian 2, los dos de Ecuador
--     (lote 2 con 31 filas invasoras y lote 86 con 1). **ItalcolPanama NO-OP exacto.**
--
-- v13 (2026-07-31) — Liquidación CONGELADA (migración 20260731185300_AddLiquidacionLoteEngordeCongelada).
--   * Problema: la fn RECALCULA en cada request — no es una foto. El fix v9→v12 del 28-jul movió
--     solas corridas CERRADAS hacía meses, después de que Costos las había dado por cuadradas.
--   * Solución: al liquidar, fn_congelar_liquidacion_engorde guarda la tabla completa en
--     liquidacion_lote_engorde_congelada(_fila). Esta fn pasa a ser un UNION ALL con quals
--     excluyentes: con copia VIGENTE (anulada_at IS NULL) devuelve la copia; sin copia, el cuerpo
--     v12 VERBATIM como subconsulta. El planner gatea la rama viva con One-Time Filter, así que
--     con copia el cálculo vivo NO se ejecuta. Reabrir el lote anula la copia (servicio o trigger
--     trg_lote_ave_engorde_anula_congelada) y vuelve el cálculo en vivo.
--   * SIGUE siendo LANGUAGE sql A PROPÓSITO: la fn se inlinea en los CROSS JOIN LATERAL del
--     Reporte de Costos / Informe Semanal / Cuadre; una variante plpgsql perdía el inlining
--     (Function Scan) y multiplicó ×2.8 el Reporte de Costos (medido con EXPLAIN ANALYZE).
--   * El ORDER BY exterior (fecha, COALESCE(seg_id,0)) es el MISMO de v12; `orden` de la copia es
--     row_number() sobre ese mismo ORDER BY ⇒ el orden no cambia en ninguna rama.
--   * Los 5 consumidores quedan congelados con este único cambio: tabla diaria, Reporte Diario de
--     Costos, Informe Semanal Panamá, Cuadre de alimento y el saldo persistido
--     (SaldoAlimentoEngordeAplicador lee de acá ⇒ la columna se vuelve auto-reparadora).
--
-- v12 (2026-07-30) — Fix: la apertura tampoco puede retroceder más allá del FIN del ciclo anterior.
--   * La v11 excluye los movimientos atribuidos a un lote AJENO, pero eso solo tapa la mitad del
--     agujero: `lote_ave_engorde_id` lo pone el trigger con `fn_lote_ave_engorde_id_desde_ubicacion`,
--     que devuelve el lote de id MÁS ALTO del galpón al momento de INSERTAR. Así que la atribución
--     falla en los DOS sentidos, y hacen falta los dos criterios:
--       - limpieza etiquetada con el lote VIEJO  → la caza `lotes_ajenos` (v11). Caso Km22 / G0036.
--       - limpieza etiquetada con el lote NUEVO  → la caza este corte.       Caso SAN GUILLERMO / G0033:
--         dos INV_TRASLADO_SALIDA del 13/03 (960 + 4.200 = 5.160 kg) son el vaciado del ciclo 2601,
--         cuyo último seguimiento fue ESE MISMO 13/03, pero quedaron con el id del lote 2602 porque
--         se insertaron cuando ese lote ya existía. La v11 no los ve: para ella son «propios».
--   * Solución: la ventana de apertura arranca en
--         GREATEST(fecha_encaset − dias_alimento_previo_encaset, fin_del_ciclo_anterior + 1)
--     donde `fin_del_ciclo_anterior` es el último día de seguimiento del lote del galpón que cerró
--     antes de que este empezara. Nada anterior a ese día puede ser alimento mío.
--   * Verificado sobre el dump de producción: Ecuador pasa de 6 aperturas negativas (−13.800 kg) a 1
--     (−580 kg); Panamá NO-OP exacto (0 lotes tocados). No toca el caso legítimo de v9 —el
--     preiniciador que llega antes del encaset— porque ese llega mucho después de que cerró el ciclo
--     previo, no el mismo día.
--
-- v11 (2026-07-29) — Fix: la apertura dejaba de ser propia y heredaba el CICLO ANTERIOR del galpón.
--   * Problema: la ventana de alimento previo al encaset (v9) retrocede `dias_alimento_previo_encaset`
--     días. En Ecuador cada galpón encadena 3-4 ciclos sucesivos, así que esa ventana cae DENTRO del
--     ciclo anterior, justo cuando se vacía su bodega. Y como el filtro de devoluciones descarta las
--     entradas (`devolución por eliminación`) pero conserva las salidas, la apertura quedaba NEGATIVA.
--     Kilometro 22 / G0036 / lote 98 (2603): apertura −7.960 kg (+160 −7.520 −440 −160, los cuatro del
--     lote 65 «2602», que cerró el 01/06 y se vació entre el 05 y el 07/06). La grilla mostraba 3.420
--     contra 11.380 kg de stock real, con el MISMO desvío en las 43 filas.
--   * Es estructural y recurrente: la ventana solo alcanza la limpieza del ciclo anterior si ese ciclo
--     existe ⇒ aparece desde el TERCER ciclo de cada galpón. Ecuador va por la corrida 4 (26 lotes con
--     apertura negativa, −98.692 kg); Panamá hoy tiene 0 casos pero le pasaría al encadenar ciclos.
--   * Solución: nuevo CTE `lotes_ajenos` — el COMPLEMENTO exacto del predicado que v10 ya usa para el
--     consumo. Un lote del mismo galpón cuyo rango de seguimiento NO se solapa con el mío no comparte
--     bodega conmigo, así que su alimento no entra en MI APERTURA. Los movimientos SIN atribución
--     (`lote_ave_engorde_id IS NULL`) se conservan: no se pierde alimento.
--   * ⚠️ El filtro va SOLO en `apert_mov`, y es a propósito. `lote_ave_engorde_id` NO distingue ciclos:
--     el sistema etiqueta el movimiento con el lote VIGENTE del galpón, así que el preiniciador del
--     ciclo nuevo queda con el id del viejo mientras el nuevo no tenga seguimiento (CAROLINA G0059:
--     2.800 kg de SM0175 del 16/06 atribuidos al lote 63 siendo del 96). Por eso la propiedad solo
--     puede usarse ANTES del primer seguimiento; dentro de [fecha_min, fecha_max] el galpón es del
--     lote y todo lo que entra es suyo. Filtrar también `hist_alimento` rompía esos 4 galpones.
--   * Verificado sobre el dump de producción: Ecuador 26 lotes con apertura negativa → 6 (déficit real),
--     Panamá NO-OP exacto (0 filas distintas). La rama VENTA_AVES ya estaba acotada al lote: no se toca.
--
-- v10 (2026-07-29) — Fix: el consumo pasa a scope GALPÓN (inventario compartido entre lotes).
--   * Problema: los ingresos/traslados SIEMPRE se leyeron con scope galpón, pero el consumo se
--     restaba solo del lote consultado. Con dos lotes solapados en el mismo galpón cada uno veía
--     el 100 % de los ingresos y nada más que su propio consumo ⇒ LOS DOS inflaban el saldo.
--     G0490 (DOÑA MARIA): ingresos 97.729,6 kg; lote 168 mostraba 82.806,4 y lote 169 34.316,9,
--     cuando el saldo real compartido era 19.393,5 (inventario 18.939,9).
--   * Solución: nuevo CTE `consumo_galpon_por_fecha` (todos los lotes de granja+núcleo+galpón,
--     acotado por fecha_corte_alimento). Lo usan la apertura, la detección de cierre y `pt_calc`,
--     de modo que saldo(f) = ingresos(≤f) − consumo_del_galpón(≤f): el MISMO valor en los dos lotes.
--   * NO-OP donde hay un solo lote por galpón (la inmensa mayoría). Verificado en la base: solo
--     Panamá tiene lotes solapados (4 galpones); Ecuador y Colombia no tienen ninguno.
--
-- v9 (2026-07-28) — Fix: saldo_alimento_kg SIN piso ni reseteo de base + ventana de alimento previo.
--   * El corte del alimento pasa de fecha_encaset a fecha_encaset − companies.dias_alimento_previo_
--     encaset (default 10, tope 30). En engorde el preiniciador llega antes que los pollitos: cortar
--     en el encaset descartaba alimento propio del lote (galpón 6 DAYLAND: 12.129,638 kg del 04/06,
--     exactamente la diferencia entre el saldo del reporte y el inventario real).
--   * Problema: el reseteo de Lindley evitaba mostrar negativos, pero cada recorte descartaba
--     déficit real y el acumulado quedaba por encima del inventario. Galpón 6 DAYLAND: reporte
--     12.869,46 kg vs 2.235,33 kg reales (+10.634,13 = el peor negativo del ciclo).
--   * Solución: el saldo es P_t crudo. Un negativo señala alimento consumido antes de registrar su
--     llegada — información de la operación, no un defecto del cálculo. Alineado con el C#.
--
-- v8 (2026-07-14) — Fix: aves_iniciales no restaba mort_caja_h/mort_caja_m.
--   * Problema: "Mort. caja H/M" (aves que llegaron muertas en la caja de transporte, campo
--     capturado una sola vez al crear/editar el lote, fuera del seguimiento diario) NO se
--     restaba de `aves_iniciales`, así saldo_aves quedaba inflado exactamente en ese monto vs.
--     el widget "Aves disponibles" (GetAvesDisponiblesAsync) y la validación de creación de
--     registros (CalcularHembrasVivasAsync), que SÍ la restan del maestro. Caso lote 77 "2603"
--     (Sacachun 3b, galpón G0049): mort_caja_h=17 → tabla diaria mostraba 17 aves vivas mientras
--     "Aves disponibles" mostraba 0/0 y bloqueaba nuevos registros ("no se puede hacer
--     seguimiento"), generando un descuadre visible entre ambas pantallas.
--   * Solución: `lote_info` expone `mort_caja_total` (mort_caja_h + mort_caja_m); `aves_iniciales`
--     lo resta (con piso 0) en toda rama que parte de aves_encasetadas/suma_hm. La rama 'cerrado'
--     no cambia: ya fuerza el cierre en 0 por construcción propia (bajas + ventas), sin depender
--     de aves_encasetadas. No-op para lotes sin mort_caja registrada (inmensa mayoría).
--
-- v7 (2026-06-10) — Fix: VENTA_AVES fuera del rango [fecha_min, fecha_max].
--   * Problema: en lotes CERRADOS `aves_iniciales = bajas + ventas_totales` (cierre en 0
--     por construcción) y `ventas_totales` NO tiene tope de fecha, pero `fechas_universo`
--     SÍ acotaba por fecha_max (cierre por alimento=0, v5). Una venta posterior al corte
--     (ej. lote 23: venta de 8 machos el 2026-05-13, reapertura "PARA TRASLADO", con corte
--     2026-03-18) inflaba `inicial` sin fila que la restara → saldo_aves residual fantasma
--     (el lote "cerrado" mostraba saldo 8 en vez de 0).
--   * Solución: en `fechas_universo` y `docs_por_fecha` las VENTA_AVES del lote NO se
--     acotan por fecha_min/fecha_max (solo por fecha_encaset). Las ventas pertenecen al
--     lote (no al galpón), no hay riesgo de contaminación de otro ciclo. Los eventos de
--     alimento (scope galpón) conservan el tope v5. La venta tardía aparece como fila
--     propia y el saldo del lote cerrado cierra en 0.
--
-- v6 (2026-05-31) — Fix: saldo_alimento_kg con PISO 0 + RESETEO DE BASE (modelo M1).
--   * Problema: la fn calculaba el saldo como GREATEST(0, acumulado) sobre el cumulativo
--     desde apertura (modelo M0). Eso ARRASTRABA el déficit transitorio: cuando el consumo
--     se adelantaba a un ingreso registrado tarde, el acumulado se volvía negativo, se
--     mostraba 0, y al llegar el ingreso "rebotaba" → el usuario lo veía como "no cuadra"
--     (caso lote 75, 29→30 may-2026). Además, M0 NO coincidía con la lógica canónica del
--     frontend (computeSaldoAlimentoKgPorSeguimiento) ni del backend C#
--     (RecalcularSaldoAlimentoPorLoteAsync), que SÍ usan piso 0 con reseteo.
--   * Solución: nuevo CTE `pt_calc` expone P_t (saldo SIN piso). La columna final aplica la
--     forma cerrada de la recursión de Lindley:  saldo_t = P_t − LEAST(0, MIN(P_t) acumulado).
--     Equivale a saldo_t = max(0, saldo_{t-1} + ingreso − consumo). Alinea la fn con front/back.
--   * Nota: para lotes sanos (que nunca van a negativo) M1 == M0. Solo cambia los que dipean.
--
-- v5 (2026-05-30) — Fix: corte por CIERRE EFECTIVO de alimento.
--   * Problema: lotes que terminaron de hecho (alimento llegó a 0 al cerrar) pero NO
--     se marcaron 'Cerrado' tenían fecha_max=NULL, así la tabla mostraba ingresos de
--     alimento del SIGUIENTE ciclo del galpón (fechas que no aplican al lote).
--   * Solución: nuevo CTE `saldo_close` detecta la primera fecha >= último seguimiento
--     donde el saldo de alimento llega a 0 (lote vaciado). `fecha_max` se acota ahí,
--     sin depender del flag estado_operativo_lote. Se incluye ESE registro de cierre
--     (la salida que deja el alimento en 0) y se excluye todo lo posterior.
--   * Fallback: si el saldo nunca llega a 0 tras el último seg → lote 'cerrado' usa
--     MAX(seg) (comportamiento previo); lote 'abierto' usa NULL (sigue mostrando movs).
--
-- v4 (plan #14): fechas_universo = seguimientos ∪ movimientos; fecha_max NULL en abiertos.
-- v3 (plan #12): apertura filtrada por fecha_encaset.
-- v2 (2026-05-28): rango_seg ::DATE; INV_TRASLADO_SALIDA con ABS(); saldo dinámico.
-- =============================================================================
-- ⭐ v15: la firma cambia (dos columnas OUT nuevas) ⇒ CREATE OR REPLACE no basta.
DROP FUNCTION IF EXISTS fn_seguimiento_diario_engorde(INT);

CREATE FUNCTION fn_seguimiento_diario_engorde(p_lote_id INT)
RETURNS TABLE (
    -- Identificación
    seg_id                      BIGINT,
    fecha                       DATE,
    -- Tiempo
    edad_dia                    INT,
    semana                      SMALLINT,
    -- Seguimiento crudo
    mortalidad_hembras          INT,
    mortalidad_machos           INT,
    sel_h                       INT,
    sel_m                       INT,
    error_sexaje_hembras        INT,
    error_sexaje_machos         INT,
    -- Calculados simples
    total_mort_sel_dia          INT,
    perdidas_totales_dia        INT,
    consumo_kg_hembras          DOUBLE PRECISION,
    consumo_kg_machos           DOUBLE PRECISION,
    consumo_dia_kg              DOUBLE PRECISION,
    -- Acumulados corrientes (window functions)
    acum_consumo_kg             DOUBLE PRECISION,
    saldo_aves                  INT,
    pct_perdidas_dia            DOUBLE PRECISION,
    -- Saldo alimento persistido por RecalcularSaldoAlimentoPorLoteAsync
    saldo_alimento_kg           DOUBLE PRECISION,
    -- Histórico agregado por fecha
    ingreso_alimento_kg         DOUBLE PRECISION,
    traslado_entrada_kg         DOUBLE PRECISION,
    traslado_salida_kg          DOUBLE PRECISION,
    consumo_bodega_kg           DOUBLE PRECISION,
    -- Documento: numeroDocumento || referencia de INV_INGRESO + VENTA_AVES
    documento                   TEXT,
    despacho_hembras            INT,
    despacho_machos             INT,
    despacho_mixtas             INT,
    -- Peso INDIVIDUAL real de la venta de ESTE lote en la fecha (R3.5), no el global de factura
    despacho_peso_neto          DOUBLE PRECISION,
    despacho_peso_tara          DOUBLE PRECISION,
    despacho_promedio_peso_ave  DOUBLE PRECISION,
    -- Mediciones del seguimiento
    tipo_alimento               TEXT,
    peso_prom_hembras           DOUBLE PRECISION,
    peso_prom_machos            DOUBLE PRECISION,
    uniformidad_hembras         DOUBLE PRECISION,
    uniformidad_machos          DOUBLE PRECISION,
    cv_hembras                  DOUBLE PRECISION,
    cv_machos                   DOUBLE PRECISION,
    consumo_agua_diario         DOUBLE PRECISION,
    consumo_agua_ph             DOUBLE PRECISION,
    consumo_agua_orp            DOUBLE PRECISION,
    consumo_agua_temperatura    DOUBLE PRECISION,
    observaciones               TEXT,
    ciclo                       TEXT,
    metadata                    JSONB,
    items_adicionales           JSONB,
    historico_consumo_alimento  JSONB,
    created_by_user_id          TEXT,
    -- ⭐ v15: «Ingreso inicial del ciclo» — el alimento previo al encaset que la apertura ya absorbía
    -- desde v9 y que hasta ahora era un escalar interno. Solo la PRIMERA fila del ciclo trae valor
    -- (el resto 0 / NULL); la rama congelada devuelve NULL en las dos. DOUBLE PRECISION igual que
    -- todas las demás columnas de kg de esta tabla (el origen `apertura_kg` ya es FLOAT8).
    apertura_alimento_kg        DOUBLE PRECISION,
    apertura_documentos         TEXT
) LANGUAGE sql STABLE AS $$
SELECT u.seg_id, u.fecha, u.edad_dia, u.semana,
       u.mortalidad_hembras, u.mortalidad_machos, u.sel_h, u.sel_m,
       u.error_sexaje_hembras, u.error_sexaje_machos,
       u.total_mort_sel_dia, u.perdidas_totales_dia,
       u.consumo_kg_hembras, u.consumo_kg_machos, u.consumo_dia_kg,
       u.acum_consumo_kg, u.saldo_aves, u.pct_perdidas_dia, u.saldo_alimento_kg,
       u.ingreso_alimento_kg, u.traslado_entrada_kg, u.traslado_salida_kg, u.consumo_bodega_kg,
       u.documento, u.despacho_hembras, u.despacho_machos, u.despacho_mixtas,
       u.despacho_peso_neto, u.despacho_peso_tara, u.despacho_promedio_peso_ave,
       u.tipo_alimento, u.peso_prom_hembras, u.peso_prom_machos,
       u.uniformidad_hembras, u.uniformidad_machos, u.cv_hembras, u.cv_machos,
       u.consumo_agua_diario, u.consumo_agua_ph, u.consumo_agua_orp, u.consumo_agua_temperatura,
       u.observaciones, u.ciclo, u.metadata, u.items_adicionales, u.historico_consumo_alimento,
       u.created_by_user_id,
       u.apertura_alimento_kg, u.apertura_documentos
FROM (
    SELECT f.seg_id, f.fecha, f.edad_dia, f.semana,
           f.mortalidad_hembras, f.mortalidad_machos, f.sel_h, f.sel_m,
           f.error_sexaje_hembras, f.error_sexaje_machos,
           f.total_mort_sel_dia, f.perdidas_totales_dia,
           f.consumo_kg_hembras, f.consumo_kg_machos, f.consumo_dia_kg,
           f.acum_consumo_kg, f.saldo_aves, f.pct_perdidas_dia, f.saldo_alimento_kg,
           f.ingreso_alimento_kg, f.traslado_entrada_kg, f.traslado_salida_kg, f.consumo_bodega_kg,
           f.documento, f.despacho_hembras, f.despacho_machos, f.despacho_mixtas,
           f.despacho_peso_neto, f.despacho_peso_tara, f.despacho_promedio_peso_ave,
           f.tipo_alimento, f.peso_prom_hembras, f.peso_prom_machos,
           f.uniformidad_hembras, f.uniformidad_machos, f.cv_hembras, f.cv_machos,
           f.consumo_agua_diario, f.consumo_agua_ph, f.consumo_agua_orp, f.consumo_agua_temperatura,
           f.observaciones, f.ciclo, f.metadata, f.items_adicionales, f.historico_consumo_alimento,
           f.created_by_user_id,
           -- ⭐ v15: la copia congelada NO guardó la apertura (se tomó con la fn v13). Un lote
           -- liquidado conserva su foto tal cual: NULL, no un recálculo en vivo.
           NULL::FLOAT8 AS apertura_alimento_kg,
           NULL::TEXT   AS apertura_documentos
      FROM liquidacion_lote_engorde_congelada_fila f
     WHERE f.liquidacion_id = (SELECT c.id
                                 FROM liquidacion_lote_engorde_congelada c
                                WHERE c.lote_ave_engorde_id = p_lote_id
                                  AND c.anulada_at IS NULL)
    UNION ALL
    SELECT t.*
      FROM (

WITH

-- 1. Datos clave del lote
lote_info AS (
    SELECT
        l.granja_id,
        COALESCE(TRIM(l.nucleo_id), '')  AS nucleo_id,
        COALESCE(TRIM(l.galpon_id), '')  AS galpon_id,
        l.fecha_encaset,
        COALESCE(l.aves_encasetadas, 0)  AS aves_encasetadas,
        COALESCE(l.hembras_l, 0) + COALESCE(l.machos_l, 0) + COALESCE(l.mixtas, 0) AS suma_hm,
        LOWER(COALESCE(l.estado_operativo_lote, '')) AS estado_operativo_lote,
        -- ⭐ v8: mortalidad en caja (llegada), capturada fuera del seguimiento diario.
        COALESCE(l.mort_caja_h, 0) + COALESCE(l.mort_caja_m, 0) AS mort_caja_total,
        -- ⭐ v9: el alimento cuenta desde esta fecha, no desde el encaset. En engorde el preiniciador
        -- llega antes que los pollitos; cortar en el encaset dejaba fuera alimento propio del lote.
        -- La ventana la configura la empresa (default 10 días, tope 30 = VentanaAlimentoPrevioCalculos).
        (l.fecha_encaset::DATE - LEAST(30, GREATEST(0, COALESCE(c.dias_alimento_previo_encaset, 10)))) AS fecha_corte_alimento
    FROM lote_ave_engorde l
    LEFT JOIN companies c ON c.id = l.company_id
    WHERE l.lote_ave_engorde_id = p_lote_id
      AND l.deleted_at IS NULL
),

-- 2. Rango base del ciclo: primer y último seguimiento + estado
rango_seg AS (
    SELECT
        MIN(s.fecha)::DATE AS fecha_min,
        MAX(s.fecha)::DATE AS last_seg,
        (SELECT LOWER(COALESCE(estado_operativo_lote, ''))
         FROM lote_ave_engorde
         WHERE lote_ave_engorde_id = p_lote_id AND deleted_at IS NULL) AS estado
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 2d. ⭐ v12: día en que arranca de verdad la ventana de apertura.
--     La ventana previa al encaset (v9) no puede meterse en el ciclo anterior: nada anterior al
--     último día de seguimiento del lote que ocupaba el galpón antes que yo es alimento mío.
--     Complementa a `lotes_ajenos` (v11), que solo caza la limpieza etiquetada con el lote VIEJO;
--     ésta caza la que quedó etiquetada con el lote NUEVO (ver cabecera v12).
corte_apertura AS (
    SELECT GREATEST(
               li.fecha_corte_alimento,
               COALESCE(
                   (SELECT MAX(DATE(s2.fecha)) + 1
                      FROM seguimiento_diario_aves_engorde s2
                      JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id
                                              AND l2.deleted_at IS NULL
                     WHERE l2.granja_id = li.granja_id
                       AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
                       AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
                       AND l2.lote_ave_engorde_id <> p_lote_id
                       -- solo ciclos que YA habían cerrado cuando este empezó
                       AND (SELECT MAX(DATE(s3.fecha))
                              FROM seguimiento_diario_aves_engorde s3
                             WHERE s3.lote_ave_engorde_id = l2.lote_ave_engorde_id) < rs.fecha_min),
                   li.fecha_corte_alimento)
           ) AS desde
    FROM lote_info li, rango_seg rs
    WHERE rs.fecha_min IS NOT NULL
),

-- 2e. ⭐ v14: día en que el galpón deja de ser mío = arranque del ciclo SIGUIENTE.
--     Complemento exacto de `corte_apertura` (v12), que hace lo mismo por el otro extremo: si nada
--     anterior al fin del ciclo previo es alimento mío, nada posterior al arranque del ciclo que me
--     sucede lo es tampoco. Criterio ESTRUCTURAL —otro lote ya tiene seguimiento en este galpón—,
--     no numérico: por eso funciona aunque el lote haya quedado en 'Abierto' sin liquidar y su saldo
--     nunca toque 0 (que es justo lo que pasa cuando el galpón sigue recibiendo alimento).
--     NULL si no hay ciclo posterior (lote genuinamente activo o último del galpón).
corte_ciclo_siguiente AS (
    SELECT MIN(prim.primer_seg) - 1 AS hasta
    FROM (
        SELECT MIN(DATE(s2.fecha)) AS primer_seg
        FROM seguimiento_diario_aves_engorde s2
        JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id
                                AND l2.deleted_at IS NULL
        JOIN lote_info li ON TRUE
        JOIN rango_seg  rs ON rs.last_seg IS NOT NULL
        WHERE l2.granja_id = li.granja_id
          AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
          AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
          AND l2.lote_ave_engorde_id <> p_lote_id
        GROUP BY l2.lote_ave_engorde_id, rs.last_seg
        -- estrictamente POSTERIOR ⇒ los lotes que conviven conmigo (v10) nunca cortan nada
        HAVING MIN(DATE(s2.fecha)) > rs.last_seg
    ) prim
),

-- 2c. ⭐ v11: lotes AJENOS = otros lotes del mismo galpón cuyo ciclo NO se solapa con el mío.
--     Es el COMPLEMENTO exacto del predicado de `consumo_galpon_por_fecha` (v10): si a un lote no le
--     cuento el consumo porque no convive conmigo, tampoco puedo contarle los ingresos ni los
--     traslados. Sin esto la ventana de alimento previo al encaset (v9) se comía la limpieza de
--     cierre del ciclo anterior y la apertura salía negativa (ver cabecera v11).
--     Un lote sin seguimiento cae acá por construcción (igual que en v10): todavía no tiene ciclo.
lotes_ajenos AS (
    SELECT l2.lote_ave_engorde_id AS id
    FROM lote_ave_engorde l2
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE l2.deleted_at IS NULL
      AND l2.lote_ave_engorde_id <> p_lote_id
      AND l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND NOT EXISTS (
            SELECT 1
              FROM seguimiento_diario_aves_engorde s2
             WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
            HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
               AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
),

-- 2b. ⭐ v10: consumo diario de TODOS los lotes del galpón (inventario COMPARTIDO).
--     El alimento vive en la bodega del galpón, no del lote: los ingresos siempre se leyeron con
--     scope galpón, pero el consumo se restaba solo del lote consultado. Con dos lotes solapados en
--     el mismo galpón cada uno veía el 100 % de los ingresos y únicamente su propio consumo, así que
--     LOS DOS inflaban el saldo. Caso G0490 (DOÑA MARIA, jul-2026): ingresos 97.729,6 kg; el lote 168
--     mostraba 82.806,4 (= todo − sus 14.923,3) y el 169 mostraba 34.316,9 (= todo − sus 63.412,8),
--     cuando el saldo real compartido era 19.393,5 contra 18.939,9 de inventario.
--     Acotado por fecha_corte_alimento igual que los ingresos, para no arrastrar ciclos anteriores.
consumo_galpon_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s.lote_ave_engorde_id
                            AND l2.deleted_at IS NULL
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE l2.granja_id = li.granja_id
      AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
      AND (li.fecha_corte_alimento IS NULL OR DATE(s.fecha) >= li.fecha_corte_alimento)
      -- Solo los lotes que CONVIVEN en el galpón comparten bodega. Un galpón que encadena ciclos
      -- sucesivos (Ecuador: 3-4 lotes por galpón, uno detrás de otro) NO comparte nada: cada ciclo
      -- gasta su propio alimento. Sin este filtro el saldo de un lote se movía con el consumo del
      -- ciclo siguiente — y como los lotes viejos quedan en 'Abierto' (fecha_max NULL), la fn les
      -- sigue mostrando fechas posteriores, así que el efecto era grande y equivocado.
      AND (
            l2.lote_ave_engorde_id = p_lote_id
         OR EXISTS (
              SELECT 1
                FROM seguimiento_diario_aves_engorde s2
               WHERE s2.lote_ave_engorde_id = l2.lote_ave_engorde_id
               HAVING MIN(DATE(s2.fecha)) <= rs.last_seg
                  AND MAX(DATE(s2.fecha)) >= rs.fecha_min)
      )
    GROUP BY DATE(s.fecha)
),

-- 3. Saldo de apertura del galpón ANTES del primer seguimiento (v2/v3).
--    ⭐ v6: PISO 0 POR PASO (Lindley), alineado con el frontend/C# (la apertura nunca abre
--    en negativo: una salida sin ingreso previo suficiente se clampa a 0). Antes era un SUM
--    plano que podía dar apertura negativa y desalinear el saldo vs la pantalla (ej. lote 7).
apert_mov AS (
    SELECT DATE(h.fecha_operacion) AS f, h.created_at AS ts,
        CASE h.tipo_evento
            WHEN 'INV_INGRESO'          THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_ENTRADA' THEN  COALESCE(h.cantidad_kg, 0)
            WHEN 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END AS delta,
        -- ⭐ v15: el documento del movimiento, para poder mostrar de DÓNDE sale la apertura.
        -- Mismo criterio que `docs_por_fecha`: número de documento y, si no hay, la referencia.
        NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), '') AS doc
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON rs.fecha_min IS NOT NULL
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.tipo_evento = 'INV_INGRESO'
               AND h.referencia IS NOT NULL
               AND h.referencia LIKE 'Seguimiento aves engorde #%')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND DATE(h.fecha_operacion) < rs.fecha_min
      -- ⭐ v12: la ventana arranca en el corte de v9 o el día siguiente al fin del ciclo
      -- anterior, el que sea más tarde. Caza la limpieza que quedó etiquetada con el lote NUEVO.
      AND (li.fecha_corte_alimento IS NULL
           OR DATE(h.fecha_operacion) >= (SELECT desde FROM corte_apertura))
      -- ⭐ v11: nada del ciclo anterior. Sin este filtro la ventana previa al encaset se comía
      -- la limpieza de cierre del lote que ocupaba el galpón y la apertura salía negativa.
      -- Caza la limpieza etiquetada con el lote VIEJO; la del NUEVO la caza el corte de arriba.
      AND (h.lote_ave_engorde_id IS NULL
           OR NOT EXISTS (SELECT 1 FROM lotes_ajenos la WHERE la.id = h.lote_ave_engorde_id))
),
apert_run AS (
    SELECT
        SUM(delta) OVER (ORDER BY f, ts ROWS UNBOUNDED PRECEDING) AS p,
        ROW_NUMBER()    OVER (ORDER BY f DESC, ts DESC)           AS rn_desc
    FROM apert_mov
),
apertura_alimento AS (
    -- v9: saldo de apertura CRUDO (P final), sin el reseteo de base de Lindley.
    -- ⭐ v10: menos el consumo que OTROS lotes del galpón ya hicieron antes del primer seguimiento
    -- de éste. Sin ese término el segundo lote de un galpón abría con todo el alimento que el
    -- primero ya se había comido. Junto con el consumo de galpón de pt_calc, la suma telescópica
    -- deja saldo(f) = ingresos(≤f) − consumo_del_galpón(≤f) — el mismo valor para los dos lotes.
    SELECT (
        COALESCE((SELECT p FROM apert_run WHERE rn_desc = 1), 0)
      - COALESCE((SELECT SUM(cg.cons_kg)
                    FROM consumo_galpon_por_fecha cg, rango_seg rs
                   WHERE rs.fecha_min IS NOT NULL
                     AND cg.fecha < rs.fecha_min), 0)
    )::FLOAT8 AS apertura_kg
),

-- 3a. ⭐ v15: los DOCUMENTOS de los movimientos que componen la apertura. Hoy esos ingresos entran
--     al saldo del día 1 sin dejar rastro visible (`documento` de la primera fila sale vacío porque
--     `docs_por_fecha` arranca en fecha_min), así que el usuario ve un saldo sin respaldo. Mismo
--     criterio de agregación que `docs_por_fecha` (DISTINCT + ', '), con ORDER BY para que la salida
--     sea determinista. Sin movimientos de apertura devuelve NULL, no cadena vacía.
apertura_docs AS (
    SELECT STRING_AGG(DISTINCT am.doc, ', ' ORDER BY am.doc) AS docs
    FROM apert_mov am
    WHERE am.doc IS NOT NULL
),

-- 3b. ⭐ v5: movimientos de alimento del galpón por fecha, SIN tope superior
--     (para detectar la fecha de cierre = saldo a 0). Neto firmado igual que el saldo.
hist_full AS (
    SELECT
        DATE(h.fecha_operacion) AS fecha,
        SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
                 THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA' THEN COALESCE(h.cantidad_kg, 0)
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'  THEN -ABS(COALESCE(h.cantidad_kg, 0))
            ELSE 0
        END)::FLOAT8 AS neto_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg  rs ON TRUE
    WHERE NOT h.anulado
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
    GROUP BY DATE(h.fecha_operacion)
),

-- 3c. Consumo del seguimiento por fecha (scope LOTE, sin cambios desde v9).
--     Alimenta saldo_running → saldo_close, que detecta el cierre del ciclo. Se deja por lote a
--     propósito: es lo que fija `fecha_max`, y usar aquí el consumo del galpón sería circular
--     (el consumo compartido de pt_calc ya se acota CON `fecha_max`).
consumo_por_fecha AS (
    SELECT DATE(s.fecha) AS fecha,
           SUM(COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS cons_kg
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    GROUP BY DATE(s.fecha)
),

-- 3d. ⭐ v5: saldo de alimento corriente (misma fórmula que la columna saldo_alimento_kg)
--     evaluado sobre TODO el histórico (sin tope) para detectar el cierre.
saldo_running AS (
    SELECT sf.fecha,
        GREATEST(0,
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE(SUM(hf.neto_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
            - COALESCE(SUM(cf.cons_kg) OVER (ORDER BY sf.fecha ROWS UNBOUNDED PRECEDING), 0)
        ) AS saldo
    FROM (SELECT fecha FROM hist_full UNION SELECT fecha FROM consumo_por_fecha) sf
    LEFT JOIN hist_full          hf ON hf.fecha = sf.fecha
    LEFT JOIN consumo_por_fecha  cf ON cf.fecha = sf.fecha
),

-- 3e. ⭐ v5: fecha de cierre = primera fecha >= último seguimiento con saldo en 0
--     (lote vaciado de alimento). NULL si el saldo nunca llega a 0 (lote aún activo).
saldo_close AS (
    SELECT MIN(sr.fecha) AS close_date
    FROM saldo_running sr, rango_seg rs
    WHERE rs.last_seg IS NOT NULL
      AND sr.fecha >= rs.last_seg
      AND sr.saldo <= 0.5
),

-- 4. ⭐ v5: rango final. fecha_max = cierre efectivo (saldo 0) o, si no lo hay,
--    MAX(seg) para lotes 'cerrado' (fallback) o NULL para 'abierto' aún activo.
--    ⭐ v14: y nunca más allá del día previo al arranque del ciclo SIGUIENTE del galpón.
--    LEAST ignora los NULL (solo da NULL si TODO es NULL), así que un lote sin ciclo posterior
--    conserva exactamente su fecha_max de v13 y uno genuinamente activo sigue sin tope.
rango_final AS (
    SELECT
        rs.fecha_min,
        LEAST(
            COALESCE(
                sc.close_date,
                CASE WHEN rs.estado = 'cerrado' THEN rs.last_seg ELSE NULL END
            ),
            cc.hasta
        ) AS fecha_max
    FROM rango_seg rs, saldo_close sc, corte_ciclo_siguiente cc
),

-- 5. Bajas totales en seguimiento
salidas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0) +
        COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0) +
        COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0)
    ), 0) AS bajas_seguimiento
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
),

-- 6. Ventas totales de aves (VENTA_AVES)
ventas_totales AS (
    SELECT COALESCE(SUM(
        COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
    ), 0) AS total_ventas
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
),

-- 7. Aves iniciales (espejo de avesInicialesLote() del frontend)
--    ⭐ v8: ramas que parten de aves_encasetadas/suma_hm restan mort_caja_total (piso 0). La
--    rama 'cerrado' no cambia: ya fuerza el cierre en 0 por construcción propia (bajas+ventas).
aves_iniciales AS (
    SELECT
        CASE
            WHEN li.estado_operativo_lote = 'cerrado'
                THEN GREATEST(1, st.bajas_seguimiento + vt.total_ventas)
            WHEN li.aves_encasetadas > 0 AND li.suma_hm = 0 THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            WHEN li.suma_hm > 0 AND li.aves_encasetadas = 0 THEN GREATEST(0, li.suma_hm - li.mort_caja_total)
            WHEN li.aves_encasetadas = li.suma_hm              THEN GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
            ELSE GREATEST(0, li.aves_encasetadas - li.mort_caja_total)
        END AS inicial
    FROM lote_info li
    CROSS JOIN salidas_totales st
    CROSS JOIN ventas_totales vt
),

-- 8. Ventas VENTA_AVES por fecha (despachos y saldo aves)
ventas_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(
            COALESCE(h.cantidad_hembras, 0) + COALESCE(h.cantidad_machos, 0) + COALESCE(h.cantidad_mixtas, 0)
        ), 0)                                                                          AS ventas_dia,
        COALESCE(SUM(COALESCE(h.cantidad_hembras, 0)), 0)                             AS despacho_h,
        COALESCE(SUM(COALESCE(h.cantidad_machos,  0)), 0)                             AS despacho_m,
        COALESCE(SUM(COALESCE(h.cantidad_mixtas,  0)), 0)                             AS despacho_x,
        COALESCE(SUM(COALESCE(h.peso_neto,      0)), 0)::FLOAT8                        AS despacho_peso_neto,
        COALESCE(SUM(COALESCE(h.peso_tara_real, 0)), 0)::FLOAT8                        AS despacho_peso_tara
    FROM lote_registro_historico_unificado h
    WHERE h.lote_ave_engorde_id = p_lote_id
      AND h.tipo_evento = 'VENTA_AVES'
      AND NOT h.anulado
    GROUP BY DATE(h.fecha_operacion)
),

-- 9. kg de alimento por fecha (scope: granja + nucleo + galpon), acotado a rango_final
hist_alimento AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_INGRESO'
             AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS ingreso_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_ENTRADA'
            THEN COALESCE(h.cantidad_kg, 0) ELSE 0 END), 0)::FLOAT8                  AS traslado_entrada_kg,
        COALESCE(SUM(CASE
            WHEN h.tipo_evento = 'INV_TRASLADO_SALIDA'
            THEN ABS(COALESCE(h.cantidad_kg, 0)) ELSE 0 END), 0)::FLOAT8             AS traslado_salida_kg
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND h.farm_id = li.granja_id
      AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
      AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
    GROUP BY DATE(h.fecha_operacion)
),

-- 10. Documento por fecha (INV_INGRESO scope galpón + VENTA_AVES scope lote), acotado a rango_final
docs_por_fecha AS (
    SELECT
        DATE(h.fecha_operacion)                                                       AS fecha,
        STRING_AGG(
            DISTINCT NULLIF(TRIM(COALESCE(h.numero_documento, h.referencia, '')), ''),
            ', '
        )                                                                              AS documento
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento = 'INV_INGRESO'
           AND NOT (h.referencia IS NOT NULL AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max (ver cabecera).
          (h.tipo_evento = 'VENTA_AVES'
           AND h.lote_ave_engorde_id = p_lote_id)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 11. UNIVERSO DE FECHAS = fechas con seguimiento ∪ fechas con movimientos (acotado a rango_final)
fechas_universo AS (
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
    UNION ALL
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info   li ON TRUE
    JOIN rango_final rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id
           AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
           AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max))
          OR
          -- ⭐ v7: VENTA_AVES del lote SIN tope fecha_min/fecha_max → toda venta (incluso
          -- posterior al cierre por alimento) genera su fila y el saldo cierra en 0.
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
      AND (li.fecha_corte_alimento IS NULL OR DATE(h.fecha_operacion) >= li.fecha_corte_alimento)
      AND NOT EXISTS (
          SELECT 1 FROM seguimiento_diario_aves_engorde s2
          WHERE s2.lote_ave_engorde_id = p_lote_id
            AND DATE(s2.fecha) = DATE(h.fecha_operacion)
      )
    GROUP BY DATE(h.fecha_operacion)
),

-- 12. Seguimiento enriquecido
seg_enriquecido AS (
    SELECT
        s.id                                                                           AS seg_id,
        fu.fecha                                                                       AS fecha,
        CASE WHEN li.fecha_encaset IS NOT NULL
             THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
             ELSE 0 END                                                                AS edad_dia,
        LEAST(8, GREATEST(1,
            CEIL((CASE WHEN li.fecha_encaset IS NOT NULL
                       THEN GREATEST(0, fu.fecha - DATE(li.fecha_encaset))
                       ELSE 0 END + 1) / 7.0)
        ))::SMALLINT                                                                   AS semana,
        COALESCE(s.mortalidad_hembras,   0)                                            AS mortalidad_hembras,
        COALESCE(s.mortalidad_machos,    0)                                            AS mortalidad_machos,
        COALESCE(s.sel_h,                0)                                            AS sel_h,
        COALESCE(s.sel_m,                0)                                            AS sel_m,
        COALESCE(s.error_sexaje_hembras, 0)                                            AS error_sexaje_hembras,
        COALESCE(s.error_sexaje_machos,  0)                                            AS error_sexaje_machos,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)                              AS total_mort_sel_dia,
        COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.mortalidad_machos, 0)
            + COALESCE(s.sel_h, 0) + COALESCE(s.sel_m, 0)
            + COALESCE(s.error_sexaje_hembras, 0) + COALESCE(s.error_sexaje_machos, 0) AS perdidas_totales_dia,
        COALESCE(s.consumo_kg_hembras, 0)::FLOAT8                                      AS consumo_kg_hembras,
        COALESCE(s.consumo_kg_machos,  0)::FLOAT8                                      AS consumo_kg_machos,
        (COALESCE(s.consumo_kg_hembras, 0) + COALESCE(s.consumo_kg_machos, 0))::FLOAT8 AS consumo_dia_kg,
        s.saldo_alimento_kg::FLOAT8                                                    AS saldo_alimento_kg,
        s.tipo_alimento,
        s.peso_prom_hembras::FLOAT8                                                    AS peso_prom_hembras,
        s.peso_prom_machos::FLOAT8                                                     AS peso_prom_machos,
        s.uniformidad_hembras::FLOAT8                                                  AS uniformidad_hembras,
        s.uniformidad_machos::FLOAT8                                                   AS uniformidad_machos,
        s.cv_hembras::FLOAT8                                                           AS cv_hembras,
        s.cv_machos::FLOAT8                                                            AS cv_machos,
        s.consumo_agua_diario::FLOAT8                                                  AS consumo_agua_diario,
        s.consumo_agua_ph::FLOAT8                                                      AS consumo_agua_ph,
        s.consumo_agua_orp::FLOAT8                                                     AS consumo_agua_orp,
        s.consumo_agua_temperatura::FLOAT8                                             AS consumo_agua_temperatura,
        s.observaciones,
        s.ciclo,
        s.metadata,
        s.items_adicionales,
        s.historico_consumo_alimento,
        s.created_by_user_id,
        COALESCE(vpf.ventas_dia, 0)                                                    AS ventas_dia,
        COALESCE(vpf.despacho_h, 0)                                                    AS despacho_h,
        COALESCE(vpf.despacho_m, 0)                                                    AS despacho_m,
        COALESCE(vpf.despacho_x, 0)                                                    AS despacho_x,
        COALESCE(vpf.despacho_peso_neto, 0)                                            AS despacho_peso_neto,
        COALESCE(vpf.despacho_peso_tara, 0)                                            AS despacho_peso_tara,
        COALESCE(ha.ingreso_kg,          0)                                            AS ingreso_alimento_kg,
        COALESCE(ha.traslado_entrada_kg, 0)                                            AS traslado_entrada_kg,
        COALESCE(ha.traslado_salida_kg,  0)                                            AS traslado_salida_kg,
        dpf.documento
    FROM fechas_universo fu
    CROSS JOIN lote_info li
    LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id
    LEFT JOIN ventas_por_fecha vpf ON vpf.fecha = fu.fecha
    LEFT JOIN hist_alimento    ha  ON ha.fecha  = fu.fecha
    LEFT JOIN docs_por_fecha   dpf ON dpf.fecha = fu.fecha
),

-- 12b. ⭐ v6 (M1): P_t = saldo SIN piso (apertura + ingresos acumulados − consumo acumulado).
--      Se materializa aquí para poder tomar MIN(P_t) acumulado en el SELECT final (la
--      forma cerrada de Lindley necesita una ventana sobre otra ventana).
pt_calc AS (
    SELECT se.*,
        (
            (SELECT apertura_kg FROM apertura_alimento)
            + COALESCE((SELECT SUM(ha2.ingreso_kg + ha2.traslado_entrada_kg - ha2.traslado_salida_kg)
                        FROM hist_alimento ha2
                        WHERE ha2.fecha <= se.fecha), 0)
            -- ⭐ v10: consumo de TODO el galpón, no solo el del lote consultado. Acotado a la MISMA
            -- ventana que los ingresos (fecha_min..fecha_max de rango_final): desde fecha_min porque
            -- lo anterior ya está en apertura_alimento, y hasta fecha_max para no traerse el consumo
            -- del ciclo SIGUIENTE del galpón — en Ecuador cada galpón encadena 3-4 lotes y sin ese
            -- tope el saldo de un lote cerrado se movía con el consumo del que vino después.
            - COALESCE((SELECT SUM(cg.cons_kg)
                          FROM consumo_galpon_por_fecha cg, rango_final rf
                         WHERE cg.fecha <= se.fecha
                           AND (rf.fecha_min IS NULL OR cg.fecha >= rf.fecha_min)
                           AND (rf.fecha_max IS NULL OR cg.fecha <= rf.fecha_max)), 0)
        )::FLOAT8 AS pt
    FROM seg_enriquecido se
)

-- 13. Query final
SELECT
    se.seg_id,
    se.fecha,
    se.edad_dia,
    se.semana,
    se.mortalidad_hembras,
    se.mortalidad_machos,
    se.sel_h,
    se.sel_m,
    se.error_sexaje_hembras,
    se.error_sexaje_machos,
    se.total_mort_sel_dia,
    se.perdidas_totales_dia,
    se.consumo_kg_hembras,
    se.consumo_kg_machos,
    se.consumo_dia_kg,
    SUM(se.consumo_dia_kg) OVER w_ord                                                 AS acum_consumo_kg,
    GREATEST(0,
        ai.inicial - SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_ord
    )::INT                                                                             AS saldo_aves,
    CASE
        WHEN GREATEST(0,
            ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
        ) > 0
        THEN (100.0 * se.total_mort_sel_dia /
            GREATEST(0,
                ai.inicial - COALESCE(SUM(se.perdidas_totales_dia + se.ventas_dia) OVER w_prev, 0)
            ))::FLOAT8
        WHEN se.total_mort_sel_dia > 0 THEN 100.0::FLOAT8
        ELSE NULL
    END                                                                                AS pct_perdidas_dia,
    -- ⭐ v9 (M2): saldo CRUDO, sin piso ni reseteo de base.
    -- El reseteo de Lindley "olvidaba" el déficit transitorio para no mostrar negativos, pero cada
    -- olvido regalaba alimento inexistente y el acumulado terminaba por encima del inventario: en el
    -- galpón 6 de DAYLAND (jul-2026) el reporte cerraba en 12.869,46 kg contra 2.235,33 reales,
    -- inflado exactamente en el peor negativo (−10.634,13 del 05/07). Un saldo negativo no es un
    -- error de cálculo: dice que el alimento se consumió antes de que su llegada quedara registrada.
    -- Espeja a RecalcularSaldoAlimentoPorLoteAsync / SeguimientoAvesEngordeCalculos, que ya no pisan.
    se.pt::FLOAT8                                                                      AS saldo_alimento_kg,
    se.ingreso_alimento_kg,
    se.traslado_entrada_kg,
    se.traslado_salida_kg,
    se.consumo_dia_kg                                                                  AS consumo_bodega_kg,
    se.documento,
    se.despacho_h  AS despacho_hembras,
    se.despacho_m  AS despacho_machos,
    se.despacho_x  AS despacho_mixtas,
    se.despacho_peso_neto                                                             AS despacho_peso_neto,
    se.despacho_peso_tara                                                             AS despacho_peso_tara,
    CASE WHEN (se.despacho_h + se.despacho_m + se.despacho_x) > 0
         THEN se.despacho_peso_neto / (se.despacho_h + se.despacho_m + se.despacho_x)
         ELSE 0 END                                                                   AS despacho_promedio_peso_ave,
    se.tipo_alimento,
    se.peso_prom_hembras,
    se.peso_prom_machos,
    se.uniformidad_hembras,
    se.uniformidad_machos,
    se.cv_hembras,
    se.cv_machos,
    se.consumo_agua_diario,
    se.consumo_agua_ph,
    se.consumo_agua_orp,
    se.consumo_agua_temperatura,
    se.observaciones,
    se.ciclo,
    se.metadata,
    se.items_adicionales,
    se.historico_consumo_alimento,
    se.created_by_user_id,
    -- ⭐ v15: «Ingreso inicial del ciclo». Es EL MISMO escalar `apertura_alimento` que ya alimenta
    -- `saldo_alimento_kg` desde v9 — no se recalcula nada, solo se expone. Va únicamente en la
    -- primera fila de `fecha_min` (si hay dos seguimientos ese día, en el de menor seg_id) para que
    -- sumar la columna a lo largo del ciclo no duplique kg. Sin seguimiento (fecha_min NULL) la
    -- comparación da NULL ⇒ ELSE ⇒ 0/NULL, que es lo correcto: ahí no hay apertura, los movimientos
    -- previos se siguen viendo como filas propias.
    CASE WHEN se.fecha = (SELECT fecha_min FROM rango_final) AND ROW_NUMBER() OVER w_ap = 1
         THEN (SELECT apertura_kg FROM apertura_alimento)
         ELSE 0::FLOAT8
    END                                                                                AS apertura_alimento_kg,
    CASE WHEN se.fecha = (SELECT fecha_min FROM rango_final) AND ROW_NUMBER() OVER w_ap = 1
         THEN (SELECT docs FROM apertura_docs)
         ELSE NULL::TEXT
    END                                                                                AS apertura_documentos
FROM pt_calc se
CROSS JOIN aves_iniciales ai
WINDOW
    w_ord  AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
    w_prev AS (ORDER BY se.fecha, COALESCE(se.seg_id, 0) ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING),
    -- ⭐ v15: desempate dentro del día de fecha_min (un día puede traer más de un seguimiento).
    w_ap   AS (PARTITION BY se.fecha ORDER BY COALESCE(se.seg_id, 0))
-- (orden final: lo aplica el SELECT exterior de la union)
      ) t
     WHERE NOT EXISTS (SELECT 1
                         FROM liquidacion_lote_engorde_congelada c
                        WHERE c.lote_ave_engorde_id = p_lote_id
                          AND c.anulada_at IS NULL)
) u
ORDER BY u.fecha, COALESCE(u.seg_id, 0);
$$;
