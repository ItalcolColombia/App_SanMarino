using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Índices para 14 claves foráneas que no tenían ninguno con la columna como PRIMERA del índice.
    ///
    /// Medido contra la copia local: `inventario_gasto` llevaba 8.164 seq scans contra 45 index scans
    /// e `inventario_gasto_detalle` 11.654 contra 37. En local la tabla entra en una página y el
    /// planner elige seq scan igual — la ganancia se confirma con el volumen de producción, no acá.
    /// Lo que sí es cierto en cualquier tamaño: sin índice sobre la FK, cada DELETE/UPDATE en la
    /// tabla padre recorre entera la tabla hija para chequear la integridad.
    ///
    /// Las 7 columnas se usan de verdad en el código (entre 11 y 92 referencias cada una), incluida
    /// navegación que EF traduce a JOIN — no se indexa nada "por si acaso".
    ///
    /// Convención copiada de la propia tabla: las columnas NULL llevan índice PARCIAL
    /// (`WHERE ... IS NOT NULL`), igual que los `ix_movimiento_pollo_engorde_lae_origen` y
    /// `_lrae_origen` que ya existían. Justamente ahí estaba la asimetría: alguien indexó los
    /// ORIGEN y dejó los DESTINO sin índice.
    ///
    /// Nota de despliegue: `CREATE INDEX` (sin CONCURRENTLY) toma un lock que bloquea ESCRITURAS
    /// sobre la tabla mientras construye. Con los tamaños actuales son segundos. No se usa
    /// CONCURRENTLY a propósito: no puede correr dentro de la transacción de la migración y, si
    /// falla, deja el índice INVALID y un reintento con IF NOT EXISTS lo saltearía para siempre.
    /// </summary>
    public partial class IndicesClavesForaneasInventarioYEngorde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- inventario_gestion_movimiento (13.6k filas en local; la más grande de las tocadas)
CREATE INDEX IF NOT EXISTS ix_igm_item_inventario_id
    ON public.inventario_gestion_movimiento (item_inventario_id);
CREATE INDEX IF NOT EXISTS ix_igm_silo_id
    ON public.inventario_gestion_movimiento (silo_id) WHERE silo_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_igm_from_silo_id
    ON public.inventario_gestion_movimiento (from_silo_id) WHERE from_silo_id IS NOT NULL;

-- movimiento_pollo_engorde: completa la simetría origen/destino que ya existía
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_granja_origen
    ON public.movimiento_pollo_engorde (granja_origen_id) WHERE granja_origen_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_granja_destino
    ON public.movimiento_pollo_engorde (granja_destino_id) WHERE granja_destino_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_lae_destino
    ON public.movimiento_pollo_engorde (lote_ave_engorde_destino_id)
    WHERE lote_ave_engorde_destino_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_movimiento_pollo_engorde_lrae_destino
    ON public.movimiento_pollo_engorde (lote_reproductora_ave_engorde_destino_id)
    WHERE lote_reproductora_ave_engorde_destino_id IS NOT NULL;

-- inventario_gasto: farm_id y pais_id están en un compuesto pero NO como primera columna
CREATE INDEX IF NOT EXISTS ix_inventario_gasto_farm_id
    ON public.inventario_gasto (farm_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gasto_pais_id
    ON public.inventario_gasto (pais_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gasto_lote_ave_engorde_id
    ON public.inventario_gasto (lote_ave_engorde_id) WHERE lote_ave_engorde_id IS NOT NULL;

-- inventario_gasto_detalle
CREATE INDEX IF NOT EXISTS ix_inventario_gasto_detalle_item_inventario_id
    ON public.inventario_gasto_detalle (item_inventario_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gasto_detalle_silo_id
    ON public.inventario_gasto_detalle (silo_id) WHERE silo_id IS NOT NULL;

-- inventario_gestion_stock
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_item_inventario_id
    ON public.inventario_gestion_stock (item_inventario_id);
CREATE INDEX IF NOT EXISTS ix_inventario_gestion_stock_silo_id
    ON public.inventario_gestion_stock (silo_id) WHERE silo_id IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS public.ix_igm_item_inventario_id;
DROP INDEX IF EXISTS public.ix_igm_silo_id;
DROP INDEX IF EXISTS public.ix_igm_from_silo_id;
DROP INDEX IF EXISTS public.ix_movimiento_pollo_engorde_granja_origen;
DROP INDEX IF EXISTS public.ix_movimiento_pollo_engorde_granja_destino;
DROP INDEX IF EXISTS public.ix_movimiento_pollo_engorde_lae_destino;
DROP INDEX IF EXISTS public.ix_movimiento_pollo_engorde_lrae_destino;
DROP INDEX IF EXISTS public.ix_inventario_gasto_farm_id;
DROP INDEX IF EXISTS public.ix_inventario_gasto_pais_id;
DROP INDEX IF EXISTS public.ix_inventario_gasto_lote_ave_engorde_id;
DROP INDEX IF EXISTS public.ix_inventario_gasto_detalle_item_inventario_id;
DROP INDEX IF EXISTS public.ix_inventario_gasto_detalle_silo_id;
DROP INDEX IF EXISTS public.ix_inventario_gestion_stock_item_inventario_id;
DROP INDEX IF EXISTS public.ix_inventario_gestion_stock_silo_id;
");
        }
    }
}
