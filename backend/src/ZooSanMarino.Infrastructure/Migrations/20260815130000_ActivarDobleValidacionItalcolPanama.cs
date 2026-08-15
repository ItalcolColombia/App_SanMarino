using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Enciende la <b>doble validación del seguimiento diario</b> para <c>ItalcolPanama</c>. Es la
    /// única empresa que la activa; las demás siguen con el comportamiento de siempre.
    /// </summary>
    /// <remarks>
    /// <b>Qué cambia para Panamá el día que esto se despliega.</b> Guardar un seguimiento diario deja
    /// de descontar: el alimento y las aves quedan <b>separados</b> (reservados) y el descuento se
    /// aplica recién al <b>validar</b> el registro. Mientras no se valide se puede editar y eliminar
    /// sin devoluciones. Un registro que pasa su plazo de un día queda <i>En retraso</i> y, mientras
    /// no se valide, <b>el lote no acepta días nuevos</b>.
    ///
    /// <para>
    /// Los registros anteriores no se tocan: el backfill de
    /// <c>20260815071444_AddValidacionYSeparacionSeguimientosDiarios</c> los dejó validados porque ya
    /// habían descontado. Esto aplica solo a lo que se capture desde el deploy.
    /// </para>
    ///
    /// <para>
    /// <b>La empresa se localiza por NOMBRE normalizado</b> (minúsculas y sin espacios), nunca por id:
    /// los ids difieren entre local y producción, y el nombre podría estar escrito «Italcol Panama».
    /// Si no existe en el entorno, no hace nada — un seed no puede tumbar el arranque de la app.
    /// </para>
    ///
    /// <para>
    /// <b>Se enciende UNA vez.</b> El <c>UPDATE</c> exige que el flag esté hoy en <c>false</c>, así que
    /// si mañana alguien lo apaga desde la pantalla de Empresas —que es de donde se administra— esta
    /// migración no se lo vuelve a prender. Correrla dos veces no cambia una sola fila la segunda.
    /// </para>
    /// </remarks>
    public partial class ActivarDobleValidacionItalcolPanama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_id   integer;
    v_name text;
BEGIN
    -- Por nombre normalizado: los ids difieren local↔prod y el nombre puede llevar espacios.
    SELECT id, name INTO v_id, v_name
      FROM public.companies
     WHERE replace(lower(name), ' ', '') = 'italcolpanama'
     ORDER BY id
     LIMIT 1;

    IF v_id IS NULL THEN
        RAISE NOTICE 'Doble validacion: no existe ItalcolPanama en este entorno; no se activa nada.';
        RETURN;
    END IF;

    -- Solo si esta apagado: si alguien ya lo apago a mano desde Empresas, se respeta esa decision.
    UPDATE public.companies
       SET requiere_validacion_seguimiento_diario = true
     WHERE id = v_id
       AND requiere_validacion_seguimiento_diario = false;

    IF FOUND THEN
        RAISE NOTICE 'Doble validacion ACTIVADA para % (id %).', v_name, v_id;
    ELSE
        RAISE NOTICE 'Doble validacion: % (id %) ya la tenia activa; sin cambios.', v_name, v_id;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Apaga el flag y deja a Panamá como estaba. Las reservas activas que hubiera quedan en la
        /// tabla sin efecto: con el flag apagado nadie las lee, y el descuento vuelve a ocurrir al
        /// guardar. Si se revierte con registros pendientes, esos quedan SIN descontar — hay que
        /// validarlos antes de revertir, o corregirlos a mano.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE public.companies
   SET requiere_validacion_seguimiento_diario = false
 WHERE replace(lower(name), ' ', '') = 'italcolpanama';
");
        }
    }
}
