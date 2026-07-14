using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCompanyIdSeguimientoProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill de datos (sin cambio de esquema): seguimiento_diario_produccion.company_id
            // quedaba en 0 porque ProduccionService.CrearSeguimientoAsync no lo seteaba (fix de
            // codigo ya aplicado). Infiere company_id desde el lote propietario de cada fila.
            // Idempotente: solo toca filas todavia en 0; si un lote_id no resuelve (huerfano),
            // esa fila queda en 0 (no se adivina la empresa).
            migrationBuilder.Sql(@"
                UPDATE seguimiento_diario_produccion s
                SET company_id = l.company_id
                FROM lotes l
                WHERE s.company_id = 0
                  AND l.lote_id = s.lote_id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible: no hay forma de distinguir las filas que este backfill toco
            // de las que ya tenian company_id correcto desde su creacion.
        }
    }
}
