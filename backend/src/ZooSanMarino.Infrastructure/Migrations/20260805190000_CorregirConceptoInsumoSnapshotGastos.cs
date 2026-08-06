using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only) de <c>inventario_gasto_detalle.concepto</c>: alinea con el
    /// concepto de su ítem las líneas de gasto que quedaron con un <c>tipo_item</c> guardado en la
    /// columna <c>concepto</c>.
    ///
    /// <para>
    /// <b>El síntoma.</b> El desplegable «Concepto» de Gastos de inventario se arma desde el catálogo
    /// (<c>InventarioGastoService.GetConceptosAsync</c>) mientras el filtro compara con igualdad
    /// EXACTA sobre el snapshot (<c>fn_inventario_gastos_search</c> y <c>InventarioGastoService</c>).
    /// Una línea cuyo concepto ya no existe en el catálogo de su empresa es <b>infiltrable</b>: no hay
    /// opción que la traiga, y en la tabla y en el Excel del reporte sale con una etiqueta distinta a
    /// la de su ítem.
    /// </para>
    ///
    /// <para>
    /// <b>El origen (investigado, no supuesto).</b> En el dump del 05-ago-2026 son 10 líneas con
    /// <c>concepto = 'insumo'</c> sobre el ítem <c>AV0351 · AV. LIV 52 PROTEC 5 LTR</c> de la empresa
    /// 3, creadas por pantalla entre el 2026-07-14 y el 2026-07-27 por 4 usuarios distintos. No fue un
    /// seed ni una carga: <c>inventario_gasto_detalle</c> tiene un solo escritor
    /// (<c>InventarioGastoService.CreateAsync</c>), que desde su commit inicial (<c>b6f5d16</c>,
    /// 2026-03-25) valida <c>item.Concepto == req.Concepto</c> y guarda <c>Concepto = item.Concepto</c>
    /// — con ese código las filas son imposibles de crear salvo que el CATÁLOGO dijera <c>insumo</c>.
    /// Y lo decía: la migración <c>20260717192803_SeedItemInventarioPanamaDesdeEcuador</c> clonó ese
    /// catálogo el 2026-07-17 con un <c>INSERT ... SELECT src.concepto</c> sin transformar, y su copia
    /// de AV0351 conserva <c>insumo</c> — la única divergencia entre los 148 códigos compartidos.
    /// <c>insumo</c> nunca fue un concepto de negocio: es un <c>tipo_item</c> (29 ítems de esa empresa
    /// lo tienen) que quedó cargado en la columna equivocada. El catálogo ya se corrigió a
    /// <c>Otros insumos</c> el 2026-07-27 por fuera de la aplicación (su <c>updated_at</c> nunca se
    /// movió), dejando el snapshot atrás. Esta migración termina esa corrección.
    /// </para>
    ///
    /// <para>
    /// <b>La regla es dinámica</b> (no nombra ids, empresas ni etiquetas de negocio) y solo alcanza a
    /// una línea cuando se cumple TODO:
    /// <list type="number">
    ///   <item>su ítem tiene hoy un concepto no vacío al que alinearse;</item>
    ///   <item>el concepto de la línea difiere <b>en valor</b> del de su ítem — no solo en
    ///         capitalización, ese caso lo resuelve la regla 2 de
    ///         <c>20260805180000_NormalizarConceptoCatalogoInventario</c> y acá queda intacto;</item>
    ///   <item>ese concepto <b>no existe</b> en el catálogo de la empresa del gasto ⇒ está probado que
    ///         el desplegable no lo ofrece y la línea es infiltrable;</item>
    ///   <item>pero <b>sí existe como <c>tipo_item</c></b> en ese catálogo ⇒ es el defecto conocido
    ///         (un <c>tipo_item</c> colado en la columna) y no una categoría de negocio retirada, que
    ///         sí sería historia legítima y no se toca.</item>
    /// </list>
    /// Si en producción los datos no cumplen las cuatro, la fila simplemente no entra.
    /// <c>IS DISTINCT FROM</c> ⇒ re-ejecutarla da <c>UPDATE 0</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Efecto medido</b> (simulado en transacción + <c>ROLLBACK</c> antes de escribir nada):
    /// 10 filas; segunda pasada <c>UPDATE 0</c>; <c>Otros insumos</c> 196 → 206 e <c>insumo</c>
    /// desaparece; el total de líneas de la tabla no cambia; el detector (consulta 4 de
    /// <c>backend/sql/verificar_conceptos_catalogo_inventario.sql</c>) pasa de 10 líneas a 0.
    /// </para>
    ///
    /// <para>
    /// <b>Irreversible por diseño:</b> el valor anterior no se guarda en ningún lado, así que
    /// <c>Down()</c> no restaura (mismo criterio que la migración hermana). Queda el rastro en
    /// <c>inventario_gasto_auditoria</c>, cuyo payload de creación conserva el concepto original.
    /// </para>
    /// </summary>
    public partial class CorregirConceptoInsumoSnapshotGastos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE inventario_gasto_detalle AS d
SET concepto = i.concepto
FROM inventario_gasto AS g, item_inventario_ecuador AS i
WHERE g.id = d.inventario_gasto_id
  AND i.id = d.item_inventario_ecuador_id
  -- (1) el item tiene hoy un concepto al que alinearse
  AND i.concepto IS NOT NULL
  AND btrim(i.concepto) <> ''
  -- (2) difiere en VALOR, no solo en capitalizacion (ese caso es de la migracion hermana)
  AND lower(btrim(coalesce(d.concepto, ''))) IS DISTINCT FROM lower(btrim(coalesce(i.concepto, '')))
  -- (3) ese concepto NO existe en el catalogo de la empresa del gasto => la linea es infiltrable
  AND NOT EXISTS (
        SELECT 1
        FROM item_inventario_ecuador AS c
        WHERE c.company_id = g.company_id
          AND lower(btrim(coalesce(c.concepto, ''))) = lower(btrim(coalesce(d.concepto, '')))
  )
  -- (4) pero SI existe como tipo_item => es un tipo_item colado en la columna, no una categoria real
  AND EXISTS (
        SELECT 1
        FROM item_inventario_ecuador AS t
        WHERE t.company_id = g.company_id
          AND lower(btrim(coalesce(t.tipo_item, ''))) = lower(btrim(coalesce(d.concepto, '')))
  )
  -- idempotencia
  AND d.concepto IS DISTINCT FROM i.concepto;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atras: el concepto anterior de cada linea no se guarda en ninguna columna.
            // El rastro queda en inventario_gasto_auditoria (payload de la accion 'Crear').
        }
    }
}
