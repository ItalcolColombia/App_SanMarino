using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only, sin cambio de esquema (las 2 columnas ya existen desde F0.1,
    /// <c>20260820083544_AddFlagsSantaReyesCicloVidaYHuevo</c>). Cierra un gap real: F4
    /// (`107cf3c`) y F5.1+F5.2 (`b93a053`) construyeron y probaron la UI que oculta Machos en
    /// producción/levante detrás de <c>consumo_alimento_solo_hembras</c> y
    /// <c>oculta_machos_en_postura</c>, pero ninguno de los dos commits encendió el flag para
    /// Santa Reyes — quedó el mismo "toggle sin efecto" que F0.1 advertía evitar (nace apagado
    /// a propósito hasta el commit que construye lo que lo consume; ese commit ya pasó y el
    /// interruptor se quedó apagado). Verificado por consulta directa a <c>companies</c> en la
    /// BD local: ambas columnas seguían en <c>false</c> para Santa Reyes.
    /// Idempotente (UPDATE con guarda <c>IS DISTINCT FROM</c>): re-ejecutable sin romper.
    /// </summary>
    public partial class EnciendeConsumoSoloHembrasYOcultaMachosSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET consumo_alimento_solo_hembras = true
 WHERE name = 'Santa Reyes'
   AND consumo_alimento_solo_hembras IS DISTINCT FROM true;
");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET oculta_machos_en_postura = true
 WHERE name = 'Santa Reyes'
   AND oculta_machos_en_postura IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET consumo_alimento_solo_hembras = false
 WHERE name = 'Santa Reyes';
");

            migrationBuilder.Sql(@"
UPDATE public.companies
   SET oculta_machos_en_postura = false
 WHERE name = 'Santa Reyes';
");
        }
    }
}
