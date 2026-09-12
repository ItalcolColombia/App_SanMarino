using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Alinea el modelo EF con la BD: <c>produccion_resultado_levante.lote_id</c> es <b>text</b> (lo escribe
    /// <c>sp_recalcular_seguimiento_levante(l_lote_id text)</c>), pero el modelo lo declaraba integer. La
    /// entidad sigue siendo <c>int</c>, ahora con <c>HasConversion&lt;string&gt;()</c>.
    ///
    /// <para>
    /// Sin esto, <c>GET /api/SeguimientoLoteLevante/por-lote/{id}/resultado</c> filtraba
    /// <c>text = integer</c>. Junto con el parámetro del SP pasado como texto, el endpoint vuelve a
    /// responder (daba 500 en toda empresa desde may-2026).
    /// </para>
    ///
    /// <para>
    /// Idempotente y sin efecto donde la columna ya es text (local y producción): convierte solo si no lo
    /// es. <c>Down</c> no revierte el tipo: el SP escribe texto y volver a integer lo rompería.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/seguimiento_varios_por_dia_errores_reportes_plan.md</c>.
    /// </summary>
    public partial class ProduccionResultadoLevanteLoteIdTexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $mig$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                         WHERE table_schema = 'public'
                           AND table_name = 'produccion_resultado_levante'
                           AND column_name = 'lote_id'
                           AND data_type <> 'text'
                    ) THEN
                        ALTER TABLE public.produccion_resultado_levante
                            ALTER COLUMN lote_id TYPE text USING lote_id::text;
                    END IF;
                END
                $mig$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío: la columna es text en la BD desde antes de esta migración.
        }
    }
}
