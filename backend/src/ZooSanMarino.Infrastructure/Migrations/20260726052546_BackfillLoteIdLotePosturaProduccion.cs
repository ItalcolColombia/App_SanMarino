using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLoteIdLotePosturaProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill DATA-ONLY (sin cambios de esquema): repara filas de lote_postura_produccion
            // creadas ANTES del fix de herencia en el cierre de levante (CrearLoteProduccion no
            // copiaba LoteId/LotePadreId), que quedaron con lote_id NULL aunque su levante de
            // origen SÍ lo tiene. Sin lote_id el seguimiento diario es imposible de guardar
            // (seguimiento_diario_produccion.lote_id es NOT NULL) → 400 "El lote postura
            // producción no tiene LoteId asociado". Ej. en Demo: LPP #8 → 119, #9 → 124.
            // Idempotente: solo toca filas rotas (lote_id NULL o <= 0) cuyo levante tenga lote
            // válido; re-ejecutar no altera filas ya sanas. No filtra deleted_at a propósito
            // (reparar borrados es inofensivo y deja el dato consistente si se restauran).
            migrationBuilder.Sql(@"
UPDATE public.lote_postura_produccion p
SET lote_id       = lev.lote_id,
    lote_padre_id = COALESCE(p.lote_padre_id, lev.lote_padre_id)
FROM public.lote_postura_levante lev
WHERE p.lote_postura_levante_id = lev.lote_postura_levante_id
  AND lev.company_id = p.company_id
  AND (p.lote_id IS NULL OR p.lote_id <= 0)
  AND lev.lote_id IS NOT NULL AND lev.lote_id > 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: backfill de datos (reparación), no reversible ni necesario revertirlo —
            // volver a dejar lote_id en NULL solo reintroduciría el bug del 400.
        }
    }
}
