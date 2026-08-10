-- =============================================================================
-- GATE MULTIPAÍS del saldo de alimento de engorde.
--
-- POR QUÉ EXISTE
-- La ventana de alimento previo al encaset (v9, jul-2026) se midió contra Panamá y se desplegó.
-- Ecuador encadena 3-4 ciclos por galpón —topología que Panamá no tiene— y la ventana se comía la
-- limpieza del ciclo anterior: 26 lotes con apertura negativa y 330 filas de la tabla diaria en rojo.
-- Se detectó a las 24 horas, por un ticket de operación. La comparación que lo habría atajado tarda
-- 30 segundos y es esta.
--
-- REGLA (vinculante, ver CLAUDE.md § Testing): si el cambio toca `fn_seguimiento_diario_engorde` o
-- cualquier `*SaldoAlimento*`, la validación obligatoria es comparación fila a fila EN TODAS LAS
-- EMPRESAS, no solo en la que motivó el fix.
--
-- USO — el mismo comando las dos veces, sin flags:
--
--     psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql     <- ANTES del cambio (congela)
--     ... aplicar el cambio (fn nueva, migración, refactor del C#) ...
--     psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql     <- DESPUÉS (compara)
--
-- La primera corrida crea la línea base y avisa. La segunda muestra el diff por empresa.
-- Para empezar de cero: DROP TABLE _paridad_saldo_base;
--
-- CLAVE DE COMPARACIÓN: (lote_id, fecha, seg_id). El `seg_id` NO es decorativo — ver el
-- comentario del snapshot.
--
-- CÓMO SE LEE
-- Toda empresa que NO sea el objetivo del cambio tiene que salir con 0 en TODAS las columnas. Si el
-- cambio se declaró no-op, salen en 0 todas. Cualquier otra cosa se justifica por escrito antes de
-- mergear.
-- =============================================================================

\timing off
\set ON_ERROR_STOP on

DROP TABLE IF EXISTS _paridad_saldo_nuevo;
CREATE TABLE _paridad_saldo_nuevo AS
SELECT l.company_id,
       l.lote_ave_engorde_id AS lote_id,
       f.fecha,
       -- `seg_id` es OBLIGATORIO en la clave: un lote puede tener DOS filas la misma fecha
       -- (dos seguimientos el mismo dia) y la fn no garantiza el orden entre ellas. Sin este
       -- desempate, el LEFT JOIN de abajo las emparejaba cruzadas y el gate reportaba
       -- diferencias que no existian. Medido el 09-ago-2026: 3 pares en Panama producian
       -- 6 falsas diferencias (dif_saldo_aves y dif_consumo) en CUALQUIER corrida, incluso
       -- sin ningun cambio. Un gate con falsos positivos se termina ignorando, que es peor
       -- que no tenerlo.
       f.seg_id,
       f.saldo_alimento_kg,
       f.saldo_aves,
       f.ingreso_alimento_kg,
       f.traslado_entrada_kg,
       f.traslado_salida_kg,
       f.consumo_dia_kg,
       f.documento
FROM lote_ave_engorde l
CROSS JOIN LATERAL fn_seguimiento_diario_engorde(l.lote_ave_engorde_id) f
WHERE l.deleted_at IS NULL;

-- Primera corrida: no hay con qué comparar todavía → la actual pasa a ser la línea base.
DO $$
BEGIN
    IF to_regclass('public._paridad_saldo_base') IS NULL THEN
        CREATE TABLE _paridad_saldo_base AS SELECT * FROM _paridad_saldo_nuevo;
        RAISE NOTICE '';
        RAISE NOTICE '>>> LINEA BASE CREADA (% filas). Aplica tu cambio y volve a correr este mismo script.',
                     (SELECT count(*) FROM _paridad_saldo_base);
        RAISE NOTICE '';
    ELSE
        RAISE NOTICE '';
        RAISE NOTICE '>>> COMPARANDO contra la linea base (% filas).', (SELECT count(*) FROM _paridad_saldo_base);
        RAISE NOTICE '';
    END IF;
END $$;

\echo '=== DIFERENCIAS POR EMPRESA (todo en 0 = el cambio no la toca) ==='

SELECT c.name                                                                     AS empresa,
       count(*)                                                                   AS filas_base,
       count(*) FILTER (WHERE n.lote_id IS NULL)                                  AS filas_que_desaparecen,
       (SELECT count(*) FROM _paridad_saldo_nuevo n2
         WHERE n2.company_id = b.company_id
           AND NOT EXISTS (SELECT 1 FROM _paridad_saldo_base b2
                            WHERE b2.lote_id = n2.lote_id AND b2.fecha = n2.fecha
                              AND b2.seg_id IS NOT DISTINCT FROM n2.seg_id)) AS filas_nuevas,
       count(*) FILTER (WHERE n.lote_id IS NOT NULL
                          AND abs(coalesce(b.saldo_alimento_kg,0) - coalesce(n.saldo_alimento_kg,0)) > 0.001) AS dif_saldo_alimento,
       count(*) FILTER (WHERE n.lote_id IS NOT NULL
                          AND coalesce(b.saldo_aves,0) <> coalesce(n.saldo_aves,0))                            AS dif_saldo_aves,
       count(*) FILTER (WHERE n.lote_id IS NOT NULL
                          AND abs(coalesce(b.ingreso_alimento_kg,0) - coalesce(n.ingreso_alimento_kg,0)) > 0.001) AS dif_ingreso,
       count(*) FILTER (WHERE n.lote_id IS NOT NULL
                          AND abs(coalesce(b.consumo_dia_kg,0) - coalesce(n.consumo_dia_kg,0)) > 0.001)        AS dif_consumo,
       count(*) FILTER (WHERE n.lote_id IS NOT NULL
                          AND coalesce(b.documento,'') <> coalesce(n.documento,''))                            AS dif_documento
FROM _paridad_saldo_base b
JOIN companies c ON c.id = b.company_id
LEFT JOIN _paridad_saldo_nuevo n ON n.lote_id = b.lote_id AND n.fecha = b.fecha
                                AND n.seg_id IS NOT DISTINCT FROM b.seg_id
GROUP BY b.company_id, c.name
ORDER BY c.name;

\echo ''
\echo '=== NINGUNA fila de seguimiento puede desaparecer (esperadas == presentes) ==='

SELECT (SELECT count(*)
          FROM seguimiento_diario_aves_engorde s
          JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = s.lote_ave_engorde_id
         WHERE l.deleted_at IS NULL)                                    AS esperadas,
       (SELECT count(*)
          FROM _paridad_saldo_nuevo n
         WHERE EXISTS (SELECT 1 FROM seguimiento_diario_aves_engorde s
                        WHERE s.lote_ave_engorde_id = n.lote_id
                          AND s.fecha::date = n.fecha))                 AS presentes;

\echo ''
\echo '=== LOTES CON MAYOR CAMBIO DE SALDO (para justificar el diff) ==='

SELECT c.name AS empresa, fa.name AS granja, l.galpon_id, l.lote_nombre AS corrida,
       count(*)                                                        AS filas_cambiadas,
       round(max(abs(b.saldo_alimento_kg - n.saldo_alimento_kg))::numeric, 1) AS peor_diferencia
FROM _paridad_saldo_base b
JOIN _paridad_saldo_nuevo n ON n.lote_id = b.lote_id AND n.fecha = b.fecha
                           AND n.seg_id IS NOT DISTINCT FROM b.seg_id
JOIN lote_ave_engorde l ON l.lote_ave_engorde_id = b.lote_id
JOIN companies c ON c.id = b.company_id
LEFT JOIN farms fa ON fa.id = l.granja_id
WHERE abs(coalesce(b.saldo_alimento_kg,0) - coalesce(n.saldo_alimento_kg,0)) > 0.001
GROUP BY c.name, fa.name, l.galpon_id, l.lote_nombre
ORDER BY peor_diferencia DESC
LIMIT 15;
