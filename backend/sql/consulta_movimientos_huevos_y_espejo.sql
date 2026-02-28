-- =============================================================================
-- CONSULTA: Registro de movimientos de huevos y tabla espejo
-- =============================================================================
-- Tabla de movimientos: traslado_huevos (cada traslado/venta se guarda aquí).
-- Tabla espejo: espejo_huevo_produccion (historico = suma seguimiento; dinamico = disponible, se resta al procesar).
-- Al procesar un traslado (estado = 'Completado'), la app resta en espejo.*_dinamico.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Listar todos los movimientos de huevos (registro histórico)
-- -----------------------------------------------------------------------------
SELECT
    th.id,
    th.numero_traslado,
    th.fecha_traslado,
    th.tipo_operacion,
    th.lote_id,
    th.lote_postura_produccion_id,
    th.estado,
    th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio + th.cantidad_deforme
        + th.cantidad_blanco + th.cantidad_doble_yema + th.cantidad_piso + th.cantidad_pequeno
        + th.cantidad_roto + th.cantidad_desecho + th.cantidad_otro AS total_huevos,
    th.cantidad_limpio,
    th.cantidad_tratado,
    th.cantidad_sucio,
    th.cantidad_deforme,
    th.cantidad_blanco,
    th.cantidad_doble_yema,
    th.cantidad_piso,
    th.cantidad_pequeno,
    th.cantidad_roto,
    th.cantidad_desecho,
    th.cantidad_otro,
    th.granja_origen_id,
    th.granja_destino_id,
    th.usuario_nombre,
    th.created_at
FROM public.traslado_huevos th
WHERE th.deleted_at IS NULL
ORDER BY th.fecha_traslado DESC, th.id DESC;

-- -----------------------------------------------------------------------------
-- 2. Movimientos por lote producción (LPP): filtrar por lote_postura_produccion_id
-- -----------------------------------------------------------------------------
-- Sustituir 999 por el id del lote postura producción que quieras consultar.
/*
SELECT
    th.id,
    th.numero_traslado,
    th.fecha_traslado,
    th.tipo_operacion,
    th.estado,
    th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio + th.cantidad_deforme
        + th.cantidad_blanco + th.cantidad_doble_yema + th.cantidad_piso + th.cantidad_pequeno
        + th.cantidad_roto + th.cantidad_desecho + th.cantidad_otro AS total_huevos,
    th.cantidad_limpio,
    th.cantidad_tratado,
    th.cantidad_sucio,
    th.cantidad_deforme,
    th.cantidad_blanco,
    th.cantidad_doble_yema,
    th.cantidad_piso,
    th.cantidad_pequeno,
    th.cantidad_roto,
    th.cantidad_desecho,
    th.cantidad_otro
FROM public.traslado_huevos th
WHERE th.deleted_at IS NULL
  AND (th.lote_postura_produccion_id = 999 OR th.lote_id = 'LPP-999')
ORDER BY th.fecha_traslado DESC;
*/

-- -----------------------------------------------------------------------------
-- 3. Espejo + suma de movimientos completados por LPP (validar coherencia)
-- -----------------------------------------------------------------------------
-- Compara: historico (producción), dinamico (disponible), suma movimientos Completados.
SELECT
    e.lote_postura_produccion_id,
    lpp.lote_nombre,
    e.huevo_tot_historico,
    e.huevo_tot_dinamico,
    COALESCE(m.suma_movimientos, 0) AS total_movimientos_completados,
    e.huevo_tot_historico - COALESCE(m.suma_movimientos, 0) AS esperado_dinamico,
    e.huevo_tot_dinamico AS dinamico_actual
FROM public.espejo_huevo_produccion e
JOIN public.lote_postura_produccion lpp ON lpp.lote_postura_produccion_id = e.lote_postura_produccion_id AND lpp.deleted_at IS NULL
LEFT JOIN (
    SELECT
        COALESCE(th.lote_postura_produccion_id,
                 CASE WHEN th.lote_id ~ '^LPP-[0-9]+$' THEN (SUBSTRING(th.lote_id FROM 5))::INTEGER END
        ) AS lpp_id,
        SUM(th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio + th.cantidad_deforme
            + th.cantidad_blanco + th.cantidad_doble_yema + th.cantidad_piso + th.cantidad_pequeno
            + th.cantidad_roto + th.cantidad_desecho + th.cantidad_otro) AS suma_movimientos
    FROM public.traslado_huevos th
    WHERE th.estado = 'Completado' AND th.deleted_at IS NULL
    GROUP BY 1
) m ON m.lpp_id = e.lote_postura_produccion_id
ORDER BY e.lote_postura_produccion_id;

-- -----------------------------------------------------------------------------
-- 4. Solo movimientos con estado 'Completado' (los que ya descontaron en espejo)
-- -----------------------------------------------------------------------------
SELECT
    th.lote_postura_produccion_id,
    th.lote_id,
    th.numero_traslado,
    th.fecha_traslado,
    th.tipo_operacion,
    th.cantidad_limpio + th.cantidad_tratado + th.cantidad_sucio + th.cantidad_deforme
        + th.cantidad_blanco + th.cantidad_doble_yema + th.cantidad_piso + th.cantidad_pequeno
        + th.cantidad_roto + th.cantidad_desecho + th.cantidad_otro AS total_huevos
FROM public.traslado_huevos th
WHERE th.estado = 'Completado'
  AND th.deleted_at IS NULL
ORDER BY th.fecha_traslado DESC;
