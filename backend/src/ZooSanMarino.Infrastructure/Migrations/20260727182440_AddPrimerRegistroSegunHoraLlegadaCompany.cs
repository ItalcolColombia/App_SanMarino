using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag por empresa <c>companies.primer_registro_segun_hora_llegada</c>: la HORA de llegada de las
    /// aves decide el primer día con registro del lote (pollo engorde y reproductora). Desde las 13:00
    /// las aves ya no alcanzan a consumir ese día y el primer consumo pasa al día siguiente.
    /// <para>
    /// Ni la fecha de encasetamiento ni la edad cambian: la edad se sigue contando desde
    /// <c>fecha_encaset</c>, así que un lote tardío arranca en edad 1. Con el flag APAGADO la hora
    /// cargada en el lote se ignora por completo ⇒ la empresa se comporta exactamente como antes.
    /// </para>
    /// <para>
    /// Default <c>false</c> ⇒ todas las empresas existentes conservan el comportamiento actual byte a
    /// byte. Idempotente (<c>ADD COLUMN IF NOT EXISTS</c>) para tolerar reintentos de deploy.
    /// </para>
    /// </summary>
    public partial class AddPrimerRegistroSegunHoraLlegadaCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    ADD COLUMN IF NOT EXISTS primer_registro_segun_hora_llegada boolean NOT NULL DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE companies
                    DROP COLUMN IF EXISTS primer_registro_segun_hora_llegada;
            ");
        }
    }
}
