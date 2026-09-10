-- ============================================================================
-- backfill_venta_engorde_neto_cero_bruto_igual_tara.sql
-- ESPEJO de la migración FixVentaEngordeNetoCeroBrutoIgualTara (el vehículo es la migración).
-- ----------------------------------------------------------------------------
-- QUÉ CORRIGE
--   Ventas de pollo engorde donde el operario digitó el MISMO número en `peso_bruto` y en
--   `peso_tara` ⇒ `peso_neto = 0`: la venta cuenta las aves y aporta 0 kg al seguimiento diario,
--   al informe semanal, a la liquidación y a los indicadores.
--
-- POR QUÉ ESE NÚMERO ES EL NETO (medido 9-sep-2026 sobre la copia de prod del 3-sep)
--   ItalcolPanama: 206 de 207 ventas con `peso_bruto = peso_tara`; `peso_bruto` promedia
--   2,357 kg/ave — el peso de un pollo de 32-42 días, NO el de un camión cargado. La empresa que
--   usa los campos como corresponde (ItalcolEcuador, 0 de 1.472 con bruto = tara) promedia
--   7,21 kg/ave en bruto y 2,836 en neto. Planta entrega UNA sola cifra de kilos y el formulario
--   pide dos, así que se repetía el número.
--
-- QUÉ HACE
--   Mueve ese valor a `peso_neto` y deja `peso_tara = 0` («no se reportó tara»), que es la verdad
--   de lo que hay: no inventa kilos, reubica los que ya estaban digitados.
--
-- SELECCIÓN POR PATRÓN, NO POR EMPRESA
--   El WHERE no nombra ninguna company_id: describe el defecto (bruto = tara > 0 con neto 0). Hoy
--   sólo Panamá cae, pero los ids de empresa difieren local↔prod y una regla por tenant no escala.
--
-- MULTI-LÍNEA
--   `peso_bruto` es el peso del camión CLONADO en cada línea de la factura; el individual correcto
--   es el prorrateo por aves. Se replica MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea:
--   redondeo a 3 decimales y residuo a la línea con más aves. Con tara 0, bruto prorrateado == neto.
--   (Las 206 filas de hoy son facturas de UNA línea ⇒ el prorrateo es la identidad y el residuo 0.)
--
-- IDEMPOTENTE
--   Tras correr, las filas tienen `peso_neto > 0` y dejan de cumplir el WHERE ⇒ re-ejecutar es no-op.
--   El respaldo se llena una sola vez por fila (NOT EXISTS).
--
-- EL ESPEJO HISTÓRICO SE ACTUALIZA SOLO
--   `trg_movimiento_pollo_engorde_lote_hist` escucha `UPDATE OF … peso_neto, peso_tara_real,
--   promedio_peso_ave …` ⇒ `lote_registro_historico_unificado` se reescribe sin tocarlo a mano.
-- ============================================================================

-- 1) Respaldo previo (para revertir sin adivinar).
CREATE TABLE IF NOT EXISTS public._backup_mpe_peso_neto_cero (
    id                  INTEGER PRIMARY KEY,
    peso_bruto          DOUBLE PRECISION,
    peso_tara           DOUBLE PRECISION,
    peso_bruto_global   DOUBLE PRECISION,
    peso_tara_global    DOUBLE PRECISION,
    peso_neto_global    DOUBLE PRECISION,
    peso_bruto_real     DOUBLE PRECISION,
    peso_tara_real      DOUBLE PRECISION,
    peso_neto           DOUBLE PRECISION,
    promedio_peso_ave   DOUBLE PRECISION,
    respaldado_en       TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public._backup_mpe_peso_neto_cero (
    id, peso_bruto, peso_tara, peso_bruto_global, peso_tara_global, peso_neto_global,
    peso_bruto_real, peso_tara_real, peso_neto, promedio_peso_ave)
SELECT m.id, m.peso_bruto, m.peso_tara, m.peso_bruto_global, m.peso_tara_global, m.peso_neto_global,
       m.peso_bruto_real, m.peso_tara_real, m.peso_neto, m.promedio_peso_ave
FROM public.movimiento_pollo_engorde m
WHERE m.deleted_at IS NULL
  AND m.tipo_movimiento = 'Venta'
  AND m.peso_bruto IS NOT NULL
  AND m.peso_bruto > 0
  AND m.peso_bruto = m.peso_tara
  AND COALESCE(m.peso_neto, 0) = 0
  AND (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) > 0
  AND NOT EXISTS (SELECT 1 FROM public._backup_mpe_peso_neto_cero b WHERE b.id = m.id);

-- 2) Corrección.
WITH objetivo AS (
    SELECT m.id,
           -- Sin factura, la línea es su propio despacho (no agrupar todos los NULL juntos).
           COALESCE(m.factura_id::text, 'mov-' || m.id) AS despacho,
           m.peso_bruto::numeric                        AS global_kg,
           (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas)::numeric AS aves
    FROM public.movimiento_pollo_engorde m
    WHERE m.deleted_at IS NULL
      AND m.tipo_movimiento = 'Venta'
      AND m.peso_bruto IS NOT NULL
      AND m.peso_bruto > 0
      AND m.peso_bruto = m.peso_tara
      AND COALESCE(m.peso_neto, 0) = 0
      AND (m.cantidad_hembras + m.cantidad_machos + m.cantidad_mixtas) > 0
),
prorrateo AS (
    SELECT o.id, o.despacho, o.aves, o.global_kg,
           ROUND(o.global_kg * o.aves / SUM(o.aves) OVER (PARTITION BY o.despacho), 3) AS neto_base,
           ROW_NUMBER() OVER (PARTITION BY o.despacho ORDER BY o.aves DESC, o.id)      AS rn
    FROM objetivo o
),
residuo AS (
    SELECT p.despacho, MAX(p.global_kg) - SUM(p.neto_base) AS resto
    FROM prorrateo p
    GROUP BY p.despacho
),
final AS (
    SELECT p.id, p.aves, p.global_kg,
           ROUND(p.neto_base + CASE WHEN p.rn = 1 THEN r.resto ELSE 0 END, 3) AS neto
    FROM prorrateo p
    JOIN residuo r ON r.despacho = p.despacho
)
UPDATE public.movimiento_pollo_engorde m
   SET peso_tara         = 0,
       peso_tara_global  = 0,
       peso_tara_real    = 0,
       peso_bruto_global = f.global_kg::double precision,
       peso_bruto_real   = f.neto::double precision,   -- con tara 0, el bruto prorrateado ES el neto
       peso_neto_global  = f.global_kg::double precision,
       peso_neto         = f.neto::double precision,
       promedio_peso_ave = (f.neto / f.aves)::double precision,
       updated_at        = now()
  FROM final f
 WHERE m.id = f.id;
