using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Re-crea las funciones de <b>mover ubicación</b> (fuente: <c>backend/sql/fn_mover_ubicacion.sql</c>)
    /// para que <c>fn_rekey_nucleo</c> copie las columnas ERP de bodega que
    /// <c>20260725175311_AddInfraErpAvicolaSantaReyes</c> agregó a <c>nucleos</c>
    /// (<c>codigo_bodega</c>, <c>descripcion_bodega</c>): el INSERT de la función usa lista
    /// explícita de columnas y al mover un núcleo esas 2 se perdían en silencio.
    /// <list type="bullet">
    ///   <item>Columnas defensivas <c>IF NOT EXISTS</c> en <c>nucleos</c> (mismo patrón que las de
    ///   <c>menus</c> en la migración de Santa Reyes): si esta migración llega a una BD antes que la
    ///   de Santa Reyes, la función nunca referencia columnas inexistentes; ambas convergen.</item>
    ///   <item>Se re-crean las <b>3</b> funciones del archivo (<c>fn_mover_lote</c>,
    ///   <c>fn_mover_galpon</c>, <c>fn_rekey_nucleo</c>): la versión original se aplicó fuera de
    ///   banda (commit <c>100c343</c>, sin migración) y no existe en BDs locales/nuevas; para
    ///   prod las dos primeras son idénticas (no-op) y solo cambia <c>fn_rekey_nucleo</c>.</item>
    /// </list>
    /// Idempotente: <c>ADD COLUMN IF NOT EXISTS</c> + <c>CREATE OR REPLACE FUNCTION</c>.
    /// </summary>
    public partial class FnMoverUbicacionCopiaBodegaNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────────────
            // nucleos — columnas defensivas (las crea AddInfraErpAvicolaSantaReyes;
            // acá solo se garantiza que existan antes de re-crear la función)
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
ALTER TABLE public.nucleos
    ADD COLUMN IF NOT EXISTS codigo_bodega character varying(20) NULL;
ALTER TABLE public.nucleos
    ADD COLUMN IF NOT EXISTS descripcion_bodega character varying(200) NULL;
");

            // ─────────────────────────────────────────────────────────────────────
            // Funciones de mover ubicación — contenido íntegro de
            // backend/sql/fn_mover_ubicacion.sql (mantener sincronizados)
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
-- fn_mover_lote: reubica UN lote (tabla `lotes`, PK int) y sus espejos de fase.
CREATE OR REPLACE FUNCTION public.fn_mover_lote(
    p_lote_id      integer,
    p_granja_dest  integer,
    p_nucleo_dest  varchar,
    p_galpon_dest  varchar,
    p_user_id      integer
) RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
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
END;
$$;

-- fn_mover_galpon: mueve un galpón (y TODO lo que contiene) a otro núcleo/granja.
CREATE OR REPLACE FUNCTION public.fn_mover_galpon(
    p_galpon_id    varchar,
    p_granja_dest  integer,
    p_nucleo_dest  varchar,
    p_user_id      integer
) RETURNS void
LANGUAGE plpgsql
AS $$
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

    -- lesiones, lote_galpones, plan_gramaje_galpon: solo galpon_id → siguen por FK, nada que reescribir.
END;
$$;

-- fn_rekey_nucleo: mueve un núcleo (y TODO su contenido) a otra granja.
-- Patrón insert-repoint-delete (la granja es parte de la PK del núcleo).
CREATE OR REPLACE FUNCTION public.fn_rekey_nucleo(
    p_nucleo_id      varchar,
    p_granja_origen  integer,
    p_granja_dest    integer,
    p_user_id        integer
) RETURNS void
LANGUAGE plpgsql
AS $$
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

    -- 4) Borrar el núcleo origen (ya sin hijos apuntando)
    DELETE FROM public.nucleos WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura la versión previa de fn_rekey_nucleo (sin codigo_bodega/descripcion_bodega).
            // No se borran funciones (prod las tiene desde antes, fuera de banda) ni las columnas
            // defensivas (pertenecen a AddInfraErpAvicolaSantaReyes y borrarlas sería destructivo).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION public.fn_rekey_nucleo(
    p_nucleo_id      varchar,
    p_granja_origen  integer,
    p_granja_dest    integer,
    p_user_id        integer
) RETURNS void
LANGUAGE plpgsql
AS $$
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

    INSERT INTO public.nucleos
        (nucleo_id, granja_id, nucleo_nombre, company_id,
         created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at)
    SELECT nucleo_id, p_granja_dest, nucleo_nombre, company_id,
           created_by_user_id, created_at, p_user_id, now(), deleted_at
      FROM public.nucleos
     WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;

    UPDATE public.galpones                  SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lotes                     SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_postura_levante      SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_postura_produccion   SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.lote_ave_engorde          SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.historial_inventario      SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.inventario_aves           SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.produccion_lotes          SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
    UPDATE public.vacunacion_cronograma_item SET granja_id = p_granja_dest WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;

    DELETE FROM public.nucleos WHERE nucleo_id = p_nucleo_id AND granja_id = p_granja_origen;
END;
$$;
");
        }
    }
}
