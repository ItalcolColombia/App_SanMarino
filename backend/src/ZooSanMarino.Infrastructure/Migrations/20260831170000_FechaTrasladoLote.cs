using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Agrega <c>historial_traslado_lote.fecha_traslado</c>: el <b>día real</b> en que el lote se
    /// movió, separado del instante en que alguien lo registró.
    /// </summary>
    /// <remarks>
    /// <b>Qué faltaba.</b> Trasladar o mover un lote no tiene campo de fecha en ningún lado: ni
    /// <c>TrasladoLoteRequestDto</c>, ni <c>MoverLoteDto</c>, ni las interfaces del front, ni la
    /// tabla; <c>fn_mover_lote</c> escribía <c>CURRENT_TIMESTAMP</c>. El pedido del usuario que abrió
    /// <c>TK-2026-000012</c> decía, textual, «la fecha de traslado de aves <b>o de lote</b>», y aquel
    /// fix cubrió solo las aves.
    ///
    /// <b>Por qué importa aunque sea la fecha de un registro:</b> el Reporte Diario de Costos de
    /// POSTURA usa ese <c>created_at</c> como fecha efectiva del traslado, así que un lote movido hoy
    /// pero que cambió de granja la semana pasada le atribuye costos a la granja equivocada durante
    /// esos días.
    ///
    /// <b>Riesgo del backfill: cero.</b> Medido antes: <c>historial_traslado_lote</c> tiene
    /// <b>0 filas</b> en la copia de producción. El <c>UPDATE</c> que copia <c>created_at</c> a la
    /// columna nueva existe igual, para que el día que se despliegue sobre una tabla con datos
    /// —otro ambiente, o producción más adelante— ninguna fila quede sin fecha.
    ///
    /// <b>Nullable con fallback a hoy.</b> <c>fn_mover_lote</c> recibe <c>p_fecha_traslado</c> con
    /// <c>DEFAULT NULL</c> y hace <c>COALESCE(p_fecha_traslado, CURRENT_DATE)</c>: un llamador que no
    /// lo mande se comporta exactamente como antes. La firma anterior de 5 argumentos se
    /// <b>elimina</b> —agregar un parámetro con default crea una sobrecarga y deja las dos vivas, y
    /// una llamada de 5 argumentos quedaría ambigua—.
    ///
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c>, <c>CREATE INDEX IF NOT EXISTS</c>,
    /// <c>CREATE OR REPLACE FUNCTION</c> y un backfill acotado a las filas sin fecha.
    ///
    /// Plan: <c>fase_de_desarrollo/correccion_hallazgos_auditoria_tickets_plan.md</c> (hallazgo #11).
    /// </remarks>
    public partial class FechaTrasladoLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.historial_traslado_lote
    ADD COLUMN IF NOT EXISTS fecha_traslado date;

-- Backfill: el día del registro es la mejor aproximación que existe para lo ya escrito.
UPDATE public.historial_traslado_lote
   SET fecha_traslado = created_at::date
 WHERE fecha_traslado IS NULL;

CREATE INDEX IF NOT EXISTS idx_historial_traslado_fecha
    ON public.historial_traslado_lote (fecha_traslado);

-- La firma vieja se elimina: con DEFAULT NULL quedarían DOS funciones y una llamada de 5
-- argumentos sería ambigua.
DROP FUNCTION IF EXISTS public.fn_mover_lote(integer, integer, character varying, character varying, integer);

CREATE OR REPLACE FUNCTION public.fn_mover_lote(
    p_lote_id integer,
    p_granja_dest integer,
    p_nucleo_dest character varying,
    p_galpon_dest character varying,
    p_user_id integer,
    p_fecha_traslado date DEFAULT NULL)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_granja_origen integer;
    v_company_id    integer;
