using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// TK-2026-000019 — deja la unidad de medida del inventario alineada con el catálogo del ítem.
    /// </summary>
    /// <remarks>
    /// <b>El defecto.</b> <c>inventario_gestion_stock.unit</c> es una columna propia con
    /// <c>DEFAULT 'kg'</c> que nunca se sincronizó con <c>item_inventario_ecuador.unidad</c>. En el
    /// dump de producción del 14ago26 había <b>145 de 569</b> filas de stock mostrando kilos para
    /// productos que el catálogo tiene en litros, mililitros o unidades. El código ya quedó
    /// corregido (la unidad la resuelve <c>UnidadInventarioCalculos</c> desde el catálogo); esta
    /// migración limpia lo que quedó escrito.
    ///
    /// <b>Paso 1 — promoción al catálogo (solo ItalcolEcuador).</b> Hay 10 ítems donde el catálogo
    /// se quedó con el valor por defecto y la unidad REAL la escribió operación a mano sobre la
    /// fila de stock (por eso en la base conviven <c>LT</c>, <c>UND</c>, <c>GALONES</c>,
    /// <c>DOSIS</c>: el modal de ajuste aceptaba texto libre y lo usaban para tapar el «kg»). Esa
    /// corrección se sube al catálogo, que es donde vive la verdad, en vez de tirarla. Va con
    /// <c>WHERE unidad = '&lt;el valor por defecto de hoy&gt;'</c>: si alguien ya lo corrigió desde
    /// la pantalla de Ítems, no se pisa.
    ///
    /// Se limita a <b>company_id = 3</b> (ItalcolEcuador) a propósito: ItalcolPanamá tiene los
    /// mismos códigos clonados con la misma unidad por defecto, pero <b>0 divergencias</b> en su
    /// stock, o sea ninguna evidencia de qué unidad quiere esa operación. Decidir por ellos sería
    /// inventar el dato.
    ///
    /// <b>Paso 2 — alineación.</b> <c>inventario_gestion_stock.unit</c>,
    /// <c>inventario_gestion_movimiento.unit</c> y <c>lote_registro_historico_unificado.unidad</c>
    /// copian la del catálogo. Es <b>relabeling puro</b>: ninguna cantidad se convierte ni se toca.
    ///
    /// <b>Por qué no puede mover un saldo.</b> Todo ítem de alimento está en <c>kg</c> en el
    /// catálogo Y en el stock: <b>0 filas de alimento divergen en ninguna empresa</b>, así que
    /// ninguno de estos UPDATE las alcanza. La aritmética de alimento
    /// (<c>fn_seguimiento_diario_engorde</c>, <c>SaldoAlimentoEngordeAplicador</c>) es siempre en
    /// kilos y no lee esta columna. El gate está en
    /// <c>backend/sql/verificar_unidad_stock_catalogo.sql</c>.
    ///
    /// <b>El UPDATE del histórico no dispara triggers.</b> Los de
    /// <c>inventario_gestion_movimiento</c> son <c>AFTER INSERT</c>, <c>AFTER DELETE</c> y
    /// <c>AFTER UPDATE OF movement_type</c>; tocar <c>unit</c> no despierta a ninguno, así que el
    /// espejo se actualiza acá explícitamente.
    ///
    /// <b>Idempotencia:</b> todo va con <c>IS DISTINCT FROM</c> ⇒ la segunda pasada no afecta
    /// ninguna fila y no ensucia ningún <c>updated_at</c>.
    ///
    /// Migración DATA-ONLY: Designer clonado, ModelSnapshot intacto.
    /// </remarks>
    public partial class AlinearUnidadInventarioConCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Paso 1: la corrección manual sube al catálogo (solo ItalcolEcuador) ──────────
            migrationBuilder.Sql(@"
UPDATE item_inventario_ecuador i
   SET unidad     = v.unidad_real,
       updated_at = now()
  FROM (VALUES
        ('AV0357',   'kg',  'l'),      -- AV. VETRIBAC D SOLUCION 1LT
        ('AV0376',   'kg',  'l'),      -- AV. LARVIGEN 1LT 0%
        ('SM0009',   'kg',  'l'),      -- SM. ANTHIUM DIOXCIDE X 50 LT
        ('SM0047',   'kg',  'l'),      -- SM. CATALIZADOR X 20 LT
        ('SM0082',   'kg',  'l'),      -- SM. EXPECTORANLIPTUS 20 LT
        ('SM0128',   'kg',  'l'),      -- SM. NEUTRALIZANTE X 4 LT
        ('AV0444',   'kg',  'ml'),     -- AV. Q-NORFLOXAN 100ML
        ('SM0142',   'kg',  'und'),    -- SM. PASTILLAS DE CLORO 90% (pastillas)
        ('AV0512',   'kg',  'dosis'),  -- AV. VME NEWCASTLE LASOTA 1FCOx5000 DS
        ('CS953502', 'und', 'gal')     -- DIESEL
       ) AS v(codigo, unidad_esperada, unidad_real)
-- Empresa por NOMBRE, no por id: los ids no coinciden entre local y producción.
 WHERE i.company_id = (SELECT c.id FROM companies c WHERE c.name = 'ItalcolEcuador')
   AND i.codigo     = v.codigo
   -- Solo si el catálogo sigue como estaba: no pisa una corrección posterior del usuario.
   AND i.unidad     = v.unidad_esperada;
");

            // ── Paso 2: stock, movimientos e histórico copian la unidad del catálogo ─────────
            migrationBuilder.Sql(@"
UPDATE inventario_gestion_stock s
   SET unit       = COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg'),
       updated_at = now()
  FROM item_inventario_ecuador i
 WHERE i.id = s.item_inventario_ecuador_id
   AND COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg') IS DISTINCT FROM s.unit;
");

            migrationBuilder.Sql(@"
UPDATE inventario_gestion_movimiento m
   SET unit = COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg')
  FROM item_inventario_ecuador i
 WHERE i.id = m.item_inventario_ecuador_id
   AND COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg') IS DISTINCT FROM m.unit;
");

            // El espejo del histórico unificado: lo llena un trigger AFTER INSERT, así que un
            // UPDATE del origen no se propaga solo (regla dura de CLAUDE.md).
            migrationBuilder.Sql(@"
UPDATE lote_registro_historico_unificado h
   SET unidad = COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg')
  FROM item_inventario_ecuador i
 WHERE i.id = h.item_inventario_ecuador_id
   AND h.unidad IS NOT NULL
   AND COALESCE(NULLIF(TRIM(i.unidad), ''), 'kg') IS DISTINCT FROM h.unidad;
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>No revierte.</b> El estado anterior era la divergencia misma (cada fila con la unidad
        /// que le tocó por el camino que la creó): no hay nada que restaurar y restaurarlo sería
        /// devolver el defecto que reporta el ticket. Si hubiera que deshacer la promoción del
        /// catálogo, se hace desde la pantalla de Ítems de inventario, que es donde vive esa
        /// decisión.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
