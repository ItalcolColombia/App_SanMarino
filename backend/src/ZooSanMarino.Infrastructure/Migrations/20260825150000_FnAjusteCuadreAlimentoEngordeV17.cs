using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// El AJUSTE DE CUADRE: dos <c>tipo_evento</c> nuevos que corrigen la TABLA DIARIA de alimento
    /// sin tocar el stock, más las tres funciones que aprenden a leerlos.
    ///
    /// <para>
    /// <b>El problema.</b> El invariante del cuadre es
    /// <c>saldo == stock − movimientos posteriores</c>. Cuando los dos lados se separan hay que poder
    /// corregir <b>cualquiera</b> de los dos, y hasta hoy solo se podía corregir el stock:
    /// <c>AjusteStock</c> y <c>EliminacionStock</c> se espejan como <c>INV_OTRO</c>, que
    /// <c>fn_seguimiento_diario_engorde</c> no lee en ninguna de sus 5 CTE. Un galpón cuyo inventario
    /// ya estaba bien y cuya tabla diaria quedó alta <b>no tenía arreglo posible desde la pantalla</b>.
    /// Medido el 25-ago-2026 sobre la copia de producción: 12 galpones de ItalcolPanama, 55.866,5 kg.
    /// </para>
    ///
    /// <para>
    /// <b>Qué entra.</b> <c>movement_type</c> <c>AjusteCuadreTablaEntrada</c> /
    /// <c>AjusteCuadreTablaSalida</c> → <c>tipo_evento</c> <c>INV_AJUSTE_CUADRE_ENTRADA</c> /
    /// <c>INV_AJUSTE_CUADRE_SALIDA</c>, leídos por la fn diaria con el mismo signo que una entrada y
    /// una salida de traslado. Son la contraparte simétrica del ajuste de stock: uno mueve el
    /// inventario y la tabla no lo ve; el otro mueve la tabla y el inventario no lo ve. Con los dos,
    /// la pantalla de Cuadre puede cerrar el galpón venga el error del lado que venga.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Por qué esto NO repite el naufragio de v15 y v16</b> (los dos revertidos por el gate):
    /// aquellos cambiaron el tratamiento de filas que <b>ya existían</b> —el disyunto de
    /// <c>para_proximo_ciclo</c>— y movieron números vivos en 1.733 filas. Este agrega tipos que
    /// <b>ninguna fila del histórico tiene</b>: con 0 filas de esos tipos la salida es byte a byte la
    /// de v16a. Lo que cambia es lo que se puede escribir de acá en adelante, no lo que ya está
    /// escrito.
    /// </para>
    ///
    /// <para>
    /// <b>GATE MULTIPAÍS</b> (CLAUDE.md § Invariantes), <c>backend/sql/verificar_paridad_saldo_engorde.sql</c>,
    /// antes y después, mismo comando:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>6.851 filas en las dos corridas (ItalcolEcuador 5.501 + ItalcolPanama 1.350);</description></item>
    ///   <item><description><b>0</b> filas que desaparecen, <b>0</b> filas nuevas, <b>0</b> dif_saldo_alimento,
    ///     <b>0</b> dif_saldo_aves, <b>0</b> dif_ingreso, <b>0</b> dif_consumo y <b>0</b> dif_documento
    ///     <b>en las dos empresas</b>;</description></item>
    ///   <item><description>6.765 filas de seguimiento esperadas == 6.765 presentes;</description></item>
    ///   <item><description><c>fn_cuadre_alimento_engorde(NULL)</c>: idéntico antes y después —
    ///     ItalcolEcuador 37 galpones / 1 descuadrado / 5.000,0 kg; ItalcolPanama 31 / 12 / 55.866,5 kg.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Lo que NO cambia, a propósito:</b> la firma de la fn diaria (siguen las 49 columnas OUT, así
    /// que los 5 consumidores que la llaman por <c>CROSS JOIN LATERAL</c> con columnas NOMBRADAS no se
    /// tocan) y <c>fn_acumulado_entradas_alimento</c>, que sigue contando solo <c>INV_INGRESO</c> e
    /// <c>INV_TRASLADO_ENTRADA</c> — un ajuste de cuadre es una corrección, no alimento que llegó.
    /// </para>
    ///
    /// <para>
    /// Plan: <c>fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md</c> §1 (F2).
    /// Espejos: <c>backend/sql/fn_seguimiento_diario_engorde.sql</c>,
    /// <c>backend/sql/fn_cuadre_alimento_engorde.sql</c> y el <c>fn_tipo_evento_inventario</c> de
    /// <c>backend/sql/create_lote_registro_historico_unificado.sql</c>. Esta migración es el
    /// <b>vehículo</b>: nada de <c>backend/sql/</c> llega a producción por sí solo.
    /// </para>
    ///
    /// Idempotente: las tres son <c>CREATE OR REPLACE</c> / <c>DROP … IF EXISTS</c> + <c>CREATE</c>.
    /// Sin DDL de tablas ni cambios de modelo (ModelSnapshot intacto).
    /// Las constantes SQL viven en el partial <c>.Fn.cs</c>.
    /// </summary>
    public partial class FnAjusteCuadreAlimentoEngordeV17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) El mapeo movement_type -> tipo_evento. Va PRIMERO: sin él, un movimiento del tipo
            //    nuevo caería en INV_OTRO y la fn no lo vería nunca.
            migrationBuilder.Sql(FnTipoEventoInventarioConAjusteCuadre);

            // 2) La fn diaria, que es la que traduce el tipo nuevo en kilos de la tabla.
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV17);

            // 3) El cuadre, para que un ajuste fechado DESPUÉS del último seguimiento cuente como
            //    movimiento posterior igual que un ingreso, y no aparezca como descuadre nuevo.
            migrationBuilder.Sql(FnCuadreAlimentoEngordeConAjuste);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FnCuadreAlimentoEngordePrevia);
            migrationBuilder.Sql(FnSeguimientoDiarioEngordeV16a);
            migrationBuilder.Sql(FnTipoEventoInventarioPrevia);
        }

        /// <summary>
        /// <c>fn_tipo_evento_inventario</c> con los dos tipos nuevos. Es <c>CREATE OR REPLACE</c> y
        /// no cambia la firma, así que el trigger que la llama no se toca.
        /// </summary>
        private const string FnTipoEventoInventarioConAjusteCuadre = """
CREATE OR REPLACE FUNCTION public.fn_tipo_evento_inventario(p_mt VARCHAR) RETURNS VARCHAR
LANGUAGE plpgsql
IMMUTABLE
AS $$
BEGIN
    IF p_mt IS NULL THEN RETURN 'INV_OTRO'; END IF;
    IF p_mt ILIKE 'Ingreso' THEN RETURN 'INV_INGRESO'; END IF;
    IF p_mt ILIKE 'TrasladoEntrada' OR p_mt ILIKE 'TrasladoInterGranjaEntrada' THEN RETURN 'INV_TRASLADO_ENTRADA'; END IF;
    IF p_mt ILIKE 'TrasladoSalida' OR p_mt ILIKE 'TrasladoInterGranjaSalida'
       OR p_mt ILIKE 'TrasladoInterGranjaPendiente' THEN RETURN 'INV_TRASLADO_SALIDA'; END IF;
    IF p_mt ILIKE 'Consumo' THEN RETURN 'INV_CONSUMO'; END IF;
    -- Ajuste de cuadre (25-ago-2026): corrige la TABLA DIARIA sin tocar el stock, que es el caso
    -- que no tenia arreglo posible desde la pantalla. Tipo propio y no INV_OTRO justamente para que
    -- `fn_seguimiento_diario_engorde` pueda leerlo (v17); INV_OTRO es el saco de lo que la fn ignora.
    IF p_mt ILIKE 'AjusteCuadreTablaEntrada' THEN RETURN 'INV_AJUSTE_CUADRE_ENTRADA'; END IF;
    IF p_mt ILIKE 'AjusteCuadreTablaSalida'  THEN RETURN 'INV_AJUSTE_CUADRE_SALIDA';  END IF;
    RETURN 'INV_OTRO';
END;
$$;
""";

        /// <summary>La versión anterior, para el <c>Down</c>.</summary>
        private const string FnTipoEventoInventarioPrevia = """
CREATE OR REPLACE FUNCTION public.fn_tipo_evento_inventario(p_mt VARCHAR) RETURNS VARCHAR
LANGUAGE plpgsql
IMMUTABLE
AS $$
BEGIN
    IF p_mt IS NULL THEN RETURN 'INV_OTRO'; END IF;
    IF p_mt ILIKE 'Ingreso' THEN RETURN 'INV_INGRESO'; END IF;
    IF p_mt ILIKE 'TrasladoEntrada' OR p_mt ILIKE 'TrasladoInterGranjaEntrada' THEN RETURN 'INV_TRASLADO_ENTRADA'; END IF;
    IF p_mt ILIKE 'TrasladoSalida' OR p_mt ILIKE 'TrasladoInterGranjaSalida'
       OR p_mt ILIKE 'TrasladoInterGranjaPendiente' THEN RETURN 'INV_TRASLADO_SALIDA'; END IF;
    IF p_mt ILIKE 'Consumo' THEN RETURN 'INV_CONSUMO'; END IF;
    RETURN 'INV_OTRO';
END;
$$;
""";
    }
}
