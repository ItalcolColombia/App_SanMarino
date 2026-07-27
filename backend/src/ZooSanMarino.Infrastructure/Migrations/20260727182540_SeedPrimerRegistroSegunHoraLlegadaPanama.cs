using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: activa <c>companies.primer_registro_segun_hora_llegada</c> para
    /// <b>ItalcolPanama</b>, que es la operación donde la hora de llegada de las aves define si el
    /// primer consumo va el día del encasetamiento o el siguiente.
    /// <para>
    /// Idempotente: lookup por <c>name</c> (los ids difieren entre local y prod) y
    /// <c>IS DISTINCT FROM</c> para no ensuciar <c>updated_at</c>. Si la empresa no existe, el UPDATE
    /// no afecta ninguna fila y la migración pasa igual.
    /// </para>
    /// <para>
    /// Las demás empresas quedan en <c>false</c> ⇒ la hora se ignora y su comportamiento no cambia.
    /// </para>
    /// </summary>
    public partial class SeedPrimerRegistroSegunHoraLlegadaPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET primer_registro_segun_hora_llegada = true
                 WHERE name = 'ItalcolPanama'
                   AND primer_registro_segun_hora_llegada IS DISTINCT FROM true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE companies
                   SET primer_registro_segun_hora_llegada = false
                 WHERE name = 'ItalcolPanama'
                   AND primer_registro_segun_hora_llegada IS DISTINCT FROM false;
            ");
        }
    }
}
