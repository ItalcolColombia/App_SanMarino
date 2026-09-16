using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Flag tipado por comportamiento <c>companies.permite_seguimiento_diario_parcial</c> (default
    /// <c>false</c> = todas las empresas actuales siguen exigiendo alimento/aves/huevos como hoy).
    /// <para>
    /// Con el flag en <c>true</c>, el seguimiento diario de LEVANTE y de PRODUCCIÓN se puede guardar
    /// con datos en un solo bloque (alimento, aves, o huevos) — incluso completamente vacío — sin que
    /// el backend ni el formulario exijan los demás campos. Pensado para empresas con varias capturas
    /// por día (ver <c>permite_multiples_seguimientos_diarios</c>).
    /// </para>
    /// Enciende el flag SOLO para la empresa <c>Santa Reyes</c> (sembrada por la migración
    /// <c>20260725190000_SeedEmpresaSantaReyes</c>, que corre ANTES que esta). Aditiva e
    /// <b>idempotente</b> (IF NOT EXISTS + UPDATE con guarda): re-ejecutable sin efectos.
    /// <para>
    /// NOTA: <c>dotnet ef migrations add</c> detectó de rebote un cambio de modelo preexistente y no
    /// relacionado (<c>ProduccionResultadoLevante.LoteId</c> string/text → string/varchar(64), sin
    /// migración propia todavía) y lo incluyó en el scaffold inicial. Se excluyó a propósito de este
    /// archivo y se revirtió la porción correspondiente del ModelSnapshot y del Designer de esta
    /// migración (a como está en HEAD), para no mezclarlo con este cambio ni pisar la migración que
    /// le corresponde.
    /// </para>
    /// </summary>
    public partial class AddPermiteSeguimientoDiarioParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    ADD COLUMN IF NOT EXISTS permite_seguimiento_diario_parcial boolean NOT NULL DEFAULT false;
");

            // Santa Reyes es la única empresa con seguimientos diarios parciales por ahora.
            // Lookup por nombre (nunca por id fijo) + guarda IS DISTINCT FROM → re-ejecutable.
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET permite_seguimiento_diario_parcial = true
 WHERE name = 'Santa Reyes'
   AND permite_seguimiento_diario_parcial IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    DROP COLUMN IF EXISTS permite_seguimiento_diario_parcial;
");
        }
    }
}
