using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FnMoverLoteRegistraHistorialTraslado : Migration
    {
        // fn_mover_lote pisaba lotes.granja_id (y los espejos de fase) SIN dejar rastro de la granja
        // anterior. El otro camino de traslado, LoteService.TrasladarLoteAsync, si inserta en
        // historial_traslado_lote; este no, asi que la granja donde ocurrio cada dia quedaba
        // irrecuperable y el Reporte Diario de Costos de Postura re-atribuia TODO el historico a la
        // granja nueva (el levante hecho en NIZA III aparecia en NIZA I).
        //
        // Ahora, y SOLO si el movimiento cambia de granja, deja el hecho fechado. Es un INSERT de
        // auditoria: no cambia una sola linea del movimiento en si, y mover de galpon dentro de la
        // misma granja no escribe nada.
        //
        // Idempotente: CREATE OR REPLACE, misma firma, sin DDL de tablas ni cambios de modelo
        // (ModelSnapshot intacto). Fuente canonica: backend/sql/fn_mover_ubicacion.sql

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"-- ----------------------------------------------------------------------------
--
-- v2 (2026-08-07): si el movimiento CAMBIA DE GRANJA, deja el hecho fechado en
-- historial_traslado_lote, igual que hace LoteService.TrasladarLoteAsync. Sin eso,
-- `lotes.granja_id` se pisa y no queda forma de saber en qué granja ocurrió cada día:
-- el Reporte Diario de Costos de Postura re-atribuía TODO el histórico a la granja
-- nueva (el levante hecho en NIZA III aparecía en NIZA I). Es un INSERT de auditoría,
-- aditivo: no cambia una sola línea del movimiento en sí.
CREATE OR REPLACE FUNCTION public.fn_mover_lote(
    p_lote_id      integer,
    p_granja_dest  integer,
    p_nucleo_dest  varchar,
    p_galpon_dest  varchar,
    p_user_id      integer
) RETURNS void
LANGUAGE plpgsql
AS $$
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
            observaciones, company_id, created_by_user_id, created_at)
        VALUES (
            p_lote_id, p_lote_id,
            v_granja_origen, p_granja_dest,
            p_nucleo_dest, p_galpon_dest,
            'Movimiento de ubicación', COALESCE(v_company_id, 0),
            COALESCE(p_user_id, 0), CURRENT_TIMESTAMP);
    END IF;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: quitar el registro de auditoria volveria a perder la granja de origen.
            // La fn queda como esta; revertir la migracion no rompe nada.
        }
    }
}
