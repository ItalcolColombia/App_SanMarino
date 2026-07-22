using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Lote base engorde, fase 3 (Panamá): vigencia del catálogo.
    ///   * fecha_activacion (date): el lote base aparece en el selector de crear-lote solo
    ///     durante el AÑO de esta fecha (NULL = siempre vigente).
    ///   * activo (bool, default true): desactivación MANUAL; inactivo sale del selector.
    /// La gestión y el filtro del reporte siguen mostrando todos. Idempotente.
    /// </summary>
    public partial class AddLoteBaseEngordeActivacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.lote_base_engorde
    ADD COLUMN IF NOT EXISTS fecha_activacion date NULL;
ALTER TABLE public.lote_base_engorde
    ADD COLUMN IF NOT EXISTS activo boolean NOT NULL DEFAULT true;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.lote_base_engorde DROP COLUMN IF EXISTS fecha_activacion;
ALTER TABLE public.lote_base_engorde DROP COLUMN IF EXISTS activo;
");
        }
    }
}
