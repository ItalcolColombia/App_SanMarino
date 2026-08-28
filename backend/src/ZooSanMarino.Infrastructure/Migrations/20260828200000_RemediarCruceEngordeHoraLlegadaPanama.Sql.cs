// Partial de la migracion RemediarCruceEngordeHoraLlegadaPanama: las dos constantes SQL, para que el
// archivo principal se pueda leer. Mismo patron que 20260828170000_FnCruceReproductoraEngordeHoraLlegada.
//
// No hay espejo en backend/sql/: esto no crea un objeto de BD, es una operacion de datos de UNA sola
// vez. El diagnostico que la mide SI vive alli (verificar_cruce_engorde_hora_llegada.sql, exento del
// gate por ser de solo lectura).

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    public partial class RemediarCruceEngordeHoraLlegadaPanama
    {
        /// <summary>
        /// Devuelve las aves al maestro, anula el historico, alinea el encaset del reproductora,
        /// recalcula el cruce con la fn canonica y vuelve a aplicar las bajas de las filas nuevas.
        /// Guardado por la tabla de respaldo: de UNA sola vez.
        /// </summary>
        private const string RemediacionUp = """
-- ---------------------------------------------------------------------------
-- PASO 0 - Cohorte, resuelta por la REGLA (no por ids fijos) y congelada, mas
--          el respaldo que hace reversible al Down.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS _backup_remediacion_cruce_hora_20260828 (
    lote_ave_engorde_id integer PRIMARY KEY,
    encaset_repro_viejo timestamptz,
    hembras_l_viejo     integer,
    machos_l_viejo      integer,
    mixtas_viejo        integer,
    aplicado_at         timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS _backup_remediacion_cruce_hora_seg_20260828 AS
SELECT * FROM seguimiento_diario_aves_engorde WHERE false;

CREATE TABLE IF NOT EXISTS _backup_remediacion_cruce_hora_hist_20260828 (
    hist_id bigint PRIMARY KEY,
    accion  text   NOT NULL CHECK (accion IN ('ANULADA', 'INSERTADA'))
);

-- ---------------------------------------------------------------------------
-- Todo el trabajo va dentro de un bloque GUARDADO. Es una remediacion de UNA
-- SOLA VEZ, no una operacion convergente: si se re-corriera, el PASO 3 volveria
-- a borrar y recrear las filas de cruce con ids NUEVOS, y las filas del historico
-- de la corrida anterior quedarian huerfanas y SIN anular -justo el invariante
-- que esto viene a cuidar-. La marca es la tabla de respaldo con filas.
-- ---------------------------------------------------------------------------
DO $rem$
BEGIN
IF EXISTS (SELECT 1 FROM _backup_remediacion_cruce_hora_20260828) THEN
    RAISE NOTICE 'remediacion_cruce_hora: ya aplicada, no se repite';
    RETURN;
END IF;

INSERT INTO _backup_remediacion_cruce_hora_20260828 (
    lote_ave_engorde_id, encaset_repro_viejo, hembras_l_viejo, machos_l_viejo, mixtas_viejo)
SELECT lae.lote_ave_engorde_id,
       (SELECT lr.fecha_encasetamiento FROM lote_reproductora_ave_engorde lr
         WHERE lr.lote_ave_engorde_id = lae.lote_ave_engorde_id
         ORDER BY lr.id LIMIT 1),
       COALESCE(lae.hembras_l, 0), COALESCE(lae.machos_l, 0), COALESCE(lae.mixtas, 0)
  FROM lote_ave_engorde lae
 WHERE lae.deleted_at IS NULL
   AND lae.hora_encasetamiento >= time '13:00'
   AND COALESCE(lae.aves_encasetadas, 0) > 0
   AND EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                WHERE s.lote_ave_engorde_id = lae.lote_ave_engorde_id
                  AND s.origen_cruce
                  AND (s.fecha AT TIME ZONE 'UTC')::date
                      < (lae.fecha_encaset AT TIME ZONE 'UTC')::date + 1)
ON CONFLICT (lote_ave_engorde_id) DO NOTHING;

INSERT INTO _backup_remediacion_cruce_hora_seg_20260828
SELECT s.* FROM seguimiento_diario_aves_engorde s
 JOIN _backup_remediacion_cruce_hora_20260828 b USING (lote_ave_engorde_id)
 WHERE s.origen_cruce;

-- ---------------------------------------------------------------------------
-- PASO 1 - Devolver al maestro las aves de las filas de cruce que van a morir y
--          ANULAR su fila del historico. Es el paso 1 de SincronizarCruceAsync
--          (filas cuyo seguimiento ya no existe = devolver las aves), corrido
--          ANTES del borrado, que es cuando todavia se puede leer el baseline.
--          El baseline lo manda la FILA DEL HISTORICO, no el seguimiento.
-- ---------------------------------------------------------------------------
WITH vivas AS (
    SELECT h.id, h.lote_ave_engorde_id,
           COALESCE(h.cantidad_hembras, 0) AS ch,
           COALESCE(h.cantidad_machos, 0)  AS cm,
           COALESCE(h.cantidad_mixtas, 0)  AS cx
      FROM lote_registro_historico_unificado h
      JOIN _backup_remediacion_cruce_hora_seg_20260828 b ON b.id = h.origen_id::bigint
     WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
       AND h.tipo_evento  = 'BAJA_SEGUIMIENTO'
       AND NOT h.anulado
), totales AS (
    SELECT lote_ave_engorde_id, SUM(ch) AS th, SUM(cm) AS tm, SUM(cx) AS tx
      FROM vivas GROUP BY 1
), anul AS (
    UPDATE lote_registro_historico_unificado h SET anulado = true
      FROM vivas v WHERE h.id = v.id
    RETURNING h.id
), bkp AS (
    INSERT INTO _backup_remediacion_cruce_hora_hist_20260828 (hist_id, accion)
    SELECT id, 'ANULADA' FROM anul
    ON CONFLICT (hist_id) DO NOTHING
    RETURNING 1
)
UPDATE lote_ave_engorde l
   SET hembras_l  = COALESCE(l.hembras_l, 0) + t.th,
       machos_l   = COALESCE(l.machos_l, 0)  + t.tm,
       mixtas     = COALESCE(l.mixtas, 0)    + t.tx,
       updated_at = now()
  FROM totales t
 WHERE l.lote_ave_engorde_id = t.lote_ave_engorde_id;

-- ---------------------------------------------------------------------------
-- PASO 2 - Alinear el encaset del lote reproductora con el de su padre engorde.
--          El cruce mapea por EDAD: un desfase corre la serie entera. Solo se
--          tocan los lotes de la cohorte, no los otros desalineados del universo.
-- ---------------------------------------------------------------------------
UPDATE lote_reproductora_ave_engorde lr
   SET fecha_encasetamiento = lae.fecha_encaset,
       updated_at           = now()
  FROM lote_ave_engorde lae
  JOIN _backup_remediacion_cruce_hora_20260828 b
    ON b.lote_ave_engorde_id = lae.lote_ave_engorde_id
 WHERE lr.lote_ave_engorde_id = lae.lote_ave_engorde_id
   AND lr.fecha_encasetamiento IS DISTINCT FROM lae.fecha_encaset;

-- ---------------------------------------------------------------------------
-- PASO 3 - Recalcular el cruce con la fn CANONICA (la unica formula del numero).
--          El UPDATE del paso 2 no dispara el trigger: vive sobre el seguimiento
--          reproductora, no sobre el lote. Hay que llamarla.
-- ---------------------------------------------------------------------------
PERFORM fn_cruce_reproductora_a_engorde(lote_ave_engorde_id)
   FROM _backup_remediacion_cruce_hora_20260828 ORDER BY lote_ave_engorde_id;

-- ---------------------------------------------------------------------------
-- PASO 4 - Aplicar al maestro las bajas de las filas NUEVAS y escribir su fila
--          del historico. Paso 2 de SincronizarCruceAsync, con el mismo reparto
--          (RetiroAvesEngordeCalculos.EsLoteMixto) y el mismo clamp a 0.
--          Idempotente por el NOT EXISTS sobre (origen_tabla, origen_id), que es
--          la clave unica del historico.
-- ---------------------------------------------------------------------------
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
      JOIN _backup_remediacion_cruce_hora_20260828 b
        ON b.lote_ave_engorde_id = s.lote_ave_engorde_id
     WHERE s.origen_cruce
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
), bkp2 AS (
    INSERT INTO _backup_remediacion_cruce_hora_hist_20260828 (hist_id, accion)
    SELECT id, 'INSERTADA' FROM ins
    ON CONFLICT (hist_id) DO NOTHING
    RETURNING 1
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
END $rem$;
""";

        /// <summary>
        /// Reversion exacta guiada por los tres respaldos que dejo el Up, que ademas se borran al
        /// final. Si el Up no corrio, las tablas no existen y esto es un no-op.
        /// </summary>
        private const string RemediacionDown = """
-- Reversion EXACTA, guiada por los tres respaldos que dejo el Up. Sin ellos no hay
-- forma de distinguir lo que movio esta migracion de lo que ya estaba: las filas del
-- historico que el aplicador anulo en regeneraciones anteriores tienen la misma pinta.
-- Si el Up no corrio, las tablas no existen y el Down es un no-op.
DO $rem$
BEGIN
    IF to_regclass('public._backup_remediacion_cruce_hora_20260828') IS NULL THEN
        RETURN;
    END IF;

    -- 1) Borrar las filas del historico que INSERTO el Up y desanular las que ANULO.
    DELETE FROM lote_registro_historico_unificado h
     USING _backup_remediacion_cruce_hora_hist_20260828 b
     WHERE h.id = b.hist_id AND b.accion = 'INSERTADA';

    UPDATE lote_registro_historico_unificado h SET anulado = false
      FROM _backup_remediacion_cruce_hora_hist_20260828 b
     WHERE h.id = b.hist_id AND b.accion = 'ANULADA';

    -- 2) Restaurar el maestro a los valores previos (respaldados antes de tocar nada).
    UPDATE lote_ave_engorde l
       SET hembras_l  = b.hembras_l_viejo,
           machos_l   = b.machos_l_viejo,
           mixtas     = b.mixtas_viejo,
           updated_at = now()
      FROM _backup_remediacion_cruce_hora_20260828 b
     WHERE l.lote_ave_engorde_id = b.lote_ave_engorde_id;

    -- 3) Reponer las filas de cruce originales, con sus ids: el historico las nombra
    --    por origen_id y con ids nuevos quedaria apuntando al vacio.
    DELETE FROM seguimiento_diario_aves_engorde s
     USING _backup_remediacion_cruce_hora_20260828 b
     WHERE s.lote_ave_engorde_id = b.lote_ave_engorde_id AND s.origen_cruce;

    INSERT INTO seguimiento_diario_aves_engorde
    SELECT * FROM _backup_remediacion_cruce_hora_seg_20260828;

    -- 4) Devolver el encaset del lote reproductora a donde estaba.
    UPDATE lote_reproductora_ave_engorde lr
       SET fecha_encasetamiento = b.encaset_repro_viejo,
           updated_at           = now()
      FROM _backup_remediacion_cruce_hora_20260828 b
     WHERE lr.lote_ave_engorde_id = b.lote_ave_engorde_id
       AND lr.fecha_encasetamiento IS DISTINCT FROM b.encaset_repro_viejo;
END $rem$;

DROP TABLE IF EXISTS _backup_remediacion_cruce_hora_hist_20260828;
DROP TABLE IF EXISTS _backup_remediacion_cruce_hora_seg_20260828;
DROP TABLE IF EXISTS _backup_remediacion_cruce_hora_20260828;
""";
    }
}
