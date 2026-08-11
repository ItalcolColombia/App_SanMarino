using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: enciende <c>companies.programacion_lotes_engorde</c> para
    /// <b>ItalcolPanama</b>, que ya venía operando así (lote base obligatorio + nombre por corrida,
    /// antes gateado en el front por país). Sin este seed, el cambio de gate apagaría una feature
    /// que en Panamá ya está en producción.
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren local↔prod) y <c>IS DISTINCT FROM</c>
    /// para no ensuciar filas ya correctas. Si la empresa no existe, no afecta ninguna fila.
    /// </para>
    /// </summary>
    public partial class SeedProgramacionLotesEngordePanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET programacion_lotes_engorde = true
                 WHERE name = 'ItalcolPanama'
                   AND programacion_lotes_engorde IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET programacion_lotes_engorde = false
                 WHERE name = 'ItalcolPanama'
                   AND programacion_lotes_engorde IS DISTINCT FROM false;
            ");
        }
    }
}
