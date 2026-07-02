using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Retira la entidad SeguimientoDiarioAvesEngordePanama del modelo EF.
    /// El fork aves-engorde-panama (front + controller + service) se eliminó como
    /// código muerto no lanzado: su menú nunca existió en BD y Panamá usa el módulo
    /// compartido /daily-log/aves-engorde.
    /// La tabla FÍSICA seguimiento_diario_aves_engorde_panama NO se toca aquí:
    /// queda documentada como candidata a DROP en
    /// backend/sql/propuesta_drop_tablas_sin_uso.sql (ejecutar solo con OK + backup).
    /// </summary>
    public partial class RemoveSeguimientoDiarioAvesEngordePanamaFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío: solo actualiza el snapshot del modelo.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío.
        }
    }
}