BEGIN
    SELECT granja_id, company_id
      INTO v_granja_origen, v_company_id
      FROM public.lotes
     WHERE lote_id = p_lote_id;

    UPDATE public.lotes
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id;

    UPDATE public.lote_postura_levante
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    UPDATE public.lote_postura_produccion
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    -- Solo si REALMENTE cambió de granja: mover de galpón dentro de la misma granja no
    -- es un traslado y ensuciaría el historial. lote_nuevo_id = lote_original_id porque
    -- este camino reubica el MISMO lote (mismo criterio que TrasladarLoteAsync).
    IF v_granja_origen IS NOT NULL AND v_granja_origen IS DISTINCT FROM p_granja_dest THEN
        INSERT INTO public.historial_traslado_lote (
            lote_original_id, lote_nuevo_id,
            granja_origen_id, granja_destino_id,
            nucleo_destino_id, galpon_destino_id,
            observaciones, company_id, created_by_user_id, created_at, fecha_traslado)
        VALUES (
            p_lote_id, p_lote_id,
            v_granja_origen, p_granja_dest,
            p_nucleo_dest, p_galpon_dest,
            'Movimiento de ubicación', COALESCE(v_company_id, 0),
            COALESCE(p_user_id, 0), CURRENT_TIMESTAMP,
            -- El día del hecho lo elige quien registra; sin dato, hoy: un llamador que no
            -- mande el parámetro se comporta igual que antes de esta migración.
            COALESCE(p_fecha_traslado, CURRENT_DATE));
    END IF;

    -- Silos del lote: si se mudó de granja, los que tenía son de la granja vieja y ya no
    -- pueden alimentarlo. Se quitan; el usuario reasigna en la granja nueva.
    DELETE FROM public.lote_silos ls
     USING public.farm_silos fs
     WHERE ls.lote_id      = p_lote_id
       AND ls.farm_silo_id = fs.id
       AND fs.granja_id   <> p_granja_dest;
END;
$function$;
");

            // El reporte de costos pasa a fechar el traslado por el DIA REAL.
            migrationBuilder.Sql(FN_COSTOS_CON_FECHA_TRASLADO);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Primero el reporte: su version nueva referencia la columna que se dropea al final.
            migrationBuilder.Sql(FN_COSTOS_PREVIA);

            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS public.fn_mover_lote(integer, integer, character varying, character varying, integer, date);

CREATE OR REPLACE FUNCTION public.fn_mover_lote(
    p_lote_id integer,
    p_granja_dest integer,
    p_nucleo_dest character varying,
    p_galpon_dest character varying,
    p_user_id integer)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_granja_origen integer;
    v_company_id    integer;
BEGIN
    SELECT granja_id, company_id
      INTO v_granja_origen, v_company_id
      FROM public.lotes
     WHERE lote_id = p_lote_id;

    UPDATE public.lotes
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id;

    UPDATE public.lote_postura_levante
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    UPDATE public.lote_postura_produccion
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    IF v_granja_origen IS NOT NULL AND v_granja_origen IS DISTINCT FROM p_granja_dest THEN
        INSERT INTO public.historial_traslado_lote (
            lote_original_id, lote_nuevo_id,
            granja_origen_id, granja_destino_id,
            nucleo_destino_id, galpon_destino_id,
            observaciones, company_id, created_by_user_id, created_at)
        VALUES (
            p_lote_id, p_lote_id,
            v_granja_origen, p_granja_dest,
            p_nucleo_dest, p_galpon_dest,
            'Movimiento de ubicación', COALESCE(v_company_id, 0),
            COALESCE(p_user_id, 0), CURRENT_TIMESTAMP);
    END IF;

    DELETE FROM public.lote_silos ls
     USING public.farm_silos fs
     WHERE ls.lote_id      = p_lote_id
       AND ls.farm_silo_id = fs.id
       AND fs.granja_id   <> p_granja_dest;
END;
$function$;

DROP INDEX IF EXISTS public.idx_historial_traslado_fecha;
ALTER TABLE public.historial_traslado_lote DROP COLUMN IF EXISTS fecha_traslado;
");
        }
    }
}
