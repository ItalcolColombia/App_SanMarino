using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// v19 de <c>fn_seguimiento_diario_engorde</c>: un ajuste de cuadre SIN lote deja de ser
    /// alimento del ciclo siguiente.
    ///
    /// <para>
    /// <b>El ticket.</b> Panamá, DOÑA MARIA / núcleo C / galpón 2 (<c>G0490</c>), lote 257
    /// «61 - 1» (10-sep-2026): <i>«en el diario de alimento aparece una cantidad y en el stock
    /// aparece otra»</i>. Stock <b>11.715,000 kg</b> (= 12.169 − 227 − 227) contra un saldo de tabla
    /// de <b>7.718,44</b>: <b>3.996,56 kg exactos</b> de diferencia. La tabla abría el ciclo en
    /// <c>apertura_alimento_kg = −3.996,56</c>, con <c>apertura_documentos = «Eliminación de stock»</c>.
    /// </para>
    ///
    /// <para>
    /// <b>La causa, con horas.</b> El 05-sep a las 09:56:35 y 09:56:38 borraron (soft delete) los
    /// lotes 168 y 169, los únicos vivos de <c>G0490</c>. A las 09:58:05 borraron el registro de
    /// stock que sobraba de ese ciclo — 3.996,560 kg de <c>AV. SUPER POLLO ENGORDE</c>.
    /// <c>EliminarStockAsync</c> escribe entonces, además del <c>EliminacionStock</c>, un
    /// <c>AjusteCuadreTablaSalida</c> para que la baja llegue también a la tabla diaria (el fix del
    /// 1-sep, TK-2026-000183). El lote se lo pone el trigger vía
    /// <c>fn_lote_ave_engorde_id_desde_ubicacion</c>, que filtra <c>deleted_at IS NULL</c> ⇒
    /// devolvió <b>NULL</b>, porque los candidatos se habían borrado 55 segundos antes. El 06-sep
    /// encasetaron el lote 257 y la ventana de alimento previo al encaset (v9, 10 días) recogió ese
    /// −3.996,56 como apertura.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué las guardas existentes no alcanzan.</b> <c>lotes_ajenos</c> (v11) filtra por lote
    /// y su condición es <c>h.lote_ave_engorde_id IS NULL OR NOT EXISTS (…)</c> ⇒ con lote NULL es
    /// <b>tautológica</b>. <c>corte_apertura</c> (v12) sube el piso de la ventana al día siguiente
    /// del último seguimiento del ciclo anterior, pero busca ese ciclo con
    /// <c>l2.deleted_at IS NULL</c>: los lotes recién borrados son invisibles y el piso cae de
    /// vuelta en <c>fecha_encaset − 10</c> = 27-ago. Aunque no se hubieran borrado, el corte por
    /// fecha tampoco lo habría atrapado: la corrección se <i>registra</i> el día de la limpieza, no
    /// el día del hecho que corrige.
    /// </para>
    ///
    /// <para>
    /// <b>La regla.</b> El fallback documentado en <c>fn_lote_ave_engorde_id_desde_ubicacion</c>
    /// —«si no queda lote vivo devuelve NULL y la apertura del ciclo siguiente las recoge»— es
    /// CORRECTO para un <b>ingreso</b> (alimento que llegó antes que los pollitos es del ciclo que
    /// entra) y EQUIVOCADO para una <b>salida de cuadre</b>: es la limpieza contable de un ciclo que
    /// ya no existe, no es de nadie, y cobrársela al siguiente le descuenta alimento que nunca
    /// recibió — encima de otro tipo (<c>SUPER POLLO ENGORDE</c> contra el <c>PREINICIADOR</c> del
    /// ciclo nuevo).
    /// </para>
    ///
    /// <para>
    /// <b>El arreglo.</b> Las 5 CTE que leen <c>INV_AJUSTE_CUADRE_ENTRADA</c>/<c>_SALIDA</c>
    /// (<c>apert_mov</c>, <c>hist_full</c>, <c>hist_alimento</c>, <c>docs_por_fecha</c>,
    /// <c>fechas_universo</c>) descartan los que vienen con <c>lote_ave_engorde_id IS NULL</c>. Un
    /// ajuste CON lote se comporta exactamente igual que hoy.
    /// </para>
    ///
    /// <para>
    /// <b>Las tres alternativas descartadas</b>, cada una con el número que la mata:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     Excluir <i>todos</i> los <c>INV_AJUSTE_CUADRE_*</c> de la apertura ⇒ rompe el ajuste
    ///     legítimo cargado sobre un lote encasetado que todavía no cargó su día 1.
    ///   </description></item>
    ///   <item><description>
    ///     Aceptar solo los del propio lote (<c>= p_lote_id</c>) ⇒ <b>rompe un galpón que hoy
    ///     cuadra</b>: en <c>GALPON</c> (Doña María C-1) el ajuste del lote 254 está en la ventana
    ///     de apertura del 256 y es correcto que entre (comparten bodega, v10). Excluirlo subía la
    ///     apertura del 256 de 9.847 a 11.993,35 y su saldo final a 10.496,35 contra un stock de
    ///     8.350.
    ///   </description></item>
    ///   <item><description>
    ///     Cambiar el trato del <c>EliminacionStock</c> (<c>INV_OTRO</c>) ⇒ es el naufragio de
    ///     v15/v16, que el gate multipaís revirtió <b>dos veces</b>: mueve filas que ya existen.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>GATE MULTIPAÍS</b> (CLAUDE.md § Invariantes) sobre la copia del 10-sep-2026, las
    /// <b>7.051 filas</b> que la fn devuelve para los <b>174 lotes vivos</b> de las dos empresas que
    /// tienen engorde (ItalcolEcuador 132 lotes con filas, ItalcolPanamá 42; Sanmarino, Demo y Santa
    /// Reyes no tienen un solo lote de engorde): <c>EXCEPT</c> en los dos sentidos, todas las
    /// columnas ⇒ <b>el único lote que cambia es el 257</b> (2 filas, las suyas). Ecuador da
    /// <b>0</b>. Es 0 por construcción: las filas <c>INV_AJUSTE_CUADRE_*</c> existen solo en Panamá
    /// y solo desde el 05-sep-2026 — 11 filas, 45.183,08 kg, 10 de ellas sin lote.
    /// </para>
    ///
    /// <para>
    /// <b>Efecto medido</b> (transacción revertida): lote 257 apertura <c>−3.996,56 → 0</c> y saldo
    /// del 08-sep <c>7.718,44 → 11.715 = stock</c>. Lote 256 <b>intacto</b> (apertura 9.847, saldo
    /// 8.350 = stock) — la prueba de que la alternativa (2) habría roto un galpón sano. Lote 254
    /// <b>intacto</b> (saldo 8.804).
    /// </para>
    ///
    /// <para>
    /// <b>Repara hacia atrás sin tocar un solo dato:</b> la fn se recalcula en cada lectura, así que
    /// los 39.040,18 kg de ajustes huérfanos que todavía esperan en 7 galpones sin ciclo vivo
    /// (<c>G0496</c> 16.751,88 · <c>G0492</c> 16.606,80 · <c>G0469</c> 2.641,85 · <c>G0495</c>
    /// 1.332,31 · <c>G0491</c> 862,52 · <c>G0470</c> 807,81 · <c>G0494</c> 37,00) dejan de ser una
    /// mina para el próximo encaset. Ningún <c>UPDATE</c> de datos.
    /// </para>
    ///
    /// <para>
    /// La firma NO cambia (49 columnas OUT) ⇒ los 5 consumidores que la llaman por
    /// <c>CROSS JOIN LATERAL</c> con columnas nombradas no se tocan. Idempotente
    /// (<c>DROP … IF EXISTS</c> + <c>CREATE</c>). Sin DDL de tablas ni cambios de modelo
    /// (ModelSnapshot intacto). <c>Down</c> = v18 verbatim.
    /// </para>
    ///
    /// <para>
    /// Plan: <c>fase_de_desarrollo/apertura_engorde_ajuste_cuadre_huerfano_plan.md</c>.
    /// Espejo: <c>backend/sql/fn_seguimiento_diario_engorde.sql</c>. Esta migración es el
    /// <b>vehículo</b>: nada de <c>backend/sql/</c> llega a producción por sí solo.
    /// Las constantes SQL viven en el partial <c>.Fn.cs</c>.
    /// </para>
    /// </summary>
    public partial class FnSeguimientoEngordeV19AperturaIgnoraAjusteHuerfano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV19);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV18);
        }
    }
}
