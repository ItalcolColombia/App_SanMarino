using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Deja <b>TK-2026-000022</b> («Indicadores Seguimiento Levante no diferencia hembras o machos.
    /// Eficiencia») en <c>SOLUCIONADO</c>, con la respuesta a las dos preguntas del usuario: de
    /// dónde salía la columna Eficiencia y por qué los parámetros no decían el sexo. Sin correos.
    /// </summary>
    /// <remarks>DATA-ONLY, idempotente, Designer clonado y ModelSnapshot intacto.</remarks>
    public partial class SolucionarTicketIndicadoresLevantePorSexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE tickets
   SET estado               = 'SOLUCIONADO',
       fecha_solucion       = COALESCE(fecha_solucion, timezone('utc', now())),
       solucion_descripcion = 'Corregido y desplegado.

1) DE DONDE SALIA ""EFICIENCIA"": era Ganancia de peso de la semana (g) dividida por el consumo por
   ave de la semana (g) - o sea, la inversa de la conversion alimenticia. No es un indicador de la
   guia genetica ni de reproductoras: quedo de una version vieja de la pantalla, que ademas la usaba
   para calcular IP (Eficiencia x Supervivencia) y VPI (que devolvia EXACTAMENTE el mismo numero que
   IP). Como usted indica que no lo manejan, se quitaron las tres columnas: Eficiencia, IP y VPI.

2) HEMBRAS Y MACHOS: tenia razon, la tabla mostraba una sola serie de numeros y no decia de que
   sexo eran. Peor todavia: el PESO y la UNIFORMIDAD que se mostraban eran el promedio aritmetico
   simple de hembras y machos (sin ponderar por cantidad de aves), o sea un valor que no le
   corresponde a ninguna ave del galpon. En el lote S369A, semana 8, las hembras pesaban 889 g y los
   machos 1.487 g: la tabla mostraba 1.188 g, un peso que no tenia ninguna ave.

   Ahora cada bloque dice HEMBRAS o MACHOS y se calcula con el saldo del propio sexo:
   - Aves inicio y fin
   - Consumo (g/ave/dia) real y guia
   - Consumo de la semana en gramos
   - Ganancia de la semana
   - Peso real, peso de guia y diferencia %
   - Uniformidad
   - Mortalidad real y de guia
   - Seleccion, error de sexaje y retiro
   Se conserva una sola columna del lote completo, ""Consumo lote semana (g)"", porque ahi el total
   si tiene sentido.

3) Donde aparece un guion (—) es porque ese sexo no tiene aves en el lote, la semana no tuvo pesaje
   o la guia genetica no trae ese dato para el sexo. Antes esos casos se mostraban como 0, que se
   leia como una medicion real de cero.

4) La UNIFORMIDAD DE GUIA es una sola columna para los dos sexos: la guia genetica no la abre por
   sexo. Queda rotulada asi para que no se confunda.

5) El Excel (boton ""Descargar Excel (Seguimiento + Indicadores)"") sale con las mismas columnas por
   sexo. Al final se dejaron las columnas del lote completo que ya venian, para no romper las
   planillas que ustedes tengan armadas encima.

NOTA: ningun numero anterior se recalculo: las columnas por sexo salen de las mismas cuentas que ya
hacia el sistema, solo que ahora se publican sin promediar los dos sexos.',
       updated_at           = timezone('utc', now())
 WHERE codigo = 'TK-2026-000022'
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
 WHERE codigo = 'TK-2026-000022'
   AND estado = 'SOLUCIONADO'
   AND solucion_descripcion LIKE 'Corregido y desplegado.%DE DONDE SALIA%';
");
        }
    }
}
