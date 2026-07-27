using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: activa <c>companies.seguimiento_engorde_mixto</c> para
    /// <b>ItalcolPanama</b> — al salir de reproductora el pollo engorde no se maneja por sexo, así
    /// que la plantilla de carga masiva del seguimiento diario se emite con columnas MIXTAS
    /// («Mort Mixta», «Consumo Mixto (kg)»…) en lugar del par H/M.
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren entre local y prod) y
    /// <c>IS DISTINCT FROM</c> para no ensuciar <c>updated_at</c> ni reescribir filas ya correctas.
    /// Si la empresa no existe, el UPDATE no afecta ninguna fila y la migración pasa igual.
    /// </para>
    /// <para>
    /// Las demás empresas quedan en <c>false</c> ⇒ plantilla por sexo, sin cambios visibles.
    /// </para>
    /// </summary>
    public partial class SeedSeguimientoEngordeMixtoPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET seguimiento_engorde_mixto = true
                 WHERE name = 'ItalcolPanama'
                   AND seguimiento_engorde_mixto IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET seguimiento_engorde_mixto = false
                 WHERE name = 'ItalcolPanama'
                   AND seguimiento_engorde_mixto IS DISTINCT FROM false;
            ");
        }
    }
}
