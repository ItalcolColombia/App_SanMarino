using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: activa <c>companies.captura_huevos_en_levante</c> para
    /// <b>Agroavicola Sanmarino</b> (la empresa que pidió capturar huevos en levante desde la
    /// semana 14 con arrastre a producción al liquidar).
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren entre local y prod) y
    /// <c>IS DISTINCT FROM</c> para no ensuciar <c>updated_at</c> ni reescribir filas ya correctas.
    /// Si la empresa no existe, el UPDATE no afecta ninguna fila y la migración pasa igual.
    /// </para>
    /// <para>
    /// Para habilitarlo en otra empresa alcanza con un UPDATE equivalente (o una migración gemela);
    /// las demás empresas quedan en <c>false</c> ⇒ sin cambios visibles.
    /// </para>
    /// </summary>
    public partial class SeedCapturaHuevosEnLevante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET captura_huevos_en_levante = true
                 WHERE name = 'Agroavicola Sanmarino'
                   AND captura_huevos_en_levante IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET captura_huevos_en_levante = false
                 WHERE name = 'Agroavicola Sanmarino'
                   AND captura_huevos_en_levante IS DISTINCT FROM false;
            ");
        }
    }
}
