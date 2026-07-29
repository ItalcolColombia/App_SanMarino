using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Aplica al maestro <c>lote_ave_engorde</c> las bajas de los 7 dias del CRUCE de reproductora
    /// que nunca se descontaron. Equivale a correr
    /// <c>RetiroAvesEngordeAplicador.SincronizarCruceAsync</c> sobre la cohorte afectada.
    ///
    /// Contexto: esos dias los genera el trigger de <c>fn_cruce_reproductora_a_engorde</c> por SQL
    /// directo, sin pasar por el service, asi que su mortalidad nunca llegaba a
    /// <c>hembras_l/machos_l</c>. Medido en la BD de Panama: 24 lotes, 168 dias, 8.411 aves
    /// (4.131 H + 4.280 M); ninguna otra empresa tiene dias de cruce sin aplicar.
    ///
    /// La tabla diaria NUNCA tuvo el problema (calcula <c>aves_encasetadas - bajas</c>); el que
    /// quedaba por encima del real era el maestro, y con el las «Aves disponibles».
    ///
    /// Detalles que importan:
    /// * <b>Idempotente</b>: el <c>NOT EXISTS</c> sobre <c>(origen_tabla, origen_id)</c> —la clave
    ///   unica del historico— deja de aplicar en la segunda corrida. Se comprueba sin filtrar por
    ///   <c>tipo_evento</c> ni <c>anulado</c> justamente porque esa unique cubre la fila entera.
    /// * <b>Fecha en UTC</b>: los dias de cruce estan anclados a medianoche UTC (19:00-05), y las
    ///   filas que el aplicador ya escribio usan <c>(fecha AT TIME ZONE 'UTC')::date</c>. Usar
    ///   <c>fecha::date</c> a secas dependeria de la zona de la sesion y correria el dia uno atras.
    /// * <b>Reparto por DATOS del lote</b>, igual que <c>RetiroAvesEngordeCalculos.EsLoteMixto</c>:
    ///   un lote cuya poblacion vive toda en <c>mixtas</c> descuenta de mixtas; el resto por sexo.
    /// * <b>Guarda anti doble descuento</b>: los lotes con <c>aves_encasetadas = 0</c> quedan fuera,
    ///   porque ahi <c>fn_seguimiento_diario_engorde</c> deriva las iniciales del propio maestro y
    ///   moverlo restaria las bajas dos veces (misma guarda que el aplicador C#).
    /// * <b>Clamp a 0</b> por si algun dia histórico reporta mas bajas de las que el lote tiene.
    ///   Verificado en la cohorte actual: ningun lote lo necesita (minimo resultante 8.399 H / 0 M),
    ///   asi que el UPDATE agregado es equivalente al descuento dia por dia del aplicador.
    ///
    /// Data-only: Designer clonado, ModelSnapshot intacto.
    /// Plan: fase_de_desarrollo/cuadre_engorde_panama_aves_alimento_plan.md
    /// </summary>
    public partial class AplicarBajasCruceReproductoraAlMaestroEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Respaldo de los seguimientos que ESTA migracion aplica. Sin el, el Down no podria
            // distinguirlos de las filas que el aplicador ya habia escrito (lotes 142/179/180/181)
            // y devolveria aves de mas. Mismo patron que CuadreInventarioVsSeguimiento2602.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS _backup_bajas_cruce_engorde_20260729 (
    seg_id              bigint PRIMARY KEY,
    lote_ave_engorde_id integer NOT NULL,
    cantidad_hembras    integer NOT NULL,
    cantidad_machos     integer NOT NULL,
    cantidad_mixtas     integer NOT NULL,
    aplicado_at         timestamptz NOT NULL DEFAULT now()
);");

            migrationBuilder.Sql(@"
WITH pendientes AS (
    SELECT s.id                                                     AS seg_id,
           (s.fecha AT TIME ZONE 'UTC')::date                       AS fecha_op,
           l.lote_ave_engorde_id,
           l.company_id, l.granja_id, l.nucleo_id, l.galpon_id,
           (COALESCE(s.mortalidad_hembras,0) + COALESCE(s.sel_h,0)
            + COALESCE(s.error_sexaje_hembras,0))                   AS bajas_h,
           (COALESCE(s.mortalidad_machos,0) + COALESCE(s.sel_m,0)
            + COALESCE(s.error_sexaje_machos,0))                    AS bajas_m,
           (COALESCE(l.mixtas,0) > 0
            AND COALESCE(l.hembras_l,0) = 0
            AND COALESCE(l.machos_l,0)  = 0)                        AS es_mixto
      FROM seguimiento_diario_aves_engorde s
      JOIN lote_ave_engorde l
        ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
       AND l.deleted_at IS NULL
     WHERE s.origen_cruce
       AND COALESCE(l.aves_encasetadas, 0) > 0
       AND NOT EXISTS (
             SELECT 1 FROM lote_registro_historico_unificado h
              WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
                AND h.origen_id    = s.id::int)
),
con_bajas AS (
    SELECT * FROM pendientes WHERE bajas_h + bajas_m > 0
),
ins AS (
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
    RETURNING 1
),
bkp AS (
    INSERT INTO _backup_bajas_cruce_engorde_20260729 (
        seg_id, lote_ave_engorde_id, cantidad_hembras, cantidad_machos, cantidad_mixtas)
    SELECT c.seg_id, c.lote_ave_engorde_id,
           CASE WHEN c.es_mixto THEN 0 ELSE c.bajas_h END,
           CASE WHEN c.es_mixto THEN 0 ELSE c.bajas_m END,
           CASE WHEN c.es_mixto THEN c.bajas_h + c.bajas_m ELSE 0 END
      FROM con_bajas c
    ON CONFLICT (seg_id) DO NOTHING
    RETURNING 1
),
totales AS (
    SELECT lote_ave_engorde_id,
           SUM(CASE WHEN es_mixto THEN 0 ELSE bajas_h END)          AS tot_h,
           SUM(CASE WHEN es_mixto THEN 0 ELSE bajas_m END)          AS tot_m,
           SUM(CASE WHEN es_mixto THEN bajas_h + bajas_m ELSE 0 END) AS tot_x
      FROM con_bajas
     GROUP BY lote_ave_engorde_id
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
 WHERE l.lote_ave_engorde_id = t.lote_ave_engorde_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devuelve al maestro SOLO las aves de los seguimientos que quedaron respaldados en el Up
            // y borra sus filas del historico. Las que el aplicador ya habia escrito antes
            // (lotes 142/179/180/181) no estan en el respaldo y no se tocan.
            migrationBuilder.Sql(@"
WITH totales AS (
    SELECT lote_ave_engorde_id,
           SUM(cantidad_hembras) th, SUM(cantidad_machos) tm, SUM(cantidad_mixtas) tx
      FROM _backup_bajas_cruce_engorde_20260729
     GROUP BY lote_ave_engorde_id
),
upd AS (
    UPDATE lote_ave_engorde l
       SET hembras_l  = CASE WHEN t.th > 0 THEN COALESCE(l.hembras_l, 0) + t.th ELSE l.hembras_l END,
           machos_l   = CASE WHEN t.tm > 0 THEN COALESCE(l.machos_l, 0)  + t.tm ELSE l.machos_l END,
           mixtas     = CASE WHEN t.tx > 0 THEN COALESCE(l.mixtas, 0)    + t.tx ELSE l.mixtas END,
           updated_at = now()
      FROM totales t
     WHERE l.lote_ave_engorde_id = t.lote_ave_engorde_id
    RETURNING 1
)
DELETE FROM lote_registro_historico_unificado h
 USING _backup_bajas_cruce_engorde_20260729 b
 WHERE h.origen_tabla = 'seguimiento_diario_aves_engorde'
   AND h.origen_id    = b.seg_id::int
   AND h.tipo_evento  = 'BAJA_SEGUIMIENTO';");

            migrationBuilder.Sql("DROP TABLE IF EXISTS _backup_bajas_cruce_engorde_20260729;");
        }
    }
}
