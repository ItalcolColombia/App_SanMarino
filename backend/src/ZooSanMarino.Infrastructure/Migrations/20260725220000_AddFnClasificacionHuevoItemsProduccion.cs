using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZooSanMarino.Infrastructure.Migrations
{
    /// <summary>
    /// Santa Reyes — Fase 5 (reportes): crea <c>fn_clasificacion_huevo_items_produccion</c>, el
    /// desglose por SEMANA × ÍTEM (Primera/Pnc) de la clasificación de huevos que las empresas con
    /// <c>companies.clasificacion_huevo_por_items = true</c> guardan en el jsonb
    /// <c>seguimiento_diario_produccion.metadata -&gt; 'huevoItems'</c> (las 11 columnas fijas quedan
    /// en 0 y <c>huevo_tot</c> conserva la suma).
    /// <para>
    /// Función HERMANA de <c>fn_indicadores_produccion_postura</c>: mismos parámetros, misma
    /// resolución de lote (LPP prioritario / legacy), misma fuente (UNION del seguimiento unificado
    /// <c>tipo_seguimiento='produccion'</c> + <c>seguimiento_diario_produccion</c> con DISTINCT ON
    /// por día en <c>America/Bogota</c>) y misma fórmula de semana de vida
    /// (<c>((fecha_local - fecha_encaset) / 7) + 1</c>) → la columna <c>semana</c> casa 1:1 con la
    /// grilla de indicadores. La consume
    /// <c>IndicadoresProduccionService.ObtenerClasificacionHuevoItemsAsync</c> (SqlQueryRaw) desde
    /// <c>POST /api/Produccion/clasificacion-huevo-items</c>.
    /// </para>
    /// Migración de SOLO DATOS/objetos SQL (no toca el modelo EF → Designer clonado, ModelSnapshot
    /// intacto). Idempotente: <c>DROP FUNCTION IF EXISTS</c> + <c>CREATE OR REPLACE</c>.
    /// Fuente de verdad del cuerpo: <c>backend/sql/fn_clasificacion_huevo_items_produccion.sql</c>.
    /// </summary>
    public partial class AddFnClasificacionHuevoItemsProduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ============================================================================
-- fn_clasificacion_huevo_items_produccion(...)
-- FASE 5 (Santa Reyes) — Desglose de la clasificación de huevos POR ÍTEM del catálogo
-- (categorías comerciales Primera / Pnc) agrupado por SEMANA DE VIDA, para las empresas con
-- companies.clasificacion_huevo_por_items = true: en ellas las 11 columnas fijas
-- (huevo_limpio…huevo_otro) quedan en 0 y el desglose real vive en el jsonb
-- seguimiento_diario_produccion.metadata -> 'huevoItems'
-- ([{catalogItemId, codigo, nombre, tipoHuevo, cantidad, um}, …], escrito por
-- HuevoItemsCalculos.EscribirEnMetadata; huevo_tot conserva la SUMA para no romper
-- espejo/trigger/saldos/indicadores).
--
-- Es el endpoint HERMANO de fn_indicadores_produccion_postura: MISMOS parámetros de entrada,
-- MISMA resolución de lote (LPP prioritario / legacy por lote), MISMA fuente de datos
-- (UNION seguimiento_diario_levante[tipo_seguimiento='produccion'] + seguimiento_diario_produccion
-- con DISTINCT ON por día en America/Bogota) y MISMA fórmula de semana
-- (semana de vida = ((fecha_local - fecha_encaset) / 7) + 1, división entera) → la columna
-- `semana` casa 1:1 con la grilla de indicadores semanales.
--
-- Diferencias deliberadas respecto de fn_indicadores_produccion_postura (documentadas):
--   * NO aplica el corte ""semanas de producción >= 25"": el desglose respeta EXCLUSIVAMENTE los
--     filtros que envía el llamador (p_semana_desde/p_semana_hasta). Motivo: las empresas que usan
--     clasificación por ítems pasan a producción antes de la semana 25 (Santa Reyes liquida el
--     levante ~semana 16) y ese desglose se perdería. El front actual manda semanaDesde = 26,
--     así que el resultado visible sigue alineado con la grilla.
--   * No usa tablas TEMP: todo se resuelve con CTEs (la fn puede invocarse varias veces dentro de
--     la misma transacción sin colisionar).
--
-- Robustez (nunca lanza; devuelve 0 filas cuando no hay nada):
--   * lote inexistente / sin fecha de referencia / sin ninguno de los dos parámetros → sin filas.
--   * registros sin metadata o sin la clave 'huevoItems' → no aportan (COALESCE a '[]').
--   * 'huevoItems' que no sea un array JSON, o elementos que no sean objetos → se ignoran.
--   * cantidades ausentes, no numéricas o <= 0 → se descartan.
--
-- Consumido por IndicadoresProduccionService.ObtenerClasificacionHuevoItemsAsync (SqlQueryRaw,
-- columnas snake_case → propiedades PascalCase) vía POST /api/Produccion/clasificacion-huevo-items.
-- ============================================================================

-- Idempotente: el row type de los parámetros OUT puede cambiar en el futuro y Postgres no admite
-- CREATE OR REPLACE en ese caso → DROP defensivo con la firma exacta.
DROP FUNCTION IF EXISTS fn_clasificacion_huevo_items_produccion(integer, integer, integer, integer, integer, date, date);

CREATE OR REPLACE FUNCTION fn_clasificacion_huevo_items_produccion(
    p_company_id                  integer,
    p_lote_postura_produccion_id  integer  DEFAULT NULL,
    p_lote_id                     integer  DEFAULT NULL,
    p_semana_desde                integer  DEFAULT NULL,
    p_semana_hasta                integer  DEFAULT NULL,
    p_fecha_desde                 date     DEFAULT NULL,
    p_fecha_hasta                 date     DEFAULT NULL
)
RETURNS TABLE(
    semana      integer,
    tipo_huevo  text,
    codigo      text,
    nombre      text,
    cantidad    bigint
)
LANGUAGE plpgsql STABLE AS $fn$
DECLARE
    v_enc_date     date;               -- fecha de referencia (encaset) en zona Bogotá
    v_lote_id_str  text;               -- flujo legacy: lote_id como texto (columna varchar del unificado)
    v_flujo_lpp    boolean := false;   -- true = flujo LPP, false = flujo legacy por lote
    v_has_lote     boolean := false;
BEGIN
    -- ════════════════════════════════════════════════════════════════════
    -- 1) RESOLVER LOTE (misma prioridad y semántica que fn_indicadores_produccion_postura).
    -- ════════════════════════════════════════════════════════════════════
    IF p_lote_postura_produccion_id IS NOT NULL AND p_lote_postura_produccion_id > 0 THEN
        -- ── Flujo LPP ──
        SELECT
            -- fecha ref: encaset del levante ligado -> lpp.fecha_encaset -> lpp.fecha_inicio_produccion
            (COALESCE(lev.fecha_encaset, lpp.fecha_encaset, lpp.fecha_inicio_produccion)
                AT TIME ZONE 'America/Bogota')::date
          INTO v_enc_date
          FROM lote_postura_produccion lpp
          LEFT JOIN lote_postura_levante lev
                 ON lev.lote_postura_levante_id = lpp.lote_postura_levante_id
                AND lev.deleted_at IS NULL
         WHERE lpp.lote_postura_produccion_id = p_lote_postura_produccion_id
           AND lpp.company_id = p_company_id
           AND lpp.deleted_at IS NULL;

        IF NOT FOUND OR v_enc_date IS NULL THEN
            RETURN;  -- lote inexistente o sin fecha de referencia -> sin filas
        END IF;
        v_flujo_lpp := true;
        v_has_lote  := true;

    ELSIF p_lote_id IS NOT NULL AND p_lote_id > 0 THEN
        -- ── Flujo legacy: Lote en fase Producción ──
        -- lote_prod: hijo (lote_padre_id = p_lote_id) en fase Produccion; si no, el propio lote_id.
        DECLARE
            v_lp_lote_id      integer;
            v_lp_padre_id     integer;
            v_lp_fip          timestamptz;
        BEGIN
            SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
              INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
              FROM lotes l
             WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
               AND l.fase = 'Produccion' AND l.lote_padre_id = p_lote_id
             ORDER BY l.lote_id
             LIMIT 1;

            IF NOT FOUND THEN
                SELECT l.lote_id, l.lote_padre_id, l.fecha_inicio_produccion
                  INTO v_lp_lote_id, v_lp_padre_id, v_lp_fip
                  FROM lotes l
                 WHERE l.company_id = p_company_id AND l.deleted_at IS NULL
                   AND l.fase = 'Produccion' AND l.lote_id = p_lote_id
                 LIMIT 1;
            END IF;

            IF NOT FOUND THEN
                RETURN;
            END IF;
            v_lote_id_str := v_lp_lote_id::text;

            -- fecha ref = fecha_inicio_produccion; si null y hay padre -> fecha_encaset del padre
            IF v_lp_fip IS NULL AND v_lp_padre_id IS NOT NULL THEN
                SELECT p.fecha_encaset INTO v_lp_fip
                  FROM lotes p WHERE p.lote_id = v_lp_padre_id AND p.deleted_at IS NULL;
            END IF;
            IF v_lp_fip IS NULL THEN
                RETURN;
            END IF;
            v_enc_date := (v_lp_fip AT TIME ZONE 'America/Bogota')::date;
            v_has_lote := true;
        END;

    ELSE
        RETURN;  -- ni LPP ni loteId válido
    END IF;

    IF NOT v_has_lote THEN RETURN; END IF;

    -- ════════════════════════════════════════════════════════════════════
    -- 2) Seguimientos del lote (unificado tipo 'produccion' UNION legacy), merge por DÍA (Bogotá):
    --    el registro más temprano gana el día — MISMO criterio que los indicadores, así el desglose
    --    por ítem corresponde exactamente al huevo_tot que muestra la grilla.
    -- 3) Semana de vida + filtros de fecha/semana.
    -- 4) Expansión del jsonb 'huevoItems' y agregación por semana × tipo × ítem.
    -- ════════════════════════════════════════════════════════════════════
    RETURN QUERY
    WITH crudos AS (
        SELECT sd.fecha AS ts, sd.metadata AS meta
          FROM seguimiento_diario_levante sd
         WHERE sd.tipo_seguimiento = 'produccion'
           AND ((v_flujo_lpp AND sd.lote_postura_produccion_id = p_lote_postura_produccion_id)
             OR (NOT v_flujo_lpp AND sd.lote_id = v_lote_id_str))
        UNION ALL
        SELECT sp.fecha_registro AS ts, sp.metadata AS meta
          FROM seguimiento_diario_produccion sp
         WHERE ((v_flujo_lpp AND sp.lote_postura_produccion_id = p_lote_postura_produccion_id)
             OR (NOT v_flujo_lpp AND sp.lote_id::text = v_lote_id_str))
    ),
    dedup AS (
        SELECT DISTINCT ON ((ts AT TIME ZONE 'America/Bogota')::date) *
          FROM crudos
         ORDER BY (ts AT TIME ZONE 'America/Bogota')::date, ts
    ),
    dias AS (
        SELECT (d.ts AT TIME ZONE 'America/Bogota')::date AS reg_date, d.meta
          FROM dedup d
    ),
    filtrados AS (
        SELECT ((dd.reg_date - v_enc_date) / 7) + 1 AS sem,   -- división entera == C# (dias/7)+1
               dd.meta
          FROM dias dd
         WHERE (p_fecha_desde IS NULL OR dd.reg_date >= p_fecha_desde)
           AND (p_fecha_hasta IS NULL OR dd.reg_date <= p_fecha_hasta)
    ),
    items AS (
        SELECT f.sem,
               COALESCE(NULLIF(btrim(it->>'tipoHuevo'), ''), '') AS tipo,
               COALESCE(NULLIF(btrim(it->>'codigo'),    ''), '') AS cod,
               COALESCE(NULLIF(btrim(it->>'nombre'),    ''), '') AS nom,
               CASE WHEN jsonb_typeof(it->'cantidad') = 'number'
                    THEN (it->>'cantidad')::numeric
                    ELSE NULL END AS cant
          FROM filtrados f
          CROSS JOIN LATERAL jsonb_array_elements(
                 CASE WHEN jsonb_typeof(COALESCE(f.meta->'huevoItems', '[]'::jsonb)) = 'array'
                      THEN COALESCE(f.meta->'huevoItems', '[]'::jsonb)
                      ELSE '[]'::jsonb
                 END) AS it
         WHERE jsonb_typeof(it) = 'object'
    )
    SELECT i.sem::integer,
           i.tipo::text,
           i.cod::text,
           i.nom::text,
           SUM(i.cant)::bigint
      FROM items i
     WHERE i.cant IS NOT NULL
       AND i.cant > 0
       AND (p_semana_desde IS NULL OR i.sem >= p_semana_desde)
       AND (p_semana_hasta IS NULL OR i.sem <= p_semana_hasta)
     GROUP BY i.sem, i.tipo, i.cod, i.nom
     ORDER BY i.sem, i.tipo, i.cod, i.nom;

    RETURN;
END;
$fn$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS fn_clasificacion_huevo_items_produccion(integer, integer, integer, integer, integer, date, date);
");
        }
    }
}
