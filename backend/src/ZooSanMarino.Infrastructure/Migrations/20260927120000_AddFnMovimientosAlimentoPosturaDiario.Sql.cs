namespace ZooSanMarino.Infrastructure.Migrations;

public partial class AddFnMovimientosAlimentoPosturaDiario
{
    private const string FnMovimientosAlimentoPosturaDiario = """
CREATE OR REPLACE FUNCTION public.fn_movimientos_alimento_postura_diario(
    p_company_id INT,
    p_lote_id    INT,
    p_farm_id    INT,
    p_nucleo_id  TEXT,
    p_galpon_id  TEXT,
    p_desde      DATE,
    p_hasta      DATE
)
RETURNS TABLE (
    fecha             DATE,
    ingresos_json     TEXT,
    traslados_json    TEXT,
    referencias_json  TEXT
)
LANGUAGE sql
STABLE
AS $$
WITH configuracion AS (
    SELECT
        COALESCE(c.maneja_inventario_por_silo, false) AS por_silo,
        COALESCE(f.maneja_alimento_por_galpon, c.maneja_alimento_por_galpon, false) AS por_galpon
    FROM public.companies c
    JOIN public.farms f
      ON f.company_id = c.id
     AND f.id = p_farm_id
     AND f.deleted_at IS NULL
    WHERE c.id = p_company_id
), movimientos AS (
    SELECT
        h.id,
        h.fecha_operacion,
        h.tipo_evento,
        h.cantidad_kg,
        h.item_resumen,
        h.referencia,
        h.numero_documento,
        COALESCE(NULLIF(BTRIM(h.referencia), ''), NULLIF(BTRIM(h.numero_documento), '')) AS referencia_visible
    FROM public.lote_registro_historico_unificado h
    JOIN public.item_inventario i
      ON i.id = h.item_inventario_id
     AND i.company_id = p_company_id
     AND LOWER(i.tipo_item) = 'alimento'
    CROSS JOIN configuracion cfg
    WHERE h.company_id = p_company_id
      AND h.farm_id = p_farm_id
      AND h.tipo_evento IN ('INV_INGRESO', 'INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')
      AND NOT h.anulado
      AND NOT h.para_proximo_ciclo
      AND h.cantidad_kg > 0
      AND h.fecha_operacion BETWEEN p_desde AND p_hasta
      AND NOT (
          h.referencia IS NOT NULL
          AND (
              h.referencia LIKE '%devolución por eliminación%'
              OR h.referencia LIKE '%devolucion por eliminacion%'
          )
      )
      AND (
          (
              cfg.por_silo
              AND h.silo_id IS NOT NULL
              AND EXISTS (
                  SELECT 1
                  FROM public.lote_silos ls
                  JOIN public.farm_silos fs
                    ON fs.id = ls.farm_silo_id
                   AND fs.company_id = p_company_id
                   AND fs.granja_id = p_farm_id
                   AND fs.activo
                   AND fs.deleted_at IS NULL
                  WHERE ls.company_id = p_company_id
                    AND ls.lote_id = p_lote_id
                    AND ls.activo
                    AND ls.farm_silo_id = h.silo_id
              )
          )
          OR (
              NOT cfg.por_silo
              AND cfg.por_galpon
              AND COALESCE(BTRIM(h.nucleo_id), '') = COALESCE(BTRIM(p_nucleo_id), '')
              AND COALESCE(BTRIM(h.galpon_id), '') = COALESCE(BTRIM(p_galpon_id), '')
          )
          OR (NOT cfg.por_silo AND NOT cfg.por_galpon)
      )
)
SELECT
    m.fecha_operacion AS fecha,
    COALESCE(
        JSONB_AGG(
            JSONB_BUILD_OBJECT(
                'id', m.id,
                'fecha', m.fecha_operacion,
                'tipoMovimiento', m.tipo_evento,
                'cantidadKg', m.cantidad_kg,
                'alimento', m.item_resumen,
                'referencia', m.referencia,
                'numeroDocumento', m.numero_documento
            ) ORDER BY m.id
        ) FILTER (WHERE m.tipo_evento = 'INV_INGRESO'),
        '[]'::jsonb
    )::text AS ingresos_json,
    COALESCE(
        JSONB_AGG(
            JSONB_BUILD_OBJECT(
                'id', m.id,
                'fecha', m.fecha_operacion,
                'tipoMovimiento', m.tipo_evento,
                'cantidadKg', m.cantidad_kg,
                'alimento', m.item_resumen,
                'referencia', m.referencia,
                'numeroDocumento', m.numero_documento
            ) ORDER BY m.id
        ) FILTER (WHERE m.tipo_evento IN ('INV_TRASLADO_ENTRADA', 'INV_TRASLADO_SALIDA')),
        '[]'::jsonb
    )::text AS traslados_json,
    COALESCE(
        TO_JSONB(ARRAY_AGG(DISTINCT m.referencia_visible ORDER BY m.referencia_visible)
            FILTER (WHERE m.referencia_visible IS NOT NULL)),
        '[]'::jsonb
    )::text AS referencias_json
FROM movimientos m
GROUP BY m.fecha_operacion
ORDER BY m.fecha_operacion;
$$;
""";
}
