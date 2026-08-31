using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cierra los dos huecos que quedaban entre la guía genética de Santa Reyes y el camino SQL, y
    /// saca de la fn de producción la única constante que asumía un negocio de reproductoras.
    ///
    /// <para>
    /// <b>1 · El alias de raza del ERP llega a SQL (la vista).</b> Los lotes se cargan con la grafía
    /// del ERP del cliente (<c>BABCOK BROWN</c> sin la 2ª C, <c>HY LINE</c> sin el apellido) y la
    /// guía se sembró con el nombre comercial (<c>Babcock Brown</c>, <c>Hy Line Brown</c>). El C# lo
    /// tolera desde el 24-ago-2026 (<c>RazaGuiaAliasCalculos</c> vía <c>GuiaGeneticaLookup</c>), pero
    /// el camino SQL comparaba la raza CRUDA. Medido el 30-ago-2026 sobre el lote 152: con
    /// <c>BABCOK BROWN</c> el reporte técnico mostraba la guía y los indicadores de producción no
    /// mostraban NADA —y los de levante, <c>0,00</c>—. El mismo lote con dos verdades según quién
    /// calculara la pantalla. Se resuelve en <c>vw_guia_genetica_postura</c>, con una tercera rama
    /// que proyecta las filas de la guía propia bajo la grafía del ERP: los cuatro objetos que la
    /// leen heredan el alias sin tocar un solo criterio de join.
    /// </para>
    ///
    /// <para>
    /// <b>2 · Levante deja de mentir dos veces</b> (<c>fn_indicadores_levante_postura</c>): compara
    /// la raza normalizada <b>sólo en la rama propia</b> —producción ya lo hacía, y tenerlo de un
    /// lado solo era la causa de que <c>CRIOLLA</c> cruzara en producción y no en levante—, y deja
    /// de coalescear a <c>0</c> cuando la empresa TIENE guía propia y la semana no tiene fila. Su
    /// guía arranca en la semana 18 y el levante empieza en la 1: ese <c>0,00</c> era un objetivo
    /// inventado en las columnas de guía, no un dato faltante.
    /// </para>
    ///
    /// <para>
    /// <b>3 · La semana de arranque de producción pasa a ser de la empresa</b>
    /// (<c>companies.semana_inicio_indicadores_produccion</c>, DEFAULT <b>25</b>). Ese 25 estaba
    /// hardcodeado en <c>fn_indicadores_produccion_postura</c> (<c>DELETE FROM _seg WHERE sem_vida
    /// &lt; 25</c> + <c>FOR s IN 25..</c>) y es la regla de una reproductora. Santa Reyes es postura
    /// comercial: empieza a poner en la 18 —la primera edad de su guía propia, coherente con su
    /// <c>huevo_primera_postura_hasta_semana = 22</c>—, así que sus semanas 18-24 no aparecían en
    /// ningún indicador, sin error ni aviso. Se siembra 18 para Santa Reyes.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Delta cero por construcción para las otras cuatro empresas.</b> La rama alias sólo
    /// produce filas para razas de <c>guia_genetica_santa_reyes</c>, tabla que sólo tiene filas de
    /// company 6 (medido: 889/15/224 filas de la compartida quedan idénticas). Los dos cambios de
    /// levante están detrás de <c>origen = 'propia'</c> y de un <c>EXISTS</c> sobre esa misma tabla,
    /// que para ellas es falso. Y la columna nueva nace con el 25 de siempre. No es «se verificó
    /// después»: para Sanmarino, Demo, Ecuador y Panamá la expresión ejecutada es la misma de hoy.
    /// Gate: <c>backend/sql/verificar_paridad_guia_genetica.sql</c>, antes y después.
    /// </para>
    ///
    /// <para>
    /// Espejos legibles en <c>backend/sql/</c>. Esta migración es el vehículo; el <c>.sql</c> es el
    /// espejo. <b>Reversible</b>: el <c>Down()</c> restaura los 3 objetos a su versión de HEAD
    /// (copiada verbatim) y borra la columna.
    /// </para>
    /// </summary>
    public partial class AliasRazaGuiaSqlYSemanaInicioProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) La columna. Idempotente y con el DEFAULT que preserva el comportamiento. ──
            // Va con SQL crudo (y no con AddColumn) para que re-aplicarla sobre una base que ya la
            // tiene no falle: es el criterio de migraciones idempotentes del repo.
            migrationBuilder.Sql("""
                ALTER TABLE public.companies
                    ADD COLUMN IF NOT EXISTS semana_inicio_indicadores_produccion integer NOT NULL DEFAULT 25;

                COMMENT ON COLUMN public.companies.semana_inicio_indicadores_produccion IS
                  'Semana de VIDA desde la que fn_indicadores_produccion_postura muestra datos. 25 = '
                  'el valor que estuvo hardcodeado hasta el 30-ago-2026 (reproductora). Santa Reyes usa '
                  '18: es postura comercial y su guia propia arranca ahi.';
                """);

            // ── 2) El valor de Santa Reyes. `IS DISTINCT FROM` para no ensuciar el updated_at ni
            //      pisar un ajuste posterior del cliente si la migración se re-corre.
            migrationBuilder.Sql("""
                UPDATE public.companies
                   SET semana_inicio_indicadores_produccion = 18
                 WHERE name = 'Santa Reyes'
                   AND semana_inicio_indicadores_produccion IS DISTINCT FROM 18;
                """);

            // ── 3) La vista primero: las dos fns de abajo dependen de ella. ──
            migrationBuilder.Sql(VwGuiaGeneticaPosturaNueva);

            // ── 4) Las dos fns de indicadores. ──
            migrationBuilder.Sql(FnIndicadoresLevantePosturaNueva);
            migrationBuilder.Sql(FnIndicadoresProduccionPosturaNueva);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Orden inverso: las fns primero (la de producción deja de nombrar la columna), después
            // la vista, y recién entonces se puede borrar la columna sin romper nada.
            migrationBuilder.Sql(FnIndicadoresProduccionPosturaPrevia);
            migrationBuilder.Sql(FnIndicadoresLevantePosturaPrevia);
            migrationBuilder.Sql(VwGuiaGeneticaPosturaPrevia);

            migrationBuilder.Sql(
                "ALTER TABLE public.companies DROP COLUMN IF EXISTS semana_inicio_indicadores_produccion;");
        }
    }
}
