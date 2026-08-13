using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Fase D del plan <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>: la
    /// línea de un gasto de inventario recuerda de qué <b>silo o bodega</b> salió.
    ///
    /// <para>
    /// Sin esta columna el módulo Gastos no se puede habilitar en una empresa que ubica el inventario
    /// por silo: el alta ya descuenta del silo indicado (fail-closed de la Fase B), pero la
    /// <b>anulación</b> repondría el insumo «a nivel granja» —una fila de stock que nadie descontó—
    /// y el saldo del silo quedaría corto para siempre, sin ningún error a la vista.
    /// </para>
    ///
    /// <para>
    /// Aditiva y NULLABLE: las líneas ya existentes (Sanmarino, Demo, Ecuador, Panamá) quedan en NULL,
    /// que es exactamente el «stock a nivel granja» con el que se registraron, así que ninguna
    /// devolución previa cambia de destino.
    /// </para>
    ///
    /// Idempotente (<c>IF NOT EXISTS</c> + <c>DO $$</c> para la FK). Escrita a mano.
    /// </summary>
    public partial class AddSiloEnGastoDetalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.inventario_gasto_detalle ADD COLUMN IF NOT EXISTS silo_id integer NULL;
");

            // Misma FK que el resto del inventario por silo: RESTRICT, para que borrar un silo con
            // historia falle en voz alta en vez de dejar líneas apuntando a la nada.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_inventario_gasto_detalle_silo') THEN
        ALTER TABLE public.inventario_gasto_detalle
            ADD CONSTRAINT fk_inventario_gasto_detalle_silo FOREIGN KEY (silo_id)
            REFERENCES public.farm_silos (id) ON DELETE RESTRICT;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.inventario_gasto_detalle DROP CONSTRAINT IF EXISTS fk_inventario_gasto_detalle_silo;
ALTER TABLE public.inventario_gasto_detalle DROP COLUMN IF EXISTS silo_id;
");
        }
    }
}
