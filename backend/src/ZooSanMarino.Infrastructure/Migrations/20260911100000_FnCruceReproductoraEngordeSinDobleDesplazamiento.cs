using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El cruce reproductora → pollo engorde deja de correr la serie <b>dos veces</b> cuando el lote
    /// reproductora ya arrancó en la edad 1 por esa misma llegada tardía.
    ///
    /// <para>
    /// <b>El defecto.</b> Desde ago-2026 <c>fn_cruce_reproductora_a_engorde</c> fecha el destino en
    /// <c>fecha_encaset + v_desp + d</c>, con <c>v_desp = 1</c> si las aves llegaron 13:00 o después.
    /// Esa regla se escribió para el caso en que la reproductora <b>sí capturó el día del
    /// encasetamiento</b> (edad 0): ese consumo es real, pertenece al día siguiente y por eso se corre
    /// la serie entera. Pero cuando la reproductora <b>ya arrancó en la edad 1</b> —por la misma
    /// llegada tardía, que es lo correcto— el <c>+1</c> se suma sobre un día que ya estaba corrido y
    /// el registro del día siguiente al encaset aterriza en el subsiguiente.
    /// </para>
    ///
    /// <para>
    /// <b>Lo que ve el usuario</b> (ticket de operación Panamá, 11-sep-2026, granja DOÑA MARIA, lote
    /// 255 «95 - 3», encaset 03-sep 21:35): la reproductora capturó del <b>04-sep</b> al 09-sep y la
    /// tabla de seguimiento pollo engorde arranca el <b>05-sep</b>, en «Edad 2». Las filas son
    /// <c>origen_cruce</c> ⇒ solo lectura en la UI: por eso el caso termina en desarrollo.
    /// </para>
    ///
    /// <para>
    /// 🔑 <b>Prueba independiente de que es un defecto y no una convención:</b>
    /// <c>EncasetamientoCalculos.PrimerDiaConRegistro</c> —el guarda de C# que valida la captura
    /// manual— dice que el primer día válido del lote 255 es el <b>04-sep</b>, y la fn escribe el
    /// 05-sep. La BD y el backend se contradecían.
    /// </para>
    ///
    /// <para>
    /// <b>La regla nueva</b> (una línea): <c>desplazamiento_efectivo = GREATEST(0, v_desp − primera
    /// edad que el cruce realmente genera)</c>. El <c>GREATEST</c> es la regla y no una defensa: sin
    /// hora informada el desplazamiento es 0 y una primera edad ≥ 1 <b>no puede</b> correr la serie
    /// hacia atrás sobre el día del encaset — esa fila se capturó el día que dice su edad. Espejo puro
    /// en <c>EncasetamientoCalculos.DesplazamientoCruce</c>, con tests (la fn es la dueña del número;
    /// el C# es su especificación ejecutable).
    /// </para>
    ///
    /// <para>
    /// <b>Radio medido sobre la copia de producción</b> (gate multipaís de CLAUDE.md,
    /// <c>backend/sql/verificar_cruce_desplazamiento_doble.sql</c>, <c>EXCEPT</c> en los dos sentidos
    /// sobre TODAS las empresas): se mueven <b>4 lotes de ItalcolPanama</b> (239, 255, 256, 257), 19
    /// filas, y <b>solo cambia el día</b> — las cifras por lote y edad son idénticas (0 y 0 al comparar
    /// sin la fecha). Los 2 lotes cuya reproductora sí capturó la edad 0 (215, 216) quedan igual, los
    /// 35 sin hora quedan igual, y ItalcolEcuador / Demo / Sanmarino tienen <b>0 filas</b>
    /// <c>origen_cruce</c> en toda la BD ⇒ impacto cero.
    /// </para>
    ///
    /// <para>
    /// <b>La remediación de datos va acá adentro</b>, no en un <c>.sql</c> suelto: nada de
    /// <c>backend/sql/</c> llega a producción por sí solo. Se selecciona por DATO, no por id: los lotes
    /// cuyo cruce guardado tiene <c>desplazamientoHora = 1</c> y <b>ninguna fila en la edad 0</b> — que
    /// hoy son exactamente esos 4. Es idempotente: en la segunda pasada el cruce ya no se mueve y el
    /// bloque no toca nada.
    /// </para>
    ///
    /// <para>
    /// <b>Y la captura manual sigue al cruce.</b> El lote 239 ya tiene 5 días digitados a mano
    /// (05→09-sep) pegados al final de su cruce. Mover solo el cruce le dejaría el 04-sep vacío en el
    /// medio de la serie, así que el bloque corre también esa cola —fila por fila, en orden
    /// ascendente, que es el único orden en que un corrimiento hacia atrás no choca con el índice
    /// único por día (misma trampa que ya conocíamos)— y solo si el día que liberó el cruce quedó
    /// realmente libre y la captura manual empezaba justo ahí.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Y el histórico de aves también (agregado 12-sep-2026, al validar contra la copia de
    /// producción del día).</b> El cruce borra y reinserta sus filas con ids NUEVOS: sin más, las
    /// <c>BAJA_SEGUIMIENTO</c> vivas del histórico quedaban apuntando a seguimientos inexistentes (con
    /// la fecha vieja) y las filas nuevas sin la suya. En la app eso lo repara
    /// <c>RetiroAvesEngordeAplicador.SincronizarCruceAsync</c> después del trigger, pero una migración
    /// no puede llamarlo y en un lote cuya reproductora ya cerró la semana (239, 255) nada lo volvería a
    /// disparar. Se replica en SQL, con el patrón de <c>20260828200000_RemediarCruceEngordeHoraLlegadaPanama</c>:
    /// anular las huérfanas devolviendo sus aves y aplicar las nuevas. Las cifras por edad son idénticas,
    /// así que el maestro de aves termina en el mismo número. Y la fila de bajas de cada registro manual
    /// que se corre sigue a su registro, igual que al corregir la fecha por pantalla
    /// (<c>UpsertHistorico</c> la actualiza en su lugar); los movimientos de inventario del consumo no se
    /// tocan, tampoco los re-fecha la edición por pantalla.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>El saldo de alimento hay que reescribirlo.</b> Medido: re-correr el cruce (que borra y
    /// reinserta sus filas) deja <c>saldo_alimento_kg</c> en <c>NULL</c> —la columna la escribe
    /// <c>SaldoAlimentoEngordeAplicador</c> después, desde <c>fn_seguimiento_diario_engorde</c>—, así
    /// que el bloque la recalcula con el mismo SQL del aplicador para los lotes que tocó.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/fecha_encaset_recalculo_cascada_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_cruce_reproductora_a_engorde.sql</c> — esta migración es el
    /// <b>vehículo</b>. Idempotente: <c>CREATE OR REPLACE</c> + <c>DROP/CREATE TRIGGER</c> +
    /// <c>INDEX IF NOT EXISTS</c>. Sin cambios de modelo (ModelSnapshot intacto).
    /// </summary>
    public partial class FnCruceReproductoraEngordeSinDobleDesplazamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnCruceReproductoraAEngordeSinDobleDesplazamiento);
            migrationBuilder.Sql(REMEDIACION_SQL);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin reversión deliberada, por el mismo criterio que la migración que introdujo la regla
            // de la hora (20260828170000): la versión anterior es la que manda el primer registro un
            // día después del que el propio backend considera válido. Un Down que reintroduce el
            // defecto no es un Down. La remediación de datos tampoco se deshace: devolver las filas a
            // un día equivocado no es revertir, es romper otra vez.
            migrationBuilder.Sql(
                "-- Sin reversion deliberada: ver el comentario del Down en la migracion.");
        }

        /// <summary>
        /// Remediación de los lotes que quedaron con la serie corrida de más. Selecciona por DATO
        /// (cruce con <c>desplazamientoHora = 1</c> y sin fila en la edad 0), no por id.
        /// </summary>
        private const string REMEDIACION_SQL = @"
