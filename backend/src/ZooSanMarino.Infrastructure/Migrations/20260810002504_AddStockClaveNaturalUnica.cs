using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// A1 de <c>fase_de_desarrollo/f0a_stock_atomico_plan.md</c>: la clave natural de
    /// <c>inventario_gestion_stock</c> pasa a ser ÚNICA.
    ///
    /// <para>
    /// <b>El bug que cierra.</b> El índice de la clave natural no era único y todos los caminos de
    /// escritura hacen buscar-o-insertar (<c>FirstOrDefaultAsync</c> → <c>if (null) Add(...)</c>). Dos
    /// escrituras concurrentes sobre la misma ubicación e ítem no encuentran fila y **ambas insertan**;
    /// a partir de ahí hay dos filas para la misma clave y todas las lecturas usan
    /// <c>FirstOrDefault</c>, así que la segunda queda **invisible para siempre**. El inventario
    /// muestra menos de lo que hay y el faltante no aparece en ningún reporte, porque la fila existe y
    /// nadie la mira. Es reproducible hoy con dos pestañas del navegador.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué la consolidación va ANTES y en la misma migración.</b> En prod
    /// <c>Database__RunMigrations=true</c>: esto corre al arrancar el contenedor. Un
    /// <c>CREATE UNIQUE INDEX</c> contra duplicados vivos falla, la migración muere y el arranque entra
    /// en el modo de falla documentado en CLAUDE.md (exit 139 / rollback silencioso de ECS). La BD
    /// local (refresh del dump de prod) tenía 0 duplicados al escribir esto, pero prod sigue operando
    /// y la migración no puede apostar a eso.
    /// </para>
    /// </summary>
    public partial class AddStockClaveNaturalUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1) Consolidar duplicados (idempotente: no-op si no hay) ─────────────────
            //
            // Se SUMA y se conserva MIN(id). Sumar es lo correcto, no una elección conservadora:
            // las filas duplicadas contienen stock real que entró por caminos distintos, y la fila
            // invisible representa mercadería que está físicamente en la granja. Quedarse con una y
            // descartar la otra borraría existencias reales.
            //
            // Verificado antes de escribir esto: NINGUNA tabla tiene FK contra
            // inventario_gestion_stock.id, así que borrar las filas absorbidas no deja nada colgando.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_grupos   int;
    v_borradas int;
BEGIN
    SELECT count(*) INTO v_grupos FROM (
        SELECT 1 FROM inventario_gestion_stock
        GROUP BY farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id,''), COALESCE(galpon_id,'')
        HAVING count(*) > 1
    ) t;

    IF v_grupos = 0 THEN
        RAISE NOTICE '[A1] Sin duplicados de clave natural: nada que consolidar.';
        RETURN;
    END IF;

    -- El ganador de cada grupo se queda con el total del grupo.
    WITH grupos AS (
        SELECT min(id) AS id_ganador, sum(quantity) AS total
        FROM inventario_gestion_stock
        GROUP BY farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id,''), COALESCE(galpon_id,'')
        HAVING count(*) > 1
    )
    UPDATE inventario_gestion_stock s
       SET quantity = g.total, updated_at = now()
      FROM grupos g
     WHERE s.id = g.id_ganador;

    -- Se borran las absorbidas. `min(id)` sobre una PK nunca es NULL, así que el NOT IN es seguro.
    WITH ganadores AS (
        SELECT min(id) AS id_ganador
        FROM inventario_gestion_stock
        GROUP BY farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id,''), COALESCE(galpon_id,'')
    )
    DELETE FROM inventario_gestion_stock s
     WHERE s.id NOT IN (SELECT id_ganador FROM ganadores);
    GET DIAGNOSTICS v_borradas = ROW_COUNT;

    -- Queda en el log del arranque del contenedor: si esto aparece en prod, hubo corrupción
    -- silenciosa y conviene auditar qué granjas estaban afectadas.
    RAISE NOTICE '[A1] Consolidados % grupo(s) duplicado(s); % fila(s) absorbida(s).', v_grupos, v_borradas;
END $$;
");

            // ── 2) Índice ÚNICO de expresión ───────────────────────────────────────────
            //
            // El COALESCE no es cosmético: en Postgres, dentro de un índice único, NULL nunca es
            // igual a otro NULL. Sin él, todo el stock a NIVEL GRANJA (núcleo y galpón nulos — el
            // modelo entero de Colombia y de las granjas con maneja_alimento_por_galpon = false) se
            // podría seguir duplicando, que es justo lo que esta migración viene a cerrar.
            //
            // Va por Sql() porque EF no sabe expresar un índice de expresión; por eso tampoco
            // aparece en el ModelSnapshot. `IF NOT EXISTS` lo hace idempotente.
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ux_inventario_gestion_stock_clave_natural
    ON inventario_gestion_stock
    (farm_id, item_inventario_ecuador_id, COALESCE(nucleo_id, ''), COALESCE(galpon_id, ''));
");

            // NOTA: se CONSERVA a propósito el índice no único
            // ix_inventario_gestion_stock_farm_item_nucleo_galpon. El único de expresión no puede
            // resolver las igualdades sobre nucleo_id/galpon_id que hacen las consultas del service
            // (`x.NucleoId == nucleoId`), así que quitarlo sería una regresión de plan de consultas.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se revierte el índice. La consolidación NO se puede deshacer —y no debería:
            // las filas absorbidas eran duplicados que ninguna lectura veía, y su cantidad quedó
            // sumada en la fila superviviente. Recrearlas reintroduciría stock invisible.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ux_inventario_gestion_stock_clave_natural;");
        }
    }
}
