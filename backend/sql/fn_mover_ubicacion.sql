-- ============================================================================
-- fn_mover_ubicacion.sql — Cascada transaccional de "mover" ubicación
-- Módulo transversal (Colombia / Ecuador / Panamá): la ubicación (granja/núcleo/
-- galpón) está DENORMALIZADA en muchas tablas con FK Restrict. Mover algo sin
-- arrastrar a los hijos deja lotes/inventarios huérfanos (incidente de prod).
-- Estas funciones hacen todos los UPDATE en UNA transacción (una función plpgsql
-- es atómica: si algo falla, se revierte todo).
--
-- Autorización (empresa/granja accesible) se valida en el SERVICE ANTES de llamar.
-- Estas funciones son la parte mecánica; asumen que los destinos ya fueron validados.
--
-- Idempotente: CREATE OR REPLACE. Reaplicar no altera datos.
--
-- Fuente de verdad del alcance (information_schema, BD real 2026-07-22):
--   Tablas con granja_id+nucleo_id+galpon_id: lotes, galpones, historial_inventario,
--     inventario_aves, lote_ave_engorde, lote_postura_levante, lote_postura_produccion,
--     produccion_lotes, vacunacion_cronograma_item
--   Tablas con nucleo_id+galpon_id (sin granja_id): inventario_gasto,
--     inventario_gestion_movimiento, inventario_gestion_stock, lote_registro_historico_unificado
--   Tablas solo galpon_id (siguen al galpón por su PK, nada que actualizar):
--     lesiones, lote_galpones, plan_gramaje_galpon
--   Tipos: granja_id=int, nucleo_id/galpon_id=varchar. seguimiento_diario NO denormaliza (usa lote_id).
-- ============================================================================


-- ----------------------------------------------------------------------------
-- fn_mover_lote: reubica UN lote (tabla `lotes`, PK int) y sus espejos de fase.
-- Alcance = mismo que el traslado existente (lotes + lote_postura_levante +
-- lote_postura_produccion) para no cambiar comportamiento probado. NO toca
-- inventario/producción del galpón (esos son del galpón, no del lote).
-- ----------------------------------------------------------------------------
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


-- ----------------------------------------------------------------------------
-- fn_mover_galpon: mueve un galpón (y TODO lo que contiene) a otro núcleo/granja.
-- El galpon_id NO cambia (es PK) → los hijos lo siguen por FK; solo hay que
-- reescribir sus columnas denormalizadas granja_id/nucleo_id. Se filtra por
-- galpon_id (globalmente único), así que es seguro entre empresas.
-- ----------------------------------------------------------------------------
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


-- ----------------------------------------------------------------------------
-- fn_rekey_nucleo: mueve un núcleo (y TODO su contenido) a otra granja.
-- La granja es parte de la PK del núcleo y las FKs son NO ACTION → no se puede
-- UPDATE-ar la PK con hijos apuntando. Patrón insert-repoint-delete:
--   1) validar origen existe y destino no colisiona
--   2) insertar el núcleo destino {nucleo_id, granja_dest}
--   3) repuntar TODOS los hijos (granja origen → destino) para ese nucleo_id
--   4) borrar el núcleo origen
-- Colisión / inexistencia → RAISE EXCEPTION (el service lo mapea a 400).
-- ----------------------------------------------------------------------------
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
    INSERT INTO public.nucleos
        (nucleo_id, granja_id, nucleo_nombre, company_id,
         created_by_user_id, created_at, updated_by_user_id, updated_at, deleted_at)
    SELECT nucleo_id, p_granja_dest, nucleo_nombre, company_id,
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
