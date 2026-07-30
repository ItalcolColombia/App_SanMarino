using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Índice <c>(farm_id, fecha_operacion)</c> sobre <c>lote_registro_historico_unificado</c>.
    /// <para>
    /// La tabla tenía índices por <c>id</c>, <c>(origen_tabla, origen_id)</c>,
    /// <c>(lote_ave_engorde_id, fecha_operacion)</c>, <c>(company_id, fecha_operacion)</c> y
    /// <c>tipo_evento</c>, pero ninguno por GRANJA — y todo el cálculo del saldo de alimento de engorde
    /// lee con scope de ubicación (granja + núcleo + galpón).
    /// </para>
    /// <para>
    /// Se vuelve necesario ahora porque el saldo persistido pasó a refrescarse en CADA movimiento de
    /// inventario (<c>SaldoAlimentoEngordeAplicador</c>), lo que dispara
    /// <c>fn_seguimiento_diario_engorde</c> una vez por lote del galpón — hasta 4.
    /// </para>
    /// <para>
    /// Medido con EXPLAIN ANALYZE sobre el dump de producción restaurado en local
    /// (12.247 filas, 4,2 MB):
    /// </para>
    /// <list type="bullet">
    /// <item><c>fn_seguimiento_diario_engorde(98)</c> completa: <b>10,3 ms → 2,7 ms</b></item>
    /// <item>consulta del histórico por ubicación de <c>…Service.SaldoAlimento.cs</c> (la que corre al
    /// crear o editar un seguimiento y en «Cuadrar Saldos»): <b>Seq Scan 4,3 ms → Bitmap Index Scan
    /// 0,55 ms</b>, dejando de descartar toda la tabla en cada llamada</item>
    /// </list>
    /// <para>
    /// <b>Por qué solo <c>(farm_id, fecha_operacion)</c> y no la ubicación completa:</b> los dos caminos
    /// comparan el núcleo y el galpón con <c>COALESCE(TRIM(...), '')</c>, que NO es sargable. Con un
    /// índice <c>(farm_id, nucleo_id, galpon_id, fecha_operacion)</c> el plan real ata en
    /// <c>Index Cond</c> únicamente <c>farm_id</c> y deja las otras dos columnas en <c>Filter</c>: serían
    /// peso muerto. Para indexar también la ubicación haría falta un índice de EXPRESIÓN sobre los
    /// <c>COALESCE(TRIM(...))</c>, que hoy no se justifica.
    /// </para>
    /// Idempotente (<c>IF NOT EXISTS</c>). Plan: fase_de_desarrollo/fix_apertura_alimento_ciclo_anterior_plan.md
    /// </summary>
    public partial class IndiceHistoricoUnificadoPorGranjaFecha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_lote_hist_farm_fecha
    ON public.lote_registro_historico_unificado (farm_id, fecha_operacion);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS public.ix_lote_hist_farm_fecha;");
        }
    }
}
