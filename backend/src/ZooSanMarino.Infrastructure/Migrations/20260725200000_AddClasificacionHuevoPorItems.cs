using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Santa Reyes — Fase 2: flag tipado por comportamiento
    /// <c>companies.clasificacion_huevo_por_items</c> (default <c>false</c> = todas las empresas
    /// actuales siguen clasificando por las 11 columnas fijas <c>huevo_limpio</c>…<c>huevo_otro</c>).
    /// <para>
    /// Con el flag en <c>true</c>, el seguimiento diario de producción clasifica los huevos POR ÍTEM
    /// del catálogo (<c>catalogo_items.item_type = 'huevo'</c>, categorías Primera/Pnc): el desglose
    /// se guarda en <c>seguimiento_diario_produccion.metadata → huevoItems</c>, <c>huevo_tot</c>
    /// recibe la suma (mantiene vivos espejo, trigger, saldos, indicadores y reportes) y
    /// <c>huevo_inc</c> + las 11 columnas quedan en 0.
    /// </para>
    /// Enciende el flag SOLO para la empresa <c>Santa Reyes</c> (sembrada por la migración
    /// <c>20260725190000_SeedEmpresaSantaReyes</c>, que corre ANTES que esta — de ahí el timestamp).
    /// Aditiva e <b>idempotente</b> (IF NOT EXISTS + UPDATE con guarda): re-ejecutable sin efectos.
    /// </summary>
    public partial class AddClasificacionHuevoPorItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    ADD COLUMN IF NOT EXISTS clasificacion_huevo_por_items boolean NOT NULL DEFAULT false;
");

            // Santa Reyes (postura comercial) es la única empresa que clasifica por ítems.
            // Lookup por nombre (nunca por id fijo) + guarda IS DISTINCT FROM → re-ejecutable.
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET clasificacion_huevo_por_items = true
 WHERE name = 'Santa Reyes'
   AND clasificacion_huevo_por_items IS DISTINCT FROM true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.companies
    DROP COLUMN IF EXISTS clasificacion_huevo_por_items;
");
        }
    }
}
