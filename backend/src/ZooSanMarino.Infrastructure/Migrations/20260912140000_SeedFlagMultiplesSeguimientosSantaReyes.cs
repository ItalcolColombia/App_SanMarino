using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja encendido <c>companies.permite_multiples_seguimientos_diarios</c> para <b>Santa Reyes</b> en el
    /// deploy que lleva la fase de varios seguimientos diarios por día ya funcional (triggers por fila,
    /// alta/edición de producción, grilla por registro y reportes).
    ///
    /// <para>
    /// <b>Data-only.</b> No toca el esquema ni el ModelSnapshot (el Designer es el snapshot vigente). La
    /// columna la crea <c>20260905015025_AddFlagPermiteMultiplesSeguimientosDiarios</c>, que ya encendía el
    /// flag al crearla; esta migración lo <b>garantiza</b> después de toda la serie del 12-sep, por si el
    /// flag se apagó desde la pantalla de Empresas entre ambos deploys.
    /// </para>
    ///
    /// <para>
    /// Idempotente: busca la empresa por <c>name</c> (los ids difieren entre local y producción) y solo
    /// escribe si el valor es distinto, así no ensucia <c>updated_at</c> ni el histórico en cada arranque.
    /// Si la empresa no existe, no hace nada. <c>Down</c> no apaga el flag: apagarlo es una decisión de
    /// negocio que se toma desde la pantalla de Empresas.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/seguimiento_varios_por_dia_errores_reportes_plan.md</c> (§ Flag Santa Reyes).
    /// </summary>
    public partial class SeedFlagMultiplesSeguimientosSantaReyes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.companies
                   SET permite_multiples_seguimientos_diarios = true
                 WHERE name = 'Santa Reyes'
                   AND permite_multiples_seguimientos_diarios IS DISTINCT FROM true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío: apagar el flag se decide desde la pantalla de Empresas.
        }
    }
}
