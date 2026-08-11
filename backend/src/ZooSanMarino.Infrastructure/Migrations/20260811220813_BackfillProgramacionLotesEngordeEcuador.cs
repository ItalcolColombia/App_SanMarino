using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Migración DATA-ONLY: deja la programación de <b>ItalcolEcuador</b> al día con lo que ya opera,
    /// para que el flag <c>programacion_lotes_engorde</c> no arranque con la lista vacía.
    /// <list type="number">
    /// <item>Crea un <b>lote base por corrida</b> ya usada. En Ecuador el nombre del lote ES la corrida
    /// (año + número: <c>2601</c>, <c>2602</c>, <c>2603</c>, <c>2604</c>), así que los bases se derivan
    /// de los nombres existentes — no se inventa nomenclatura.</item>
    /// <item><b>Amarra</b> cada lote vivo a su base por nombre.</item>
    /// <item>Numera la <b>corrida</b> por (base, galpón). Es imprescindible: sin ella
    /// <c>MAX(numero_corrida)</c> sería NULL y el próximo lote del mismo base en ese galpón volvería a
    /// llamarse <c>2603</c>, duplicando el nombre dentro del galpón. Con el backfill, el siguiente sale
    /// <c>2603 - 2</c>.</item>
    /// <item><b>Asigna las granjas</b> a cada base según dónde se encasetó de hecho — que es la
    /// visibilidad que el selector necesita al crear un lote.</item>
    /// </list>
    /// <para>
    /// Sólo toca ItalcolEcuador y sólo filas vivas; los nombres que no son corrida (<c>^[0-9]{4}$</c>)
    /// y los lotes borrados quedan intactos. Todo idempotente (<c>NOT EXISTS</c> + guardas
    /// <c>IS NULL</c>): re-ejecutarla no duplica bases ni asignaciones ni re-numera lo ya numerado.
    /// </para>
    /// </summary>
    public partial class BackfillProgramacionLotesEngordeEcuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_company integer;
    v_user    integer;
BEGIN
    SELECT id INTO v_company FROM companies WHERE name = 'ItalcolEcuador';
    IF v_company IS NULL THEN
        RETURN;   -- la empresa no existe en este entorno: nada que hacer
    END IF;

    -- Autor de las filas creadas: el mismo criterio del módulo (un usuario real de la empresa),
    -- con 0 como último recurso (la columna es NOT NULL DEFAULT 0).
    SELECT created_by_user_id INTO v_user
      FROM lote_base_engorde WHERE company_id = v_company ORDER BY id LIMIT 1;
    IF v_user IS NULL THEN
        SELECT created_by_user_id INTO v_user
          FROM lote_ave_engorde
         WHERE company_id = v_company AND created_by_user_id IS NOT NULL
         ORDER BY lote_ave_engorde_id DESC LIMIT 1;
    END IF;
    v_user := COALESCE(v_user, 0);

    -- 1) Un lote base por corrida ya usada (2601, 2602, …). fecha_activacion = primer encaset de esa
    --    corrida (dato informativo; ya no controla vigencia).
    INSERT INTO lote_base_engorde (nombre, company_id, activo, created_by_user_id, created_at, fecha_activacion)
    SELECT l.lote_nombre,
           v_company,
           true,
           v_user,
           now(),
           MIN(l.fecha_encaset)::date
      FROM lote_ave_engorde l
     WHERE l.company_id = v_company
       AND l.deleted_at IS NULL
       AND l.lote_nombre ~ '^[0-9]{4}$'
     GROUP BY l.lote_nombre
       HAVING NOT EXISTS (
            SELECT 1 FROM lote_base_engorde b
             WHERE b.company_id = v_company
               AND b.deleted_at IS NULL
               AND lower(b.nombre) = lower(l.lote_nombre)
       );

    -- 2) Amarrar cada lote vivo a su lote base (por nombre). Sólo los que aún no tienen base.
    UPDATE lote_ave_engorde l
       SET lote_base_engorde_id = b.id
      FROM lote_base_engorde b
     WHERE b.company_id = v_company
       AND b.deleted_at IS NULL
       AND l.company_id = v_company
       AND l.deleted_at IS NULL
       AND l.lote_base_engorde_id IS NULL
       AND lower(l.lote_nombre) = lower(b.nombre);

    -- 3) Número de corrida por (base, galpón), continuando después del máximo ya existente para no
    --    reusar números. Un galpón con una sola apertura de la corrida queda en 1 ⇒ la próxima sale 2.
    WITH numerados AS (
        SELECT l.lote_ave_engorde_id AS id,
               COALESCE(mx.max_corrida, 0)
                 + ROW_NUMBER() OVER (
                     PARTITION BY l.lote_base_engorde_id, l.galpon_id
                     ORDER BY l.fecha_encaset NULLS LAST, l.lote_ave_engorde_id
                   ) AS n
          FROM lote_ave_engorde l
          LEFT JOIN LATERAL (
                SELECT MAX(l2.numero_corrida) AS max_corrida
                  FROM lote_ave_engorde l2
                 WHERE l2.company_id = l.company_id
                   AND l2.lote_base_engorde_id = l.lote_base_engorde_id
                   AND l2.galpon_id IS NOT DISTINCT FROM l.galpon_id
          ) mx ON TRUE
         WHERE l.company_id = v_company
           AND l.deleted_at IS NULL
           AND l.lote_base_engorde_id IS NOT NULL
           AND l.numero_corrida IS NULL
    )
    UPDATE lote_ave_engorde l
       SET numero_corrida = numerados.n
      FROM numerados
     WHERE l.lote_ave_engorde_id = numerados.id;

    -- 4) Visibilidad: cada base queda asignado a las granjas donde de hecho se encasetó.
    INSERT INTO lote_base_engorde_granja (lote_base_engorde_id, farm_id, company_id, created_by_user_id, created_at)
    SELECT DISTINCT l.lote_base_engorde_id, l.granja_id, v_company, v_user, now()
      FROM lote_ave_engorde l
     WHERE l.company_id = v_company
       AND l.deleted_at IS NULL
       AND l.lote_base_engorde_id IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM lote_base_engorde_granja g
             WHERE g.lote_base_engorde_id = l.lote_base_engorde_id
               AND g.farm_id = l.granja_id
       );
END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op deliberado: es un backfill de datos operativos. Revertirlo automáticamente
            // borraría bases que para entonces pueden tener gastos o lotes nuevos colgando. Para
            // apagar la feature alcanza con revertir el seed del flag (SeedProgramacionLotesEngordeEcuador).
        }
    }
}
