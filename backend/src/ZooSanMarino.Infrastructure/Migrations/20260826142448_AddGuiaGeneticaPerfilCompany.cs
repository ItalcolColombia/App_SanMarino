using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Perfil tipado <c>companies.guia_genetica_perfil</c> (F1 de
    /// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c>): qué MODELO de guía genética usa
    /// cada empresa — <c>'sanmarino'</c> (tabla ancha compartida <c>guia_genetica_sanmarino_colombia</c>,
    /// default neutro) o <c>'reducida'</c> (tabla plana de 3 métricas <c>guia_genetica_santa_reyes</c>).
    ///
    /// <para>
    /// <b>La señal es por COMPORTAMIENTO, no por empresa</b> (CLAUDE.md §🏢): el backfill se deriva de
    /// DATOS — <c>EXISTS</c> sobre la tabla reducida—, nunca de <c>name = 'Santa Reyes'</c>. Con eso, la
    /// empresa #4 que mañana quiera el modelo plano se da de alta cambiando un dato, sin desplegar código.
    /// </para>
    ///
    /// <para>
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> + <c>UPDATE</c> con <c>IS DISTINCT FROM</c> (no
    /// ensucia <c>updated_at</c> de las filas que ya están bien) ⇒ re-ejecutable sin romper.
    /// </para>
    /// </summary>
    public partial class AddGuiaGeneticaPerfilCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE companies ADD COLUMN IF NOT EXISTS guia_genetica_perfil varchar(16) NOT NULL DEFAULT 'sanmarino';");

            // Backfill DERIVADO DE DATOS: usa el perfil reducido toda empresa que YA tenga filas en la
            // tabla plana. Nada de nombres ni de países acá.
            //
            // El guard `to_regclass` no es paranoia decorativa: el historial de este repo ya tuvo
            // migraciones marcadas como aplicadas sin haberse ejecutado, y una migración que revienta
            // al arrancar mata la tarea ECS con SIGSEGV antes del primer log. Si la tabla reducida no
            // existiera en ese entorno, no hay nada que backfillear y el default neutro ya es correcto.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.guia_genetica_santa_reyes') IS NOT NULL THEN
        UPDATE public.companies c
           SET guia_genetica_perfil = 'reducida'
         WHERE c.guia_genetica_perfil IS DISTINCT FROM 'reducida'
           AND EXISTS (
                 SELECT 1
                   FROM public.guia_genetica_santa_reyes g
                  WHERE g.company_id = c.id
               );
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE companies DROP COLUMN IF EXISTS guia_genetica_perfil;");
        }
    }
}
