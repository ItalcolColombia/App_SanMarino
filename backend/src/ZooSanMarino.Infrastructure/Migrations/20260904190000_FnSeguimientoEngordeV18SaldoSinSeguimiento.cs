using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// v18 de <c>fn_seguimiento_diario_engorde</c>: un lote que todavía no cargó NINGÚN seguimiento
    /// deja de mostrar como saldo todo el alimento que el galpón recibió en su historia.
    ///
    /// <para>
    /// <b>El ticket.</b> Panamá, DOÑA MARIA / núcleo A / galpón 4 (G0475), lote 95 (04-sep-2026): la
    /// grilla diaria mostraba <b>una sola fila</b> con <c>saldo_alimento_kg = 176.246,967</c> contra un
    /// único ingreso de 11.740 kg. La operación lo leyó —bien— como «el saldo acumulado del lote
    /// anterior, que ya liquidamos».
    /// </para>
    ///
    /// <para>
    /// <b>La causa.</b> <c>hist_full</c>, <c>hist_alimento</c> y <c>docs_por_fecha</c> acotaban el
    /// histórico con <c>(rs.fecha_min IS NULL OR DATE(h.fecha_operacion) &gt;= rs.fecha_min)</c>.
    /// <c>fecha_min</c> es el primer seguimiento del lote, así que mientras no hay ninguno la condición
    /// es verdadera para todo y el saldo suma cada movimiento del galpón desde siempre — ciclos
    /// anteriores incluidos y sin los guards que sí protegen la apertura (<c>lotes_ajenos</c> v11 y
    /// <c>corte_apertura</c> v12 viven solo en <c>apert_mov</c>, que además exige
    /// <c>fecha_min IS NOT NULL</c> y por eso vale 0). La incoherencia era <b>interna</b>:
    /// <c>fechas_universo</c> sí corta en <c>li.fecha_corte_alimento</c>, de ahí que se viera una fila
    /// sola con el saldo de toda la historia.
    /// </para>
    ///
    /// <para>
    /// Aritmética exacta sobre la copia de producción del 04-sep, galpón G0475:
    /// 173.296,967 kg de <c>INV_INGRESO</c> desde el 02-jul (lote 165 «94 - 2», liquidado el 27-ago)
    /// − 6.350 de devolución por eliminación − 2.440 del ingreso que se editó + 11.740 del ingreso real
    /// = <b>176.246,967</b>. <b>Reproducido en transacción revertida</b> (1 fila, <c>edad_dia</c> 2,
    /// <c>saldo_aves</c> 19.110): idéntico a la captura del ticket.
    /// </para>
    ///
    /// <para>
    /// <b>El arreglo.</b> Las tres CTE del saldo usan el MISMO piso que ya usan las filas:
    /// <c>DATE(h.fecha_operacion) &gt;= COALESCE(rs.fecha_min, li.fecha_corte_alimento, DATE(...))</c>.
    /// Con <c>fecha_min</c> presente la expresión es idéntica a v17 ⇒ salida byte a byte igual para
    /// todo lote con al menos un seguimiento. Si tampoco hay encaset, el tercer término deja la
    /// condición siempre verdadera ⇒ comportamiento actual: no se inventa una regla para un lote sin
    /// fecha.
    /// </para>
    ///
    /// <para>
    /// <b>GATE MULTIPAÍS</b> (CLAUDE.md § Invariantes) sobre la copia de producción del 04-sep-2026,
    /// las 7.102 filas que devuelve la fn para los 178 lotes vivos de TODAS las empresas:
    /// <b>0 filas que desaparecen, 0 filas nuevas y 0 filas distintas</b> (<c>EXCEPT</c> en los dos
    /// sentidos, todas las columnas). El caso roto no aparece en el censo porque exige un lote sin un
    /// solo seguimiento Y un movimiento dentro de su ventana: se verificó con el lote del ticket
    /// simulado en transacción revertida, donde el saldo pasa de <b>176.246,967 a 11.740</b> y los
    /// escenarios con seguimiento (11.740 y 11.120) quedan intactos.
    /// </para>
    ///
    /// <para>
    /// La firma NO cambia (49 columnas OUT) ⇒ los 5 consumidores que la llaman por
    /// <c>CROSS JOIN LATERAL</c> con columnas nombradas no se tocan. Idempotente
    /// (<c>DROP … IF EXISTS</c> + <c>CREATE</c>). Sin DDL de tablas ni cambios de modelo
    /// (ModelSnapshot intacto). <c>Down</c> = v17 verbatim.
    /// </para>
    ///
    /// <para>
    /// Plan: <c>fase_de_desarrollo/tk_panama_saldo_alimento_lote_sin_seguimiento_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_seguimiento_diario_engorde.sql</c>. Esta migración es el
    /// <b>vehículo</b>: nada de <c>backend/sql/</c> llega a producción por sí solo.
    /// Las constantes SQL viven en el partial <c>.Fn.cs</c>.
    /// </para>
    /// </summary>
    public partial class FnSeguimientoEngordeV18SaldoSinSeguimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV18);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV17);
        }
    }
}
