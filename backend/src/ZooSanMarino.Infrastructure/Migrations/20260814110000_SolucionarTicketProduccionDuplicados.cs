using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000023</b> («Seguimiento producción. parámetros duplicados y sobrantes.
    /// cálculos indicadores») en <c>SOLUCIONADO</c>, con la respuesta a los tres puntos del
    /// usuario. Sin correos.
    /// </summary>
    /// <remarks>DATA-ONLY, idempotente, Designer clonado y ModelSnapshot intacto.</remarks>
    public partial class SolucionarTicketProduccionDuplicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Corregido y desplegado. Los tres puntos:

1) CONSUMOS DUPLICADOS. Tenia razon: la tabla mostraba ""Cons. H (kg)"" / ""Cons. M (kg)"" y otra
   vez ""Cons. orig H"" / ""Cons. orig M"". Esas dos ultimas eran el consumo tal como se hubiera
   tecleado en otra unidad (por ejemplo gramos) y solo servian si el registro venia en una unidad
   distinta de kg. Al revisar la base, esa informacion NO existe en ninguna de las 604 filas de
   produccion, asi que la columna caia siempre al mismo valor en kg: el numero se repetia. Se
   quitaron las cuatro columnas de la tabla y del Excel.

2) UNIFORMIDAD Y CV EN PRODUCCION. Tambien correcto: son parametros de LEVANTE. Se sacaron de la
   tabla de Seguimiento, de la tabla de Indicadores y de los dos Excel. Verificado en la base:
   uniformidad esta vacia en las 605 filas de produccion y coeficiente de variacion tiene un unico
   registro con 0,02 (una prueba). La guia genetica ni siquiera define uniformidad para las edades
   de produccion. El dato no se borro de la base, solo dejo de mostrarse.

3) DIFERENCIA DE MORTALIDAD VS GUIA. Confirmado el error. La columna calculaba el porcentaje
   RELATIVO ((real - guia) / guia x 100) sobre dos numeros que YA son porcentajes, y sobre valores
   de decimas eso explota: en la semana 26 mostraba -80,05 % (0,07 % real contra 0,33 % de guia) y
   +2.212,10 % en machos (0,26 % contra 0,01 %). Ahora es la DIFERENCIA DIRECTA en puntos
   porcentuales: -0,26 pp y +0,25 pp respectivamente. La columna quedo rotulada ""Dif Mort H (pp)""
   y ""Dif Mort M (pp)"" para que no se confunda con un porcentaje.

   Se le quito el semaforo de colores a esas dos columnas: los umbrales de 5 % y 15 % que pinta el
   resto de la tabla son de porcentaje relativo y no significan lo mismo sobre puntos porcentuales.
   Si nos indica que diferencia en pp considera aceptable para mortalidad, lo volvemos a pintar.

   Las demas diferencias (consumo, peso, huevos) siguen en porcentaje relativo a proposito: ahi la
   guia y el real son kilos, gramos o unidades, no porcentajes, y el porcentaje es la lectura
   correcta. Si quiere que ""% produccion"", ""uniformidad"" o ""% retiro"" pasen tambien a
   diferencia directa, avisenos: son del mismo tipo y es un cambio de una linea cada uno.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000023'
   AND estado NOT IN ('SOLUCIONADO', 'CERRADO');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'ABIERTO',
       fecha_solucion       = NULL,
       solucion_descripcion = NULL,
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000023'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Corregido y desplegado. Los tres puntos:%';
");
        }
    }
}