DO $remediar$
DECLARE
    v_lote          int;
    v_lotes         int[] := ARRAY[]::int[];
    v_ultimo_viejo  date;
    v_ultimo_nuevo  date;
    v_fila          record;
    v_movidas       int;
BEGIN
    FOR v_lote IN
        SELECT s.lote_ave_engorde_id
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde lae ON lae.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE s.origen_cruce
           AND lae.deleted_at IS NULL
         GROUP BY s.lote_ave_engorde_id
        -- El cruce guardado dice que se corrio un dia...
        HAVING MAX((s.metadata->>'desplazamientoHora')::int) = 1
        -- ...y sin embargo no tiene ninguna fila en la edad 0: la reproductora ya habia arrancado
        -- en la edad 1 y el desplazamiento se aplico por segunda vez.
           AND MIN((s.metadata->>'edad')::int) >= 1
         ORDER BY 1
    LOOP
        SELECT MAX((fecha AT TIME ZONE 'UTC')::date) INTO v_ultimo_viejo
          FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = v_lote AND origen_cruce;

        PERFORM fn_cruce_reproductora_a_engorde(v_lote);
        v_lotes := v_lotes || v_lote;

        SELECT MAX((fecha AT TIME ZONE 'UTC')::date) INTO v_ultimo_nuevo
          FROM seguimiento_diario_aves_engorde
         WHERE lote_ave_engorde_id = v_lote AND origen_cruce;

        -- Si el cruce no se movio exactamente un dia hacia atras, no hay cola que correr.
        CONTINUE WHEN v_ultimo_viejo IS NULL OR v_ultimo_nuevo IS NULL;
        CONTINUE WHEN v_ultimo_nuevo <> v_ultimo_viejo - 1;

        -- El dia v_ultimo_viejo quedo libre. La cola digitada a mano solo se corre si empezaba
        -- JUSTO ahi (si no, mover el cruce no abrio ningun hueco) y si ese dia quedo realmente
        -- vacio (si algo lo ocupa, correr la cola chocaria con el indice unico).
        CONTINUE WHEN NOT EXISTS (
            SELECT 1 FROM seguimiento_diario_aves_engorde
             WHERE lote_ave_engorde_id = v_lote AND NOT origen_cruce
               AND (fecha AT TIME ZONE 'UTC')::date = v_ultimo_viejo + 1);
        CONTINUE WHEN EXISTS (
            SELECT 1 FROM seguimiento_diario_aves_engorde
             WHERE lote_ave_engorde_id = v_lote
               AND (fecha AT TIME ZONE 'UTC')::date = v_ultimo_viejo);

        v_movidas := 0;
        -- Orden ASCENDENTE: es el unico en el que un corrimiento hacia atras nunca pisa una fila
        -- que todavia no se movio. El dia destino de la primera ya lo libero el cruce.
        FOR v_fila IN
            SELECT id FROM seguimiento_diario_aves_engorde
             WHERE lote_ave_engorde_id = v_lote AND NOT origen_cruce
               AND (fecha AT TIME ZONE 'UTC')::date > v_ultimo_viejo
             ORDER BY fecha ASC
        LOOP
            UPDATE seguimiento_diario_aves_engorde
               SET fecha = fecha - interval '1 day'
             WHERE id = v_fila.id;

            -- La fila de bajas del historico sigue al registro, igual que cuando se corrige la fecha
            -- desde el formulario: RetiroAvesEngordeAplicador.UpsertHistorico la actualiza EN SU
            -- LUGAR (fecha_operacion y la fecha de la referencia). Los movimientos de inventario del
            -- consumo no se tocan: la edicion por pantalla tampoco los re-fecha (solo emite el diff).
            UPDATE lote_registro_historico_unificado
               SET fecha_operacion = fecha_operacion - 1,
                   referencia      = regexp_replace(referencia, '[0-9]{4}-[0-9]{2}-[0-9]{2}',
                                                    to_char(fecha_operacion - 1, 'YYYY-MM-DD'))
             WHERE origen_tabla = 'seguimiento_diario_aves_engorde'
               AND tipo_evento  = 'BAJA_SEGUIMIENTO'
               AND origen_id    = v_fila.id::int;

            v_movidas := v_movidas + 1;
        END LOOP;

        RAISE NOTICE 'Cruce del lote %: la serie vuelve al % y se corrieron % dia(s) digitados a mano.',
            v_lote, v_ultimo_nuevo, v_movidas;
    END LOOP;

    -- El cruce borro y reinserto sus filas con ids NUEVOS => las BAJA_SEGUIMIENTO vivas del historico
    -- quedaron apuntando a seguimientos que ya no existen, y las filas nuevas no tienen la suya. Es
    -- lo que en la app corrige RetiroAvesEngordeAplicador.SincronizarCruceAsync despues del trigger;
    -- una migracion no puede llamarlo, asi que se replica en SQL (mismo patron que
    -- 20260828200000_RemediarCruceEngordeHoraLlegadaPanama). Sin esto el invariante del historico
    -- se rompe: nada volveria a sincronizar un lote cuya reproductora ya cerro la semana (239, 255).
    -- Las cifras por edad son identicas, asi que el maestro de aves termina en el mismo numero.
    IF cardinality(v_lotes) > 0 THEN
        -- PASO A (paso 1 de SincronizarCruceAsync): devolver las aves de las filas huerfanas y anularlas.
        -- El baseline lo manda la FILA DEL HISTORICO.
        WITH huerfanas AS (
            SELECT h.id, h.lote_ave_engorde_id,
                   COALESCE(h.cantidad_hembras, 0) AS ch,
                   COALESCE(h.cantidad_machos, 0)  AS cm,
                   COALESCE(h.cantidad_mixtas, 0)  AS cx
              FROM lote_registro_historico_unificado h
              JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = h.lote_ave_engorde_id
                                     AND l.deleted_at IS NULL
                                     AND COALESCE(l.aves_encasetadas, 0) > 0
             WHERE h.lote_ave_engorde_id = ANY (v_lotes)
               AND h.origen_tabla = 'seguimiento_diario_aves_engorde'
               AND h.tipo_evento  = 'BAJA_SEGUIMIENTO'
               AND NOT h.anulado
               AND NOT EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s WHERE s.id = h.origen_id)
        ), anul AS (
            UPDATE lote_registro_historico_unificado h SET anulado = true
              FROM huerfanas x WHERE h.id = x.id
            RETURNING h.id
        ), totales AS (
            SELECT lote_ave_engorde_id, SUM(ch) AS th, SUM(cm) AS tm, SUM(cx) AS tx
              FROM huerfanas GROUP BY 1
        )
        UPDATE lote_ave_engorde l
           SET hembras_l  = COALESCE(l.hembras_l, 0) + t.th,
               machos_l   = COALESCE(l.machos_l, 0)  + t.tm,
               mixtas     = COALESCE(l.mixtas, 0)    + t.tx,
               updated_at = now()
          FROM totales t
         WHERE l.lote_ave_engorde_id = t.lote_ave_engorde_id;

        -- PASO B (paso 2 de SincronizarCruceAsync): aplicar las bajas de las filas NUEVAS y escribir su
        -- fila del historico, con el mismo reparto (RetiroAvesEngordeCalculos.EsLoteMixto) y el mismo
        -- clamp a 0. Idempotente por el NOT EXISTS sobre (origen_tabla, origen_id), la clave unica.
        WITH pendientes AS (
            SELECT s.id AS seg_id, (s.fecha AT TIME ZONE 'UTC')::date AS fecha_op,
                   l.lote_ave_engorde_id, l.company_id, l.granja_id, l.nucleo_id, l.galpon_id,
                   (COALESCE(s.mortalidad_hembras, 0) + COALESCE(s.sel_h, 0)
                    + COALESCE(s.error_sexaje_hembras, 0)) AS bajas_h,
                   (COALESCE(s.mortalidad_machos, 0) + COALESCE(s.sel_m, 0)
                    + COALESCE(s.error_sexaje_machos, 0)) AS bajas_m,
                   (COALESCE(l.mixtas, 0) > 0 AND COALESCE(l.hembras_l, 0) = 0
                    AND COALESCE(l.machos_l, 0) = 0)       AS es_mixto
              FROM seguimiento_diario_aves_engorde s
              JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
                                     AND l.deleted_at IS NULL
             WHERE s.lote_ave_engorde_id = ANY (v_lotes)
               AND s.origen_cruce
               AND COALESCE(l.aves_encasetadas, 0) > 0
               AND NOT EXISTS (SELECT 1 FROM lote_registro_historico_unificado h
                                WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
                                  AND h.origen_id    = s.id::int)
        ), con_bajas AS (
            SELECT * FROM pendientes WHERE bajas_h + bajas_m > 0
        ), ins AS (
            INSERT INTO lote_registro_historico_unificado (
                company_id, lote_ave_engorde_id, farm_id, nucleo_id, galpon_id, fecha_operacion,
                tipo_evento, origen_tabla, origen_id,
                cantidad_hembras, cantidad_machos, cantidad_mixtas, referencia, anulado)
            SELECT c.company_id, c.lote_ave_engorde_id, c.granja_id, c.nucleo_id, c.galpon_id, c.fecha_op,
                   'BAJA_SEGUIMIENTO', 'seguimiento_diario_aves_engorde', c.seg_id::int,
                   CASE WHEN c.es_mixto THEN 0 ELSE c.bajas_h END,
                   CASE WHEN c.es_mixto THEN 0 ELSE c.bajas_m END,
                   CASE WHEN c.es_mixto THEN c.bajas_h + c.bajas_m ELSE 0 END,
                   'Bajas seguimiento aves engorde #' || c.seg_id::int || ' '
                     || to_char(c.fecha_op, 'YYYY-MM-DD'),
                   false
              FROM con_bajas c
            RETURNING id
        ), totales AS (
            SELECT lote_ave_engorde_id,
                   SUM(CASE WHEN es_mixto THEN 0 ELSE bajas_h END)           AS tot_h,
                   SUM(CASE WHEN es_mixto THEN 0 ELSE bajas_m END)           AS tot_m,
                   SUM(CASE WHEN es_mixto THEN bajas_h + bajas_m ELSE 0 END) AS tot_x
              FROM con_bajas GROUP BY lote_ave_engorde_id
        )
        UPDATE lote_ave_engorde l
           SET hembras_l  = CASE WHEN t.tot_h > 0
                                 THEN GREATEST(0, COALESCE(l.hembras_l, 0) - t.tot_h) ELSE l.hembras_l END,
               machos_l   = CASE WHEN t.tot_m > 0
                                 THEN GREATEST(0, COALESCE(l.machos_l, 0)  - t.tot_m) ELSE l.machos_l END,
               mixtas     = CASE WHEN t.tot_x > 0
                                 THEN GREATEST(0, COALESCE(l.mixtas, 0)    - t.tot_x) ELSE l.mixtas END,
               updated_at = now()
          FROM totales t
         WHERE l.lote_ave_engorde_id = t.lote_ave_engorde_id;
    END IF;

    -- El cruce borro y reinserto sus filas => saldo_alimento_kg quedo en NULL. Mismo SQL que
    -- SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync.
    FOREACH v_lote IN ARRAY v_lotes LOOP
        WITH nuevos AS (
            SELECT f.seg_id, ROUND(f.saldo_alimento_kg::numeric, 3) AS saldo
              FROM fn_seguimiento_diario_engorde(v_lote) f
             WHERE f.seg_id IS NOT NULL
        )
        UPDATE seguimiento_diario_aves_engorde s
           SET saldo_alimento_kg = n.saldo
          FROM nuevos n
         WHERE s.id = n.seg_id
           AND s.saldo_alimento_kg IS DISTINCT FROM n.saldo;
    END LOOP;
END
$remediar$;
";
    }
}
