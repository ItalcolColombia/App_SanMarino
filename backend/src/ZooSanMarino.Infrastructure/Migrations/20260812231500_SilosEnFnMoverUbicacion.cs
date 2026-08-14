using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// FASE A — las funciones de mover ubicación aprenden a arrastrar los silos.
    ///
    /// <para>
    /// <b>El defecto que cierra.</b> <c>galpon_silos</c> guarda el trío
    /// <c>(granja_id, nucleo_id, galpon_id)</c> denormalizado. <c>fn_mover_galpon</c> y
    /// <c>fn_rekey_nucleo</c> reescriben ese trío en todas las tablas que lo llevan, pero no conocían
    /// esta: mover un galpón de núcleo dejaba sus silos apuntando al núcleo viejo y el galpón se
    /// quedaba <b>sin ubicaciones que ofrecer</b>, sin ningún error que lo explicara. Es exactamente
    /// el mismo agujero que ya costó una migración de fix con <c>nucleos.codigo_bodega</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Y uno peor, que aparece al mover ENTRE GRANJAS.</b> Los silos son de la granja. Si el galpón
    /// (o el núcleo, o el lote) se muda a otra granja, sus silos siguen siendo de la granja vieja: la
    /// asignación queda cruzada y violaría el invariante «el silo es de la misma granja». No se puede
    /// repuntar —en la granja destino esos silos no existen—, así que la asignación se <b>quita</b>.
    /// El usuario reasigna silos en la granja nueva, que es la única respuesta correcta.
    /// </para>
    ///
    /// <para>
    /// Los movimientos ya registrados NO se tocan: cada uno guarda su propio silo y su histórico
    /// tiene que seguir diciendo dónde pasó realmente.
    /// </para>
    /// </summary>
    public partial class SilosEnFnMoverUbicacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── fn_mover_galpon ──────────────────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_mover_galpon(
    p_galpon_id    varchar,
    p_granja_dest  integer,
    p_nucleo_dest  varchar,
    p_user_id      integer
) RETURNS void
LANGUAGE plpgsql
AS $fn$
BEGIN
    -- El galpón en sí
    UPDATE public.galpones
       SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest,
           updated_by_user_id = p_user_id, updated_at = now()
     WHERE galpon_id = p_galpon_id;

    -- Tablas con granja_id + nucleo_id + galpon_id
    UPDATE public.lotes                     SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.lote_postura_levante      SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.lote_postura_produccion   SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.lote_ave_engorde          SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.historial_inventario      SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.inventario_aves           SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.produccion_lotes          SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.vacunacion_cronograma_item SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;

    -- Tablas con nucleo_id + galpon_id (sin granja_id)
    UPDATE public.inventario_gasto                 SET nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.inventario_gestion_movimiento    SET nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.inventario_gestion_stock         SET nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;
    UPDATE public.lote_registro_historico_unificado SET nucleo_id = p_nucleo_dest WHERE galpon_id = p_galpon_id;

    -- Asignación de silos del galpón: sigue al galpón...
    UPDATE public.galpon_silos
       SET granja_id = p_granja_dest, nucleo_id = p_nucleo_dest
     WHERE galpon_id = p_galpon_id;

    -- ...y se limpia la que quedó cruzada de granja (el silo es de la granja vieja).
    DELETE FROM public.galpon_silos gs
     USING public.farm_silos fs
     WHERE gs.farm_silo_id = fs.id
       AND gs.granja_id   <> fs.granja_id;

    -- Ídem para los lotes que se mudaron con el galpón.
    DELETE FROM public.lote_silos ls
     USING public.farm_silos fs, public.lotes l
     WHERE ls.farm_silo_id = fs.id
       AND ls.lote_id      = l.lote_id
       AND fs.granja_id   <> l.granja_id;

    -- lesiones, lote_galpones, plan_gramaje_galpon: solo galpon_id → siguen por FK, nada que reescribir.
