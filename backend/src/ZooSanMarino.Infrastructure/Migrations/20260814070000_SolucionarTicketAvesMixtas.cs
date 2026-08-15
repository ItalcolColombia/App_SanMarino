using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000024</b> («Aves Mixtas no existen en reproductoras») en <c>SOLUCIONADO</c>
    /// con la descripción de la solución para el usuario. Sin correos.
    /// </summary>
    /// <remarks>
    /// Misma mecánica que <c>20260814060000_SolucionarTicketUnidadStock</c>: DATA-ONLY, idempotente
    /// (<c>WHERE estado NOT IN ('SOLUCIONADO','CERRADO')</c>), Designer clonado y ModelSnapshot
    /// intacto. El cierre definitivo lo confirma el solicitante desde la app.
    /// </remarks>
    public partial class SolucionarTicketAvesMixtas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Corregido y desplegado.

Se quitaron los campos de aves Mixtas de las pantallas de reproductoras:

1) Lote base: ya no aparece ""Cantidad mixtas"". Ademas ese campo era OBLIGATORIO, asi que hasta
   ahora no se podia guardar un lote base sin llenarlo con un 0. Tampoco aparece la columna
   ""Mixtas"" del listado ni la linea del detalle.
2) Lote Reproductora: ya no aparece ""Mixtas"" ni en el alta individual ni en el alta masiva por
   incubadora, y se sacaron las columnas ""Mixtas"" y ""Peso Mixto"" del listado, del cuadro de
   datos del lote y del detalle.
3) El mensaje de validacion ahora dice ""Debe asignar al menos 1 hembra o 1 macho"".

Las aves mixtas siguen existiendo donde si corresponden: pollo de engorde (seguimiento, ventas,
movimientos de aves y liquidacion). Ahi no se toco nada.

VERIFICACION: antes de sacarlo se reviso la base de produccion y el campo estaba en cero en el 100%
de los registros de reproductoras (lotes 0 de 22, lote base 0 de 30, levante 0 de 22, produccion
0 de 6), tal como usted lo indico. No se oculta ningun dato cargado.

NOTA TECNICA: el dato no se borro de la base. Si algun registro llegara a tener un valor, se
conserva al editarlo; solo dejo de pedirse y de mostrarse en reproductoras.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000024'
   AND estado NOT IN ('SOLUCIONADO', 'CERRADO');
");
        }

        /// <inheritdoc />
        /// <remarks>Devuelve el caso a <c>ABIERTO</c>, que es el estado del que salió.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'ABIERTO',
       fecha_solucion       = NULL,
       solucion_descripcion = NULL,
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000024'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Corregido y desplegado.%campos de aves Mixtas de las pantallas de reproductoras%';
");
        }
    }
}
