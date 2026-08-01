using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// DROP de espejo_huevo_produccion.historico_semanal (jsonb) + su índice GIN, con OK
    /// explícito del usuario (01-ago-2026). Evidencia del retiro (forensia del plan
    /// seguimiento_produccion_fn_canonica): vacío ('{}' o NULL) en el 100 % de las filas de
    /// prod, sin escritores vivos (su único poblador era el trigger legacy
    /// tr_espejo_huevo_produccion_aiud, retirado por 20260801071000, cuya rama UPDATE además
    /// nunca lo mantuvo) y CERO lectores en backend, frontend y SQL. No se pierde información:
    /// el detalle por semana es derivable on-demand desde seguimiento_diario_produccion.
    /// Idempotente: DROP ... IF EXISTS. Down() recrea columna e índice vacíos (el dato era
    /// vacío, no hay nada que restaurar).
    /// </summary>
    public partial class DropHistoricoSemanalEspejoHuevoProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_espejo_huevo_produccion_historico_semanal;
                ALTER TABLE espejo_huevo_produccion DROP COLUMN IF EXISTS historico_semanal;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE espejo_huevo_produccion ADD COLUMN IF NOT EXISTS historico_semanal jsonb DEFAULT '{}'::jsonb;
                CREATE INDEX IF NOT EXISTS ix_espejo_huevo_produccion_historico_semanal
                    ON espejo_huevo_produccion USING gin (historico_semanal);
                """);
        }
    }
}
