using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignLotesFkAndBackfillTrasladoHuevosLoteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotente: bases ya alineadas manualmente no fallan al aplicar migraciones desde .NET.
            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_lotes_lote_postura_base_id ON public.lotes (lote_postura_base_id);
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'fk_lotes_lote_postura_bases_lote_postura_base_id'
  ) THEN
    ALTER TABLE public.lotes
      ADD CONSTRAINT fk_lotes_lote_postura_bases_lote_postura_base_id
      FOREIGN KEY (lote_postura_base_id)
      REFERENCES public.lote_postura_base (lote_postura_base_id)
      ON DELETE RESTRICT;
  END IF;
END $$;
");

            // Alinear lote_id en traslado_huevos con lotes.lote_id cuando el origen es LPP (antes se guardaba "LPP-{id}").
            migrationBuilder.Sql(@"
UPDATE traslado_huevos th
SET lote_id = lpp.lote_id::text
FROM lote_postura_produccion lpp
WHERE th.lote_postura_produccion_id = lpp.lote_postura_produccion_id
  AND lpp.lote_id IS NOT NULL
  AND th.lote_id IS DISTINCT FROM lpp.lote_id::text;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.lotes DROP CONSTRAINT IF EXISTS fk_lotes_lote_postura_bases_lote_postura_base_id;
DROP INDEX IF EXISTS public.ix_lotes_lote_postura_base_id;
");
        }
    }
}
