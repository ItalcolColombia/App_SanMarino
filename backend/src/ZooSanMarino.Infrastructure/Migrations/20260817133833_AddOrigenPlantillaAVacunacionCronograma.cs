using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Marca de origen en el cronograma del lote: de qué ítem de la plantilla de empresa salió cada
    /// fila y si la sigue gobernando el plan (W2.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Aditiva y neutra.</b> Las dos columnas nacen en <c>NULL</c> / <c>false</c>, o sea que todo
    /// lo que ya existe queda marcado como <i>cargado a mano</i> —que es exactamente lo que es— y el
    /// materializador no lo va a tocar nunca. Ningún camino existente cambia de comportamiento.
    /// </para>
    /// <para>
    /// El <c>Up()</c> va en SQL <b>idempotente</b> (regla de CLAUDE.md §🗄️): el deploy aplica las
    /// migraciones solas al arrancar, y una que falla a mitad deja el historial inconsistente.
    /// </para>
    /// </remarks>
    public partial class AddOrigenPlantillaAVacunacionCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.vacunacion_cronograma_item
    ADD COLUMN IF NOT EXISTS origen_plantilla_item_id integer NULL;");

            migrationBuilder.Sql(@"
ALTER TABLE public.vacunacion_cronograma_item
    ADD COLUMN IF NOT EXISTS generado_automatico boolean NOT NULL DEFAULT FALSE;");

            // SET NULL y no RESTRICT ni CASCADE: vacunacion_plan_plantilla_item cascadea desde su
            // plantilla, asi que con CASCADE borrar un plan se llevaria puesto el cronograma de lotes
            // reales (y por 1:1 su registro de aplicacion, o sea la prueba de que la vacuna se puso),
            // y con RESTRICT el borrado del plan fallaria. El item del lote es historia sanitaria:
            // sobrevive y solo pierde el vinculo con el plan del que nacio.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_vacunacion_cronograma_item_vacunacion_plan_plantilla_item_o'
          AND conrelid = 'public.vacunacion_cronograma_item'::regclass
    ) THEN
        ALTER TABLE public.vacunacion_cronograma_item
            ADD CONSTRAINT fk_vacunacion_cronograma_item_vacunacion_plan_plantilla_item_o
            FOREIGN KEY (origen_plantilla_item_id)
            REFERENCES public.vacunacion_plan_plantilla_item (id) ON DELETE SET NULL;
    END IF;
END $$;");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_vacunacion_cronograma_item_origen_plantilla
    ON public.vacunacion_cronograma_item (origen_plantilla_item_id);");

            // Idempotencia del materializador, garantizada por la BASE y no por el codigo: un mismo
            // item de plantilla no puede materializarse dos veces en el mismo lote.
            //
            // El COALESCE no es adorno. Los tres FK de linea son excluyentes (dos de los tres vienen
            // siempre en NULL) y en Postgres NULL no es igual a NULL, asi que sin envolverlos el
            // indice unico no bloquearia ni un duplicado. Mismo golpe ya documentado en el indice
            // unico de stock de inventario.
            //
            // Es parcial (WHERE origen_plantilla_item_id IS NOT NULL), y al crearse la columna esta
            // toda en NULL: cubre CERO filas, asi que no puede fallar sobre datos existentes.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ux_vci_origen_plantilla_item
    ON public.vacunacion_cronograma_item (
        COALESCE(lote_postura_levante_id, 0),
        COALESCE(lote_postura_produccion_id, 0),
        COALESCE(lote_ave_engorde_id, 0),
        origen_plantilla_item_id)
    WHERE origen_plantilla_item_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ux_vci_origen_plantilla_item;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_vacunacion_cronograma_item_origen_plantilla;");
            migrationBuilder.Sql(@"
ALTER TABLE public.vacunacion_cronograma_item
    DROP CONSTRAINT IF EXISTS fk_vacunacion_cronograma_item_vacunacion_plan_plantilla_item_o;");
            migrationBuilder.Sql(@"
ALTER TABLE public.vacunacion_cronograma_item
    DROP COLUMN IF EXISTS generado_automatico,
    DROP COLUMN IF EXISTS origen_plantilla_item_id;");
        }
    }
}
