using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Historial enriquecido del puente Panamá: columna jsonb <c>detalle_json</c> en
    /// <c>migracion_masiva</c> para persistir el detalle completo de cada corrida
    /// (ResultadoSincronizacionDto podado) y poder reconstruir en el historial los mismos
    /// contadores/mensajes de la previsualización. Nullable: los demás tipos de migración
    /// no la usan y su contrato (errores/errores_json) queda intacto. Idempotente.
    /// </summary>
    public partial class AddDetalleJsonMigracionMasiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.migracion_masiva
                    ADD COLUMN IF NOT EXISTS detalle_json jsonb NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.migracion_masiva
                    DROP COLUMN IF EXISTS detalle_json;
            ");
        }
    }
}
