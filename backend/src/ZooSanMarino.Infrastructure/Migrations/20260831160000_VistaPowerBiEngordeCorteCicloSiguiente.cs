using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Lleva a <c>vw_seguimiento_pollo_engorde</c> el <b>corte por ciclo siguiente</b> (v14) que
    /// <c>fn_seguimiento_diario_engorde</c> tiene desde junio y que la vista nunca recibió, aunque se
    /// declara a sí misma «espejo set-based de la fn».
    /// </summary>
    /// <remarks>
    /// <b>Qué le faltaba.</b> Medido contra la BD:
    /// <c>position('corte_ciclo_siguiente' in pg_get_viewdef(…))</c> daba <b>0</b> en la vista y
    /// <b>6573</b> en la función. Su <c>rango_final</c> conservaba el <c>COALESCE</c> pelado, sin
    /// <c>LEAST</c>: un lote sin cierre por saldo y sin estado <c>cerrado</c> se queda con
    /// <c>fecha_max = NULL</c> —o sea, sin tope— y se lleva el alimento del ciclo siguiente. Es el
    /// mismo defecto que motivó <c>TK-2026-000015</c>, corregido en la función y no en su espejo.
    ///
    /// <b>La vista se sirve a Power BI</b> (<c>usrDWH</c>), así que el número mal no se ve en la app:
    /// se ve en los tableros de gerencia, que es peor, porque nadie lo cruza contra la pantalla.
    ///
    /// <b>Alcance real, medido antes de tocar nada:</b> <b>0 filas</b> exceden el corte hoy, en las
    /// cinco empresas — los dos lotes expuestos (20 y 86) están <c>Cerrado</c>, y ese estado es su
    /// única tapa. Están a un botón de «Reabrir» de distancia, y ese botón existe en la aplicación.
    /// Por eso el cambio se prueba con un <b>contrafactual</b>, no con el estado actual: reabiertos en
    /// una transacción revertida, la vista vieja se desbordaba hasta el <c>2026-08-28</c> (96 filas en
    /// el lote 20) y la nueva corta en <c>2026-04-12</c> (62), que es donde termina el ciclo.
    ///
    /// <b>Por qué NO es cambio de comportamiento.</b> El <c>LEFT JOIN</c> sobre el CTE nuevo y el
    /// <c>LEAST</c>: un lote sin ciclo posterior tiene <c>cc.hasta = NULL</c>, y <c>LEAST</c> ignora
    /// los NULL, así que conserva exactamente su <c>fecha_max</c> anterior. Verificado fila a fila
    /// contra la vista viva, desplegando la nueva en paralelo con otro nombre: <b>0 filas</b> de
    /// diferencia en los dos sentidos, mismas 6.784 filas y mismas 67 columnas.
    ///
    /// <b><c>CREATE OR REPLACE</c>, no <c>DROP</c> + <c>CREATE</c>:</b> conserva owner y GRANT, así
    /// que el usuario de Power BI no pierde el acceso en ningún ambiente. Exige la misma lista de
    /// columnas en el mismo orden, que es justamente la garantía que se busca acá.
    ///
    /// El espejo <c>backend/sql/vw_seguimiento_pollo_engorde.sql</c> se actualiza en este mismo
    /// commit: el gate de CI exige que todo <c>vw_</c> llegue por migración, y la lección de este
    /// hallazgo es precisamente que el espejo y lo desplegado no pueden divergir.
    ///
    /// Plan: <c>fase_de_desarrollo/correccion_hallazgos_auditoria_tickets_plan.md</c> (hallazgo #12).
    /// </remarks>
    public partial class VistaPowerBiEngordeCorteCicloSiguiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(VISTA_CON_CORTE);
        }

        /// <summary>Restaura la definición previa, verbatim desde el espejo anterior.</summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(VISTA_SIN_CORTE);
        }
    }
}