END;
$fn$;
");

            // ── fn_rekey_nucleo ──────────────────────────────────────────────────
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_rekey_nucleo(
    p_nucleo_id      varchar,
    p_granja_origen  integer,
    p_granja_dest    integer,
    p_user_id        integer
) RETURNS void
LANGUAGE plpgsql
AS $fn$
BEGIN
    IF p_granja_origen = p_granja_dest THEN
        RAISE EXCEPTION 'La granja destino es la misma que la de origen.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.nucleos WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen) THEN
        RAISE EXCEPTION 'El núcleo % no existe en la granja origen %.', p_nucleo_id, p_granja_origen;
    END IF;

    IF EXISTS (SELECT 1 FROM public.nucleos WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_dest) THEN
        RAISE EXCEPTION 'Ya existe un núcleo con Id % en la granja destino %. Renómbrelo antes de mover.', p_nucleo_id, p_granja_dest;
    END IF;

    -- 2) Insertar el núcleo destino (copia; conserva auditoría de creación)
    --    ⚠ Lista explícita: toda columna nueva de `nucleos` DEBE sumarse aquí (y re-crearse
    --    la función vía migración) o su valor se pierde en silencio al mover el núcleo.
    INSERT INTO public.nucleos
        (nucleo_id, granja_id, nucleo_nombre, company_id, codigo_bodega, descripcion_bodega,
         created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at)
    SELECT nucleo_id, p_granja_dest, nucleo_nombre, company_id, codigo_bodega, descripcion_bodega,
           created_by_user_id, created_at, p_user_id, now(), deleted_at
      FROM public.nucleos
     WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;

    -- 3) Repuntar hijos (tablas con nucleo_id + granja_id): granja origen → destino
    UPDATE public.galpones                  SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lotes                     SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_postura_levante      SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_postura_produccion   SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_ave_engorde          SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.historial_inventario      SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.inventario_aves           SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.produccion_lotes          SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.vacunacion_cronograma_item SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    -- Tablas con nucleo_id sin granja_id (inventario_gasto, inventario_gestion_*, lote_registro_historico_unificado):
    -- no guardan granja → el nucleo_id no cambia, siguen al núcleo automáticamente.

    -- Los galpones del núcleo cambiaron de granja: sus silos eran de la granja ORIGEN y allá se
    -- quedan (son ubicaciones físicas de esa granja). La asignación se quita en vez de repuntarse:
    -- en la granja destino esos silos no existen. El usuario reasigna en la granja nueva.
    UPDATE public.galpon_silos
       SET granja_id = p_granja_dest
     WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;

    DELETE FROM public.galpon_silos gs
     USING public.farm_silos fs
     WHERE gs.farm_silo_id = fs.id
       AND gs.granja_id   <> fs.granja_id;

    DELETE FROM public.lote_silos ls
     USING public.farm_silos fs, public.lotes l
     WHERE ls.farm_silo_id = fs.id
       AND ls.lote_id      = l.lote_id
       AND fs.granja_id   <> l.granja_id;

    -- 4) Borrar el núcleo origen (ya sin hijos apuntando)
    DELETE FROM public.nucleos WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
END;
$fn$;
");

            // ── fn_mover_lote ────────────────────────────────────────────────────
            // Un lote que se muda a otra granja también deja atrás sus silos. Se recrea la función
            // COMPLETA (cuerpo verbatim + el saneamiento al final): hacer cirugía de texto sobre
            // pg_get_functiondef sería más corto y mucho menos predecible.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_mover_lote(
    p_lote_id      integer,
    p_granja_dest  integer,
    p_nucleo_dest  varchar,
    p_galpon_dest  varchar,
    p_user_id      integer
) RETURNS void
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_granja_origen integer;
    v_company_id    integer;
BEGIN
    SELECT granja_id, company_id
      INTO v_granja_origen, v_company_id
      FROM public.lotes
     WHERE lote_id = p_lote_id;

    UPDATE public.lotes
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id;

    UPDATE public.lote_postura_levante
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    UPDATE public.lote_postura_produccion
       SET granja_id = p_granja_dest,
           nucleo_id = p_nucleo_dest,
           galpon_id = p_galpon_dest,
           updated_by_user_id = p_user_id,
           updated_at = now()
     WHERE lote_id = p_lote_id AND deleted_at IS NULL;

    -- Solo si REALMENTE cambió de granja: mover de galpón dentro de la misma granja no
    -- es un traslado y ensuciaría el historial. lote_nuevo_id = lote_original_id porque
    -- este camino reubica el MISMO lote (mismo criterio que TrasladarLoteAsync).
    IF v_granja_origen IS NOT NULL AND v_granja_origen IS DISTINCT FROM p_granja_dest THEN
        INSERT INTO public.historial_traslado_lote (
            lote_original_id, lote_nuevo_id,
            granja_origen_id, granja_destino_id,
            nucleo_destino_id, galpon_destino_id,
            observaciones, company_id, created_by_user_id, created_at)
        VALUES (
            p_lote_id, p_lote_id,
            v_granja_origen, p_granja_dest,
            p_nucleo_dest, p_galpon_dest,
            'Movimiento de ubicación', COALESCE(v_company_id, 0),
            COALESCE(p_user_id, 0), CURRENT_TIMESTAMP);
    END IF;

    -- Silos del lote: si se mudó de granja, los que tenía son de la granja vieja y ya no
    -- pueden alimentarlo. Se quitan; el usuario reasigna en la granja nueva.
    DELETE FROM public.lote_silos ls
     USING public.farm_silos fs
     WHERE ls.lote_id      = p_lote_id
       AND ls.farm_silo_id = fs.id
       AND fs.granja_id   <> p_granja_dest;
END;
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hay reversa razonable: quitar el arrastre de silos reintroduciría el defecto (silos
            // huérfanos y asignaciones cruzadas de granja). Las sentencias agregadas son inocuas para
            // las empresas sin silos —no hay filas que borrar—, así que se dejan puestas.
        }
    }
}
