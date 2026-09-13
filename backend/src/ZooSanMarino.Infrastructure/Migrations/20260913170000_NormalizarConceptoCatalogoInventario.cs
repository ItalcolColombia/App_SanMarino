using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Corrección de DATOS (data-only) de la columna <c>concepto</c> del catálogo de inventario:
    /// el mismo concepto convive escrito con distinta capitalización y el desplegable «Concepto» de
    /// Gestión de Inventario lo lista DOS veces, con opciones que devuelven exactamente las mismas
    /// filas (todos los filtros del módulo comparan normalizado; solo las LISTAS usaban igualdad
    /// exacta).
    ///
    /// <para>
    /// En el dump del 05-ago-2026: <c>Otros insumos</c> (36 ítems) junto a <c>Otros Insumos</c> (6)
    /// en la empresa 3, y lo mismo (34 / 6) en la 5 — sus catálogos son clones (la 5 se sembró el
    /// 2026-07-17 desde la 3).
    /// </para>
    ///
    /// <para>
    /// <b>Las tres reglas son dinámicas</b> (no nombran ids, empresas ni etiquetas de negocio): si en
    /// producción los datos difieren, la fila simplemente no entra. Todas usan
    /// <c>IS DISTINCT FROM</c> ⇒ re-ejecutarlas da <c>UPDATE 0</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Regla 1 — capitalización, POR EMPRESA.</b> Para cada grupo de conceptos que solo difieren
    /// en mayúsculas/espacios se conserva la variante MÁS USADA (empate ⇒ la que ordena primero) y se
    /// reescriben las demás. Es por empresa a propósito: el catálogo se sirve acotado a la empresa
    /// activa (fail-closed), así que un duplicado solo existe dentro de una misma empresa. Hacerlo
    /// global CREARÍA uno nuevo: el único ítem con <c>concepto = 'alimento'</c> vive en una empresa
    /// donde los otros 61 ítems no tienen concepto y caen a <c>tipo_item = 'alimento'</c> por el
    /// <c>Concepto ?? TipoItem</c>; capitalizarlo a <c>Alimento</c> pondría las dos opciones en ese
    /// desplegable. Por empresa, ese ítem queda intacto.
    /// </para>
    ///
    /// <para>
    /// <b>Regla 2 — el snapshot de Gastos de inventario.</b> <c>inventario_gasto_detalle.concepto</c>
    /// es una copia del concepto del ítem al momento del consumo y se filtra con igualdad EXACTA
    /// (<c>fn_inventario_gastos_search</c> y <c>InventarioGastoService</c>), mientras el desplegable
    /// de ese módulo se arma desde el catálogo. Si se normaliza el catálogo sin tocar el snapshot,
    /// las líneas que quedaron con la capitalización vieja dejan de ser filtrables. Solo se alinean
    /// las que coinciden en minúsculas con el concepto de SU ítem: una diferencia real de valor no
    /// entra, así que la regla nunca recategoriza un consumo histórico.
    /// </para>
    ///
    /// <para>
    /// <b>Regla 3 — el concepto suelto.</b> Un mismo <c>codigo</c> con conceptos divergentes entre
    /// empresas (catálogos clonados que deberían coincidir) adopta el concepto más usado del
    /// catálogo. Guardas: el perdedor tiene que ser marginal (≤ 1 ítem en toda la base) y el ganador
    /// estar usado por varios. Alcanza al ítem <c>AV0351 · AV. LIV 52 PROTEC 5 LTR</c>, que tiene
    /// <c>concepto = 'insumo'</c> (un <c>tipo_item</c> cargado en la columna equivocada) en una
    /// empresa y <c>Otros insumos</c> en la otra — la ÚNICA divergencia entre los dos catálogos.
    /// </para>
    ///
    /// <para>
    /// <b>Efecto medido</b> (simulado en transacción + <c>ROLLBACK</c> antes de escribir nada):
    /// regla 1 → 12 filas (6 + 6), regla 2 → 0, regla 3 → 1. Después: cero grupos duplicados por
    /// empresa, cero códigos divergentes y el conteo de ítems por empresa sin cambios.
    /// </para>
    ///
    /// <para>
    /// <b>Fuera de alcance a propósito.</b> (a) Los 167 ítems con <c>concepto IS NULL</c> y
    /// <c>tipo_item = 'alimento'</c>: el fallback los resuelve bien y en Colombia el concepto no
    /// aplica por diseño (ver <c>backend/sql/migracion_inventario_colombia_01_items.sql</c>);
    /// rellenarlos cambiaría el <c>itemType</c> que hoy devuelve el API. (b) 10 líneas de gasto con
    /// <c>concepto = 'insumo'</c> apuntando a un ítem cuyo concepto siempre fue <c>Otros insumos</c>:
    /// corregirlas sería reescribir una categorización histórica sobre una hipótesis.
    /// </para>
    ///
    /// <para>
    /// <b>Port del 13-sep-2026.</b> Nació como <c>20260805180000</c> en una rama que nunca llegó a
    /// <c>main</c>. Al integrarla se re-timestampeó para ordenar después de todo lo desplegado y se
    /// corrigieron los nombres: el rename neutro dejó <c>item_inventario</c> /
    /// <c>item_inventario_id</c> (antes <c>item_inventario_ecuador</c>). Re-medido sobre la copia
    /// local de producción, en transacción revertida: regla 1 → 12, regla 2 → 1, regla 3 → 1.
    /// </para>
    ///
    /// <para>
    /// <b><c>Down()</c> es no-op deliberado</b>: no se puede reconstruir qué fila tenía cuál
    /// capitalización, y volver atrás reintroduciría el duplicado en pantalla. Copia trazable del SQL
    /// en <c>backend/sql/normalizar_concepto_catalogo_inventario.sql</c>; el reporte de control, en
    /// <c>backend/sql/verificar_conceptos_catalogo_inventario.sql</c>.
    /// </para>
    /// </summary>
    public partial class NormalizarConceptoCatalogoInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Regla 1: por empresa, gana la variante más usada de cada concepto.
            migrationBuilder.Sql("""
                WITH variantes AS (
                    SELECT company_id,
                           lower(btrim(concepto)) AS norm,
                           btrim(concepto)        AS etiqueta,
                           count(*)               AS usos
                    FROM item_inventario
                    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
                    GROUP BY 1, 2, 3
                ), canonico AS (
                    SELECT DISTINCT ON (company_id, norm) company_id, norm, etiqueta
                    FROM variantes
                    ORDER BY company_id, norm, usos DESC, etiqueta
                )
                UPDATE item_inventario i
                SET concepto = c.etiqueta, updated_at = now()
                FROM canonico c
                WHERE c.company_id = i.company_id
                  AND c.norm = lower(btrim(i.concepto))
                  AND i.concepto IS DISTINCT FROM c.etiqueta;
                """);

            // ── Regla 2: el snapshot de gastos sigue al catálogo, SOLO en la capitalización.
            migrationBuilder.Sql("""
                UPDATE inventario_gasto_detalle d
                SET concepto = i.concepto
                FROM item_inventario i
                WHERE i.id = d.item_inventario_id
                  AND d.concepto IS NOT NULL AND btrim(d.concepto) <> ''
                  AND i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
                  AND lower(btrim(d.concepto)) = lower(btrim(i.concepto))
                  AND d.concepto IS DISTINCT FROM i.concepto;
                """);

            // ── Regla 3: mismo código con conceptos divergentes ⇒ gana el más usado del catálogo.
            migrationBuilder.Sql("""
                WITH usos AS (
                    SELECT lower(btrim(concepto)) AS norm, count(*) AS n
                    FROM item_inventario
                    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
                    GROUP BY 1
                ), divergentes AS (
                    SELECT codigo
                    FROM item_inventario
                    WHERE concepto IS NOT NULL AND btrim(concepto) <> ''
                    GROUP BY codigo
                    HAVING count(DISTINCT lower(btrim(concepto))) > 1
                ), ganador AS (
                    SELECT DISTINCT ON (i.codigo) i.codigo, btrim(i.concepto) AS etiqueta, u.n
                    FROM item_inventario i
                    JOIN divergentes d ON d.codigo = i.codigo
                    JOIN usos u        ON u.norm   = lower(btrim(i.concepto))
                    WHERE i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
                    ORDER BY i.codigo, u.n DESC, btrim(i.concepto)
                )
                UPDATE item_inventario i
                SET concepto = g.etiqueta, updated_at = now()
                FROM ganador g
                WHERE g.codigo = i.codigo
                  AND i.concepto IS NOT NULL AND btrim(i.concepto) <> ''
                  AND lower(btrim(i.concepto)) <> lower(btrim(g.etiqueta))
                  -- el ganador tiene que ser un concepto establecido...
                  AND g.n > 1
                  -- ...y el perdedor, marginal (si las dos formas se usan, no hay evidencia de cuál manda)
                  AND (SELECT u2.n FROM usos u2 WHERE u2.norm = lower(btrim(i.concepto))) <= 1
                  AND i.concepto IS DISTINCT FROM g.etiqueta;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: el Up() unifica capitalizaciones equivalentes; no hay forma de saber
            // qué fila tenía cuál, y revertirlo devolvería el duplicado al desplegable. El detalle de
            // lo que se cambió queda en backend/sql/normalizar_concepto_catalogo_inventario.sql.
        }
    }
}
