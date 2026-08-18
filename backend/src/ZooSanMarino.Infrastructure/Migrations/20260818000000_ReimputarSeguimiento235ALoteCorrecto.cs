using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReimputarSeguimiento235ALoteCorrecto : Migration
    {
        // Cierra C12 del bloque "Consolidado de sublotes": el lote 123 (LOTE 235A de LA CAROLINA,
        // empresa Demo) mostraba saldo_hembras = -460 y dejaba sin `part` a su semana 21.
        //
        // NO ERA UN PROBLEMA DE CALCULO. El kardex lo explica solo:
        //   2026-07-06  traslado de SALIDA 5.100  ->  el lote pasa de 5.172 a 72 aves
        //   2026-07-28  20 mortalidades           ->  52
        //   2026-07-30  10 mort + 1 sel + 1 err   ->  40
        //   2026-08-03  500 MORTALIDADES          ->  -460
        // 5.303 - 648 mort - 14 sel - 1 err - 5.100 trasladadas = -460, exacto.
        //
        // El registro del 03-ago es del lote 124 (LOTE 235A de LA PRIMAVERA), que recibio esas 5.100
        // aves y dejo de registrar mortalidad el 10-jul. Los dos sublotes se llaman IGUAL, que es
        // como se elige el equivocado al cargar.
        //
        // POR QUE ESTA ES LA UNICA HIPOTESIS QUE CIERRA (las dos se simularon en transaccion y se
        // revirtieron antes de escribir esto):
        //   * reimputar la fila al lote 124  ->  123 = 40   y  124 = 4.370   (los dos sanos)
        //   * "fue un error de digitacion"   ->  con 50 en vez de 500 el lote queda en -10, porque
        //     solo tenia 40 aves vivas: CUALQUIER cifra mayor a 40 lo deja negativo.
        // Y el consumo lo confirma: la misma fila trae 750 kg de alimento. Para 40 aves son 18
        // kg/ave/dia (absurdo); para las 4.870 del lote 124 son 154 g/ave/dia (normal).
        //
        // ALCANCE — lo que esta migracion NO toca, a proposito:
        //   El movimiento de inventario de esa fila (Consumo de 750 kg del item 208) quedo asentado
        //   en la granja 95 (LA CAROLINA). Re-apuntarlo a la granja 90 (LA PRIMAVERA) crearia stock
        //   NEGATIVO de un item que esa granja nunca tuvo: su stock es del item 412. Corregir esa
        //   pata exige saber que alimento se consumio realmente, que es una decision de operacion.
        //   Queda anotado en el tracker (C12).
        //
        // IDEMPOTENTE y por LOOKUP DE NOMBRES, no por ids: los ids de lote y granja difieren entre
        // local y prod. Si el entorno no tiene exactamente este dato (por ejemplo prod, donde la
        // empresa Demo puede no existir), las tres sentencias no encuentran fila y no hacen nada.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_lpl_origen  int;
    v_lpl_destino int;
    v_lote_origen  text;
    v_lote_destino text;
BEGIN
    -- Sublote de LA CAROLINA (el que quedo en negativo) y sublote de LA PRIMAVERA (el que recibio
    -- las aves). Se resuelven por nombre de empresa + nombre de lote + nombre de granja.
    SELECT lpl.lote_postura_levante_id, lpl.lote_id::text
      INTO v_lpl_origen, v_lote_origen
      FROM lote_postura_levante lpl
      JOIN companies c ON c.id = lpl.company_id
      JOIN farms     f ON f.id = lpl.granja_id
     WHERE lpl.deleted_at IS NULL AND c.name = 'Demo'
       AND lpl.lote_nombre = 'LOTE 235A' AND f.name = 'LA CAROLINA';

    SELECT lpl.lote_postura_levante_id, lpl.lote_id::text
      INTO v_lpl_destino, v_lote_destino
      FROM lote_postura_levante lpl
      JOIN companies c ON c.id = lpl.company_id
      JOIN farms     f ON f.id = lpl.granja_id
     WHERE lpl.deleted_at IS NULL AND c.name = 'Demo'
       AND lpl.lote_nombre = 'LOTE 235A' AND f.name = 'LA PRIMAVERA';

    IF v_lpl_origen IS NULL OR v_lpl_destino IS NULL THEN
        RAISE NOTICE 'C12: los sublotes LOTE 235A de Demo no existen en este entorno; no se hace nada.';
        RETURN;
    END IF;

    -- El destino no puede tener ya un registro ese dia: la clave unica es
    -- (tipo_seguimiento, lote_id, coalesce(reproductora_id,''), fecha).
    IF EXISTS (SELECT 1 FROM seguimiento_diario_levante
                WHERE tipo_seguimiento = 'levante' AND lote_id = v_lote_destino
                  AND fecha::date = DATE '2026-08-03') THEN
        RAISE NOTICE 'C12: el lote destino ya tiene registro del 2026-08-03; no se hace nada.';
        RETURN;
    END IF;

    -- La fila se identifica por su huella, no por id: 500 mortalidades de hembras y 750 kg de
    -- consumo el 2026-08-03 sobre el sublote de LA CAROLINA. Si ya se movio, no matchea.
    UPDATE seguimiento_diario_levante
       SET lote_id = v_lote_destino,
           lote_postura_levante_id = v_lpl_destino
     WHERE tipo_seguimiento = 'levante'
       AND lote_id = v_lote_origen
       AND fecha::date = DATE '2026-08-03'
       AND COALESCE(mortalidad_hembras, 0) = 500
       AND COALESCE(consumo_kg_hembras, 0) = 750;

    IF NOT FOUND THEN
        RAISE NOTICE 'C12: el registro del 2026-08-03 ya estaba reimputado o no existe; no se hace nada.';
        RETURN;
    END IF;

    -- El maestro de aves NO se deriva: lo mantiene la app de forma incremental, asi que hay que
    -- moverlo con la fila. `IS DISTINCT FROM` deja la segunda pasada sin efecto.
    UPDATE lote_postura_levante SET aves_h_actual = 40
     WHERE lote_postura_levante_id = v_lpl_origen  AND aves_h_actual IS DISTINCT FROM 40;

    UPDATE lote_postura_levante SET aves_h_actual = 4370
     WHERE lote_postura_levante_id = v_lpl_destino AND aves_h_actual IS DISTINCT FROM 4370;

    RAISE NOTICE 'C12: registro del 2026-08-03 reimputado del sublote % al %; maestros en 40 y 4370.',
                 v_lpl_origen, v_lpl_destino;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devuelve la fila y los dos maestros a su estado previo (el lote de origen quedaba
            // clampeado en 0 por el GREATEST del maestro, no en -460).
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_lpl_origen  int;
    v_lpl_destino int;
    v_lote_origen  text;
    v_lote_destino text;
BEGIN
    SELECT lpl.lote_postura_levante_id, lpl.lote_id::text INTO v_lpl_origen, v_lote_origen
      FROM lote_postura_levante lpl
      JOIN companies c ON c.id = lpl.company_id
      JOIN farms     f ON f.id = lpl.granja_id
     WHERE lpl.deleted_at IS NULL AND c.name = 'Demo'
       AND lpl.lote_nombre = 'LOTE 235A' AND f.name = 'LA CAROLINA';

    SELECT lpl.lote_postura_levante_id, lpl.lote_id::text INTO v_lpl_destino, v_lote_destino
      FROM lote_postura_levante lpl
      JOIN companies c ON c.id = lpl.company_id
      JOIN farms     f ON f.id = lpl.granja_id
     WHERE lpl.deleted_at IS NULL AND c.name = 'Demo'
       AND lpl.lote_nombre = 'LOTE 235A' AND f.name = 'LA PRIMAVERA';

    IF v_lpl_origen IS NULL OR v_lpl_destino IS NULL THEN RETURN; END IF;

    UPDATE seguimiento_diario_levante
       SET lote_id = v_lote_origen,
           lote_postura_levante_id = v_lpl_origen
     WHERE tipo_seguimiento = 'levante'
       AND lote_id = v_lote_destino
       AND fecha::date = DATE '2026-08-03'
       AND COALESCE(mortalidad_hembras, 0) = 500
       AND COALESCE(consumo_kg_hembras, 0) = 750;

    IF NOT FOUND THEN RETURN; END IF;

    UPDATE lote_postura_levante SET aves_h_actual = 0
     WHERE lote_postura_levante_id = v_lpl_origen  AND aves_h_actual IS DISTINCT FROM 0;

    UPDATE lote_postura_levante SET aves_h_actual = 4870
     WHERE lote_postura_levante_id = v_lpl_destino AND aves_h_actual IS DISTINCT FROM 4870;
END $$;
");
        }
    }
}
