using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Cierra el «hueco de LECTURA» de la guía genética: hasta hoy los 5 objetos SQL de postura
    /// leían <c>guia_genetica_sanmarino_colombia</c> HARDCODEADA, así que para una empresa cuya guía
    /// vive en la tabla reducida (<c>guia_genetica_santa_reyes</c>) devolvían 0 filas y la columna
    /// «Tabla» salía VACÍA, sin error. Los reportes técnicos en C# sí funcionaban, porque pasan por
    /// <c>GuiaGeneticaLookup</c> — de ahí el síntoma que reportó el cliente: «a veces aparece y a
    /// veces no», según si la pantalla la calcula C# o Postgres.
    ///
    /// <para>
    /// Crea <c>vw_guia_genetica_postura</c> (UNION ALL de las dos tablas, con una columna
    /// <c>origen</c>) y repunta los 5 objetos para que lean de ahí.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Delta cero por construcción, no por revisión.</b> Los 5 objetos filtran
    /// <c>guia.company_id = lote.company_id</c> y las dos tablas están PARTICIONADAS por empresa
    /// (medido el 26-ago-2026: compartida ⇒ companies 1/3/4, reducida ⇒ company 6, intersección
    /// vacía). Para Sanmarino, Demo, Ecuador y Panamá la rama nueva aporta CERO filas.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Por qué las 2 fns de indicadores cambian algo más que el <c>FROM</c>.</b> La guía
    /// reducida tiene 3 columnas de dato; la compartida más de 40. Las fns coalescean a <c>0</c> lo
    /// que falta, y ese <c>0</c> NO es neutro: levante promedia por sexo dividiendo por 2 FIJO, así
    /// que una guía de solo hembras mostraría <b>la mitad</b> del valor del cliente
    /// (<c>(95.00 + 0)/2 = 47,5</c>) — un número plausible y equivocado por un factor de 2. Y en
    /// producción, <c>fn_dif_pp</c> documenta que con guía = 0 no devuelve NULL, así que la columna
    /// «diferencia vs guía» pintaría la mortalidad REAL del lote como si fuera la desviación.
    /// Por eso las fns leen <c>origen</c> y aplican el COALESCE sólo cuando vale
    /// <c>'compartida'</c> — literalmente el comportamiento de hoy para las otras cuatro empresas.
    /// Quitar esos COALESCE a secas NO sería delta cero: en el rango de producción, company 1 tiene
    /// entre 6 y 14 filas en blanco por columna.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>No se toca un solo <c>WHERE</c>.</b> Los criterios de join divergen A PROPÓSITO entre
    /// fns (levante compara raza exacta y no filtra <c>deleted_at</c>; producción usa
    /// <c>btrim(lower())</c> y sí filtra; levante cruza la edad como texto exacto y producción la
    /// parsea con desempate <c>'25P'</c>). Unificarlos haría que empiecen a matchear filas que hoy
    /// no matchean, o sea que el refactor cambiaría resultados por sí solo.
    /// </para>
    ///
    /// <para>
    /// Espejos legibles en <c>backend/sql/</c>. Esta migración es el vehículo; el <c>.sql</c> es el
    /// espejo. <b>Reversible</b>: el <c>Down()</c> restaura los 5 objetos a su versión de HEAD
    /// (copiada verbatim) y borra la vista.
    /// </para>
    /// </summary>
    public partial class VwGuiaGeneticaPosturaYFnsOrigen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La vista primero: los 5 objetos de abajo dependen de ella.
            migrationBuilder.Sql(VwGuiaGeneticaPosturaNueva);

            // Las 2 fns de indicadores: FROM + lectura de `origen` + COALESCE condicionado.
            migrationBuilder.Sql(FnIndicadoresLevantePosturaNueva);
            migrationBuilder.Sql(FnIndicadoresProduccionPosturaNueva);

            // Los 3 restantes: sólo el FROM. Sus columnas pasan por f_safe_numeric(), que ya
            // degrada a NULL sola, así que no fabrican el 0 falso que allá hubo que condicionar.
            migrationBuilder.Sql(FnResumenSemanalRaPesadasLevanteNueva);
            migrationBuilder.Sql(FnResumenSemanalRaPesadasProduccionNueva);
            migrationBuilder.Sql(VwGuiaGeneticaPorLotePosturaNueva);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Orden inverso: primero los consumidores vuelven a apuntar a la tabla, y recién
            // entonces se puede borrar la vista (si no, DROP VIEW falla por dependencia).
            migrationBuilder.Sql(VwGuiaGeneticaPorLotePosturaPrevia);
            migrationBuilder.Sql(FnResumenSemanalRaPesadasProduccionPrevia);
            migrationBuilder.Sql(FnResumenSemanalRaPesadasLevantePrevia);
            migrationBuilder.Sql(FnIndicadoresProduccionPosturaPrevia);
            migrationBuilder.Sql(FnIndicadoresLevantePosturaPrevia);

            migrationBuilder.Sql("DROP VIEW IF EXISTS public.vw_guia_genetica_postura;");
        }
    }
}
