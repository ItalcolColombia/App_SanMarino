using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000020</b> («La información de levante incompleta en carga masiva lote
    /// S369») en <c>SOLUCIONADO</c>. No hubo cambio de código: la respuesta es operativa. Sin
    /// correos.
    /// </summary>
    /// <remarks>
    /// <b>Lo verificado en la base de producción:</b> S369A (<c>lote_postura_levante</c> 34, lote
    /// 142) tiene <b>168 registros de levante</b> — 29/08/2025 a 12/02/2026, 24 semanas exactas —,
    /// <c>estado_cierre = 'Abierto'</c> y no existe ningún <c>lote_postura_produccion</c> asociado.
    /// S369B (35) está igual, también con 168.
    ///
    /// <b>Lo verificado en el código:</b> <c>CerrarLoteYCrearProduccionAsync</c> valida únicamente
    /// usuario, huevos ≥ 0, que el lote no esté ya cerrado y que no exista un lote de producción.
    /// <b>No hay ninguna validación por semana ni por edad</b>, y el botón «Cerrar lote» tampoco
    /// tiene condición de edad. Donde sí aparece la semana 25 es en la <b>liquidación</b>
    /// (<c>LiquidacionCierreLoteLevanteService</c> recorta a <c>encaset + 175 días</c> y busca la
    /// fila de guía genética de la semana 25): con 168 días el lote queda 7 días corto de ese corte.
    ///
    /// DATA-ONLY, idempotente, Designer clonado y ModelSnapshot intacto.
    /// </remarks>
    public partial class SolucionarTicketS369Semana24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Revisado. No es una falla del sistema: faltan dias en el archivo.

QUE ENCONTRAMOS EN LA BASE. El lote S369A tiene 168 registros diarios de levante, del 29/08/2025 al
12/02/2026. Son 24 semanas EXACTAS (24 x 7 = 168). S369B esta igual, tambien con 168. Ninguno de los
dos tiene todavia lote de produccion creado.

POR QUE LLEGA A LA SEMANA 24. La carga masiva cargo exactamente los dias que traia el archivo. El
informe tecnico del lote cubre hasta el dia 175 (25 semanas); en la plantilla que se importo quedaron
168, o sea faltan los ULTIMOS 7 DIAS de levante. La plantilla no corta ni descarta por semana: carga
lo que se le pone.

QUE HAY QUE HACER. Cargar esos 7 dias faltantes (del dia 169 al 175) con la misma plantilla de
levante. La importacion es idempotente por lote y fecha: las fechas que ya estan cargadas se omiten
solas, asi que se puede volver a subir el archivo completo sin duplicar nada. Despues de eso el lote
queda con las 25 semanas y se cierra normal.

DATO IMPORTANTE: el cierre del lote NO esta bloqueado por la semana. Revisamos el codigo y la accion
""Cerrar lote"" solo exige que el lote no este ya cerrado y que no exista un lote de produccion
asociado; no valida edad ni semanas. Donde si se usa la semana 25 es en la LIQUIDACION, que compara
contra la fila de la semana 25 de la guia genetica: con 168 dias el lote queda 7 dias corto de ese
punto de comparacion, y por eso conviene completar los dias ANTES de cerrar y liquidar.

SI AL INTENTAR CERRAR LES APARECE UN MENSAJE DE ERROR, o el lote no les aparece en la lista de
Levante para poder cerrarlo, mandenos la captura con el texto exacto: con eso lo atacamos puntual.
Con la informacion que tenemos hoy, el lote es cerrable.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000020'
   AND estado NOT IN ('SOLUCIONADO', 'CERRADO');
");
        }

        /// <inheritdoc />
        /// <remarks>Vuelve al estado del que salió: <c>SUSPENDIDO</c>.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SUSPENDIDO',
       fecha_solucion       = NULL,
       solucion_descripcion = NULL,
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000020'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Revisado. No es una falla del sistema%';
");
        }
    }
}
