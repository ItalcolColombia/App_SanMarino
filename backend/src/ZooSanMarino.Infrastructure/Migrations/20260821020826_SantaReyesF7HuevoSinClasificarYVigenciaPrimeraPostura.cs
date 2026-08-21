using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only, sin cambio de esquema (§8 de <c>santa_reyes_requerimientos_italapp_plan.md</c>, F7):
    /// (1) renombra los 6 ítems «Primera» de Santa Reyes que faltaban el prefijo «SIN CLASIFICAR»
    /// (solo Azur ya lo traía); (2) tagea `metadata.primeraPostura = true` en los 3 ítems de primera
    /// postura que ya existen (Rojo/Blanco/Criollo); (3) pone
    /// <c>Company.HuevoPrimeraPosturaHastaSemana = 22</c> para Santa Reyes (columna existente desde
    /// F0.1, sin consumidor hasta este commit). Idempotente: todo UPDATE trae guarda
    /// <c>IS DISTINCT FROM</c> / chequeo de la clave jsonb.
    /// </summary>
    public partial class SantaReyesF7HuevoSinClasificarYVigenciaPrimeraPostura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Rename de los 6 ítems "Primera" al patrón "HUEVO SIN CLASIFICAR <RAZA>" (Azur ya lo tenía).
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR ROJO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '528'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR ROJO';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR BLANCO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '2520'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR BLANCO';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR CRIOLLO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '2121'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR CRIOLLO';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR GALLINA FELIZ'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '530'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR GALLINA FELIZ';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR BONEGG'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '531'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR BONEGG';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO SIN CLASIFICAR LIBRE DE JAULA CERTIFICADO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '552'
   AND nombre IS DISTINCT FROM 'HUEVO SIN CLASIFICAR LIBRE DE JAULA CERTIFICADO';
");

            // 2) Tag metadata.primeraPostura = true en los 3 ítems de primera postura ya existentes.
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET metadata = metadata || '{""primeraPostura"": true}'::jsonb
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo IN ('2756', '2776', '5389')
   AND (metadata->>'primeraPostura') IS DISTINCT FROM 'true';
");

            // 3) Vigencia: última semana de vida con el ítem disponible.
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET huevo_primera_postura_hasta_semana = 22
 WHERE name = 'Santa Reyes'
   AND huevo_primera_postura_hasta_semana IS DISTINCT FROM 22;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET huevo_primera_postura_hasta_semana = NULL
 WHERE name = 'Santa Reyes';
");

            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET metadata = metadata - 'primeraPostura'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo IN ('2756', '2776', '5389');
");

            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO ROJO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '528';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO BLANCO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '2520';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO CRIOLLO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '2121';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO GALLINA FELIZ'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '530';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO BONEGG'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '531';
");
            migrationBuilder.Sql(@"
UPDATE public.catalogo_items
   SET nombre = 'HUEVO LIBRE DE JAULA CERTIFICADO'
 WHERE company_id = (SELECT id FROM public.companies WHERE name = 'Santa Reyes')
   AND codigo = '552';
");
        }
    }
}
