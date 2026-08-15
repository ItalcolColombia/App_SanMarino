using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000021</b> («Seguimiento levante solucionar requerimientos de parámetros
    /// requeridos siempre aparte hembras y machos. Huevo es tema de producción») en
    /// <c>SOLUCIONADO</c>. Sin correos.
    /// </summary>
    /// <remarks>DATA-ONLY, idempotente, Designer clonado y ModelSnapshot intacto.</remarks>
    public partial class SolucionarTicketSeguimientoLevantePorSexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Corregido y desplegado. Los cuatro puntos del reporte:

1) SALIDAS POR SEXO. La columna ""TOTAL MORT+ SEL / DIA"" sumaba hembras y machos en una sola
   cifra. Ahora son dos columnas: ""TOTAL MORT+ SEL hembras / dia"" y ""TOTAL MORT+ SEL machos /
   dia"" (mortalidad + seleccion de cada sexo).

2) SALDO DE AVES POR SEXO. La columna ""Saldo aves vivas"" tambien era una sola cifra. Ahora son
   ""Saldo hembras"" y ""Saldo machos"". El sistema ya llevaba los dos saldos por separado, solo
   no se estaban mostrando.

3) UNIFORMIDAD Y C.V. Se agregaron cuatro columnas: Uniformidad hembras, Uniformidad machos,
   C.V. hembras y C.V. machos. Se capturan en el registro diario y no se veian en la tabla.
   Donde aparece un guion (—) es porque ese dia no hubo pesaje, no es un cero.

   ⚠️ IMPORTANTE sobre el C.V. en los lotes historicos: los lotes que se cargaron por plantilla
   antes del 07/08/2026 no tienen coeficiente de variacion, porque hasta esa fecha la plantilla de
   carga masiva de levante NO traia esa columna (se agrego junto con ""Coef. Variacion H/M"",
   ""Observaciones Pesaje"" y los cuatro campos de agua). En esos lotes la columna va a salir
   vacia; en los que se carguen de ahora en mas, con la plantilla nueva, sale completa.

4) HUEVOS. Salieron de la tabla de seguimiento de levante y de su Excel: son tema de produccion,
   como usted indica. Lo que NO se toco, a proposito, es la captura: el registro diario los sigue
   aceptando, el detalle (icono del ojo) los sigue mostrando y, sobre todo, el ARRASTRE de los
   huevos de levante hacia el lote de produccion cuando se cierra el lote sigue funcionando igual.
   Si se quisiera apagar tambien la captura, avisenos: eso es una configuracion de la empresa y hay
   que definir antes que pasa con ese arrastre y con los huevos ya registrados.

5) EXCEL. Todo lo anterior se refleja en el archivo que genera el boton ""Descargar seguimiento
   (Excel)"": las dos columnas de salidas por sexo, los dos saldos, uniformidad y C.V. de cada
   sexo, y sin las columnas de huevos.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000021'
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
 WHERE codigo = 'TK-2026-000021'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Corregido y desplegado. Los cuatro puntos del reporte:%';
");
        }
    }
}
