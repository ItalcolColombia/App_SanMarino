using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000019</b> («UNIDAD DE MEDIDA EN STOCK DE INVENTARIO») en
    /// <c>SOLUCIONADO</c>, con la descripción de la solución escrita para el usuario que lo
    /// reportó. El despliegue que trae el arreglo es el mismo que marca el caso.
    /// </summary>
    /// <remarks>
    /// Va DESPUÉS de <c>20260814050000_AlinearUnidadInventarioConCatalogo</c> a propósito: el caso
    /// no se puede dar por resuelto antes de que la migración que limpia los datos haya corrido.
    ///
    /// <b>No manda ningún correo:</b> es SQL, no pasa por el servicio de notificación;
    /// <c>notificado_correo</c> y <c>correo_notificado_a</c> quedan como estaban.
    ///
    /// <b>Queda en SOLUCIONADO, no en CERRADO:</b> el cierre lo confirma el solicitante desde la
    /// app cuando verifica el resultado, que es el flujo del módulo
    /// (<c>SOLUCIONADO → confirmación → CERRADO</c>).
    ///
    /// <b>Idempotencia:</b> el <c>WHERE</c> exige que el caso no esté ya solucionado o cerrado ⇒ la
    /// segunda pasada no afecta ninguna fila y no pisa una solución escrita a mano después.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class SolucionarTicketUnidadStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Corregido y desplegado.

QUE PASABA: la pantalla de Stock no mostraba la unidad del item, mostraba una unidad propia que se
guardaba en el registro de stock cuando ese registro nacia. Como esa copia arrancaba en ""kg"" y
despues nadie la volvia a sincronizar con el catalogo, un producto creado en litros (AV0373
GLIFOSATO 1LT, AV0374 AMINAPOT 1LT) seguia mostrandose en kg para siempre. Eran 145 de 569
registros de stock en esta situacion.

QUE SE HIZO:
1) La unidad de medida ahora la manda SIEMPRE el catalogo del item (Configuracion > Items de
   inventario). Ingresos, traslados, recepciones, consumos y ajustes graban esa unidad y la
   pantalla de Stock la muestra. Ya no hay dos versiones de la misma unidad.
2) Se corrigieron los 145 registros de stock que estaban desalineados, mas sus movimientos y el
   historico. Es solo la etiqueta: NINGUNA cantidad se modifico ni se convirtio.
3) Se subieron al catalogo las unidades que ustedes ya habian corregido a mano sobre el stock, para
   no perder ese trabajo: VETRIBAC D SOLUCION, LARVIGEN, ANTHIUM DIOXCIDE, CATALIZADOR,
   EXPECTORANLIPTUS y NEUTRALIZANTE quedan en litros; Q-NORFLOXAN 100ML en ml; PASTILLAS DE CLORO
   en und; la vacuna NEWCASTLE LASOTA en dosis; y el DIESEL en gal.
4) En el catalogo de items se agregaron las unidades ""dosis"" y ""gal"", que antes no se podian
   elegir.
5) El campo Unidad del boton Editar del stock quedo de SOLO LECTURA. Era texto libre y se venia
   usando para tapar el kg que mostraba la pantalla; por eso convivian ""LT"", ""UND"", ""GALONES"" y
   ""DOSIS"" con las unidades del catalogo.

COMO SE USA DE AHORA EN MAS: si a un item le corresponde otra unidad, se cambia UNA vez en
Configuracion > Items de inventario y el stock, los movimientos y los gastos la toman solos. Ya no
hay que corregir registro por registro.

IMPORTANTE: cambiar la unidad es cambiar la etiqueta, no convierte cantidades. Si algun item quedo
cargado con cantidades en una unidad que no corresponde, avisenos y lo ajustamos aparte.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000019'
   AND estado NOT IN ('SOLUCIONADO', 'CERRADO');
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Devuelve el caso a <c>EN_ANALISIS</c> y le borra la solución, que es el estado exacto
        /// del que salió. Solo actúa si la solución sigue siendo la que escribió esta migración: si
        /// alguien la reescribió desde la app, no se toca.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'EN_ANALISIS',
       fecha_solucion       = NULL,
       solucion_descripcion = NULL,
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000019'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Corregido y desplegado.%la pantalla de Stock no mostraba la unidad%';
");
        }
    }
}
