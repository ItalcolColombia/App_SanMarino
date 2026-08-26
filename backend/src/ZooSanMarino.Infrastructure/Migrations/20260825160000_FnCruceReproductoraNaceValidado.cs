using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Los días 1-7 que el cruce de reproductora genera en pollo engorde pasan a nacer
    /// <b>ya validados</b>, y se corrigen los que nacieron sin validar.
    ///
    /// <para>
    /// <b>El defecto.</b> <c>fn_cruce_reproductora_a_engorde</c> inserta esos días sin nombrar la
    /// columna <c>validado</c>, que tiene <c>DEFAULT false</c> — mientras el C# documenta lo
    /// contrario, textual (<c>SeguimientoDiarioAvesEngorde.Validado</c>): <i>«Los registros con
    /// OrigenCruce nacen validados: los escribe el trigger de BD desde reproductora, ya confirmados
    /// en su origen, y nadie los edita a mano»</i>. El backfill de la migración que estrenó la doble
    /// validación arregló el pasado; nadie arregló el futuro.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué explota solo al confirmar tarde.</b> El plazo de la doble validación es de
    /// <b>1 día contado desde la FECHA del seguimiento</b>, no desde cuándo se creó la fila. Si la
    /// reproductora se confirma a tiempo, el cruce nace con fecha de hoy y hay ventana para validar.
    /// Si se confirma tarde, el cruce inserta con fechas de hace días: los registros <b>nacen
    /// vencidos</b>, <c>BloqueaAltaPorVencidos</c> impide crear días nuevos en ese lote, y nadie
    /// puede destrabarlo porque los registros <c>origen_cruce</c> son de solo lectura en la UI.
    /// </para>
    ///
    /// <para>
    /// <b>Medido en la copia de producción (25-ago-2026):</b> 28 registros así, en 4 lotes de
    /// DAYLAND (215, 216, 224, 225) — dos de ellos creados ese mismo día. El caso que lo reportó es
    /// el lote 215 (galpón «6»), cuya reproductora confirmó sus 7 días con <b>5 a 10 días de
    /// atraso</b>.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué <c>validado = true</c> y no «excluir el cruce del conteo de vencidos»:</b> no hay
    /// nada que validar —el cruce no crea reservas de alimento (0 filas verificadas), así que
    /// validar sería un no-op—, la confirmación humana ya ocurrió en reproductora, y el descuento de
    /// aves (<c>RetiroAvesEngordeAplicador.SincronizarCruceAsync</c>) nunca miró esta columna:
    /// trabaja sobre <c>OrigenCruce</c> y el histórico. Dejarlos pendientes es un estado sin salida.
    /// </para>
    ///
    /// <para>
    /// <b>Alcance:</b> solo <c>ItalcolPanama</c> tiene <c>requiere_validacion_seguimiento_diario</c>;
    /// en las otras cuatro empresas nadie lee <c>validado</c>. El backfill igual las alcanza para
    /// dejar el dato coherente si algún día lo encienden.
    /// </para>
    ///
    /// <para>
    /// Verificado en transacción revertida: forzando una regeneración real del trigger, los 7 días
    /// del lote 215 nacen <c>validado = true</c> / <c>validado_por = SYSTEM_CRUCE</c> y el lote queda
    /// con <b>0 vencidos</b>. Los 4 lotes se destraban; el lote 177 sigue bloqueado <b>a propósito</b>
    /// — su registro vencido es normal, no de cruce, y se valida desde la pantalla.
    /// </para>
    ///
    /// Plan: <c>fase_de_desarrollo/cruce_reproductora_nace_sin_validar_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_cruce_reproductora_a_engorde.sql</c> — esta migración es el
    /// <b>vehículo</b>: nada de <c>backend/sql/</c> llega a producción por sí solo.
    /// Idempotente: <c>CREATE OR REPLACE</c> + un <c>UPDATE</c> acotado que no reprocesa nada.
    /// Sin cambios de modelo (ModelSnapshot intacto).
    /// </summary>
    public partial class FnCruceReproductoraNaceValidado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) La función, para que los días nuevos nazcan validados.
            migrationBuilder.Sql(FnCruceReproductoraAEngordeValidado);

            // 2) Los que ya nacieron sin validar. Acotado a origen_cruce: un registro normal sin
            //    validar es trabajo pendiente del operario y NO se toca — se valida desde la
            //    pantalla, que para eso es editable.
            migrationBuilder.Sql(BackfillCruceSinValidar);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El backfill NO se revierte: devolver esos registros a "pendiente" volvería a trabar los
            // lotes, que es exactamente el defecto. La función anterior tampoco se restituye por lo
            // mismo — su version vieja es la que rompe. Un Down que reintroduce el bug no es un Down.
            migrationBuilder.Sql(
                "-- Sin reversion deliberada: ver el comentario del Down en la migracion.");
        }

        /// <summary>
        /// Los registros que ya nacieron sin validar.
        ///
        /// <para>
        /// <b><c>validado_at</c> se deja en NULL a propósito.</b> Es una columna de auditoría: poner
        /// <c>now()</c> fabricaría el instante de una validación que nunca ocurrió. Es el mismo
        /// criterio del backfill que estrenó la doble validación
        /// (<c>20260815071444</c>), que hizo <c>SET validado = true</c> a secas — por eso los 273
        /// registros de cruce ya corregidos tienen las dos columnas en NULL.
        /// </para>
        ///
        /// <para>
        /// <c>validado_por</c> sí se escribe, con un literal <b>distinto</b> del que usa la función
        /// (<c>SYSTEM_CRUCE</c>): así una auditoría puede separar «esta fila la arregló la migración
        /// del 25-ago» de «esta nació bien». Sin eso, las dos son indistinguibles.
        /// </para>
        ///
        /// <para>
        /// Acotado a <c>origen_cruce</c>: un registro normal sin validar es trabajo pendiente del
        /// operario y NO se toca — se valida desde la pantalla, que para eso es editable. Hoy hay dos
        /// así en Panamá (lotes 177 y 180) y quedan fuera deliberadamente.
        /// </para>
        /// </summary>
        private const string BackfillCruceSinValidar = """
UPDATE seguimiento_diario_aves_engorde
   SET validado     = true,
       validado_por = 'SYSTEM_CRUCE_BACKFILL'
 WHERE origen_cruce
   AND NOT validado;
""";
    }
}
