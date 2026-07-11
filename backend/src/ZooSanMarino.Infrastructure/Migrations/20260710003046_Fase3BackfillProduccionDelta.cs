using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase3BackfillProduccionDelta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill del delta de producción: copia las filas de la deprecada
            // produccion_seguimiento que NO tienen contraparte en la canónica
            // (por lote_id + día). IDEMPOTENTE (NOT EXISTS) y NO-OP local
            // (produccion_seguimiento tiene 0 filas). Se guarda por si prod tuviera
            // datos. El guard to_regclass evita error si la tabla ya fue retirada.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF to_regclass('public.produccion_seguimiento') IS NOT NULL THEN
                        INSERT INTO public.seguimiento_diario_produccion_reproductoras (
                            lote_id, fecha_registro,
                            mortalidad_hembras, mortalidad_machos, sel_h, sel_m,
                            cons_kg_h, cons_kg_m, huevo_tot, huevo_inc,
                            tipo_alimento, observaciones, peso_huevo, etapa,
                            error_sexaje_hembras, error_sexaje_machos,
                            traslado_ingreso_hembras, traslado_ingreso_machos,
                            traslado_salida_hembras, traslado_salida_machos,
                            es_traslado, traslado_direccion,
                            traslado_lote_contraparte_id, traslado_granja_contraparte_id,
                            traslado_hembras, traslado_machos, lote_destino_id, granja_destino_id,
                            fecha_traslado, traslado_observaciones,
                            company_id, created_by_user_id, created_at, updated_by_user_id, updated_at
                        )
                        SELECT
                            ps.lote_id, ps.fecha_registro::timestamptz,
                            ps.mortalidad_h, ps.mortalidad_m, ps.sel_h, ps.sel_m,
                            ps.consumo_kg, 0, ps.huevos_totales, ps.huevos_incubables,
                            '', ps.observaciones, ps.peso_huevo, 0,
                            ps.error_sexaje_hembras, ps.error_sexaje_machos,
                            ps.traslado_ingreso_hembras, ps.traslado_ingreso_machos,
                            ps.traslado_salida_hembras, ps.traslado_salida_machos,
                            ps.es_traslado, ps.traslado_direccion,
                            ps.traslado_lote_contraparte_id, ps.traslado_granja_contraparte_id,
                            ps.traslado_hembras, ps.traslado_machos, ps.lote_destino_id, ps.granja_destino_id,
                            ps.fecha_traslado, ps.traslado_observaciones,
                            ps.company_id, ps.created_by_user_id, ps.created_at, ps.updated_by_user_id, ps.updated_at
                        FROM public.produccion_seguimiento ps
                        WHERE ps.deleted_at IS NULL
                          AND NOT EXISTS (
                            SELECT 1 FROM public.seguimiento_diario_produccion_reproductoras sd
                            WHERE sd.lote_id = ps.lote_id
                              AND sd.fecha_registro::date = ps.fecha_registro
                          );
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill no reversible por migración (ver §10 del plan: revertir por
            // marca/pg_dump). No-op.
        }
    }
}
