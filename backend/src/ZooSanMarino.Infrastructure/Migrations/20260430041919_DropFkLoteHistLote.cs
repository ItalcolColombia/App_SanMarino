using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropFkLoteHistLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La FK fk_lote_hist_lote fue creada fuera de EF (create_lote_registro_historico_unificado.sql).
            // Al insertar directamente vía EF, PostgreSQL exige FOR KEY SHARE sobre lote_ave_engorde para
            // validar la FK, pero el usuario de la app (repropesa01) no tiene REFERENCES sobre esa tabla.
            // Se elimina la FK; lote_ave_engorde_id queda como referencia blanda (la integridad se garantiza
            // en capa de aplicación). La columna y los datos existentes no se modifican.
            migrationBuilder.Sql(
                "ALTER TABLE public.lote_registro_historico_unificado DROP CONSTRAINT IF EXISTS fk_lote_hist_lote;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.lote_registro_historico_unificado
                    ADD CONSTRAINT fk_lote_hist_lote
                    FOREIGN KEY (lote_ave_engorde_id)
                    REFERENCES public.lote_ave_engorde (lote_ave_engorde_id)
                    ON DELETE SET NULL;");
        }
    }
}
