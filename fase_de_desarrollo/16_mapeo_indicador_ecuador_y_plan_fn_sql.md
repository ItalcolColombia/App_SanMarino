# 📋 Plan #16 — Mapeo Indicador Ecuador + Plan función SQL `fn_indicadores_pollo_engorde`

**ID:** 16
**Fecha:** 2026-05-28
**Módulo:** `IndicadorEcuadorService` + endpoint `POST /api/IndicadorEcuador/*`
**Estado:** 📐 **Diseño / Mapeo previo a refactor** — NO se implementa código en esta fase.
**Objetivo final:** consolidar todos los cálculos del servicio C# (~1233 líneas) en una función SQL que devuelva el indicador por lote de pollo engorde, para reducir N+1 queries y latencia.

---

## 0. Contexto y motivación

`IndicadorEcuadorService` es el servicio que produce la "Liquidación Técnica Pollo Engorde" para Ecuador. Calcula 22 KPIs por lote (aves encasetadas, aves vendidas, mortalidad, consumo, kg carne, conversión, eficiencia americana/europea, etc.).

### Problema actual (perfil del servicio en C#)

- **N+1 estructural**: por cada lote se ejecutan **8–10 queries independientes** (`AvesSacrificadasPolloEngordeAsync`, `MortalidadSeleccionAvesEngordeAsync`, `ConsumoPolloEngordeAsync`, `KgCarneYEdadPolloEngordeAsync`, `FechaCierrePolloEngordeAsync`, `CalcularMetrosCuadradosAsync`, `AvesTrasladadasDesdePadreHaciaReproductoresAsync`, `TodosReproductoresConCeroAvesAsync`, etc.).
- En reportes consolidados (granja completa, modo Rango, modo TodosLiquidados) se recorren foreach × 30-80 lotes ⇒ **~300-800 round-trips a la BD por reporte**.
- Lógica de cálculo duplicada con [SeguimientoAvesEngordeEcuadorService.cs](backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs) (consumo, retiros).
- La interfaz expone 6 endpoints que llaman a las **mismas** primitivas privadas con leves variaciones — ideal para extraer a función SQL única.

### Solución propuesta

Crear `fn_indicadores_pollo_engorde(p_lote_id INT)` y `fn_indicadores_pollo_engorde_bulk(p_lote_ids INT[])` en PostgreSQL. El servicio C# pasa a ser un wrapper delgado (mapping + filtros administrativos).

---

## 1. Diagrama de fuentes de datos

```
                  ┌──────────────────────────────────────────────────┐
                  │  fn_indicadores_pollo_engorde(p_lote_id) RETURNS │
                  │  TABLE (22 columnas — un fila por lote padre)   │
                  └──────────────────────────────────────────────────┘
                                       │
            ┌──────────────────────────┴──────────────────────────┐
            │                                                       │
   ┌────────▼────────┐                                  ┌──────────▼─────────┐
   │ lote_ave_engorde │ (padre)                          │ lote_reproductora_  │ (hijo)
   │  - aves_encasetadas, fecha_encaset                  │  ave_engorde         │
   │  - estado_operativo_lote, liquidado_at              │  - aves_inicio_*    │
   │  - granja_id, galpon_id                             │  - fecha_encasetamiento │
   └────────┬─────────┘                                  └──────────┬─────────┘
            │                                                       │
            │ lote_ave_engorde_origen_id        ────────┐           │ lote_reproductora_ave_engorde_origen_id
            ▼                                            │           ▼
   ┌─────────────────────────────────────┐               │  ┌────────────────────────────────────────┐
   │ movimiento_pollo_engorde            │ ◄─────────────┴──┤ (mismas filas, diferente foreign key)  │
   │  - tipo_movimiento ∈ {Venta,Despacho,Retiro,Traslado} │
   │  - cantidad_hembras+machos+mixtas → aves vendidas     │
   │  - peso_bruto − peso_tara          → kg carne         │
   │  - edad_aves                       → edad promedio    │
   │  - fecha_movimiento                → fecha cierre     │
   └─────────────────────────────────────┘
            │
            │ lote_ave_engorde_id
            ▼
   ┌─────────────────────────────────────┐
   │ seguimiento_diario_aves_engorde     │ (consumo, mortalidad lote padre)
   │  - mortalidad_hembras+machos        │
   │  - sel_h + sel_m                    │
   │  - consumo_kg_hembras+machos        │
   └─────────────────────────────────────┘
            │
            │ lote_reproductora_ave_engorde_id
            ▼
   ┌─────────────────────────────────────────────────────┐
   │ seguimiento_diario_lote_reproductora_aves_engorde   │ (consumo, mortalidad reproductora)
   └─────────────────────────────────────────────────────┘
            │
            │ granja_id + galpon_id
            ▼
   ┌─────────────────────────────────────┐
   │ galpones (ancho, largo)             │ → metros²
   └─────────────────────────────────────┘
```

---

## 2. Tabla maestra: campo → fuente → fórmula

Mapa exhaustivo de los 22 campos del `IndicadorEcuadorDto` para un **lote padre** (`LoteAveEngorde`). El equivalente para `LoteReproductoraAveEngorde` reemplaza el filtro `lote_ave_engorde_origen_id` por `lote_reproductora_ave_engorde_origen_id`.

| # | Campo DTO | Tipo | Tabla(s) fuente | Columna(s) | Fórmula / agregación | Función C# actual |
|---|-----------|------|----------------|------------|----------------------|---------------------|
| 1 | `GranjaId` | int | `lote_ave_engorde` | `granja_id` | directo | `LoteAveEngorde.GranjaId` |
| 2 | `GranjaNombre` | string | `farms` | `name` | JOIN `farms ON id = granja_id` | `lote.Farm.Name` |
| 3 | `LoteId` | int | `lote_ave_engorde` | `lote_ave_engorde_id` | directo | `LoteAveEngordeId` |
| 4 | `LoteNombre` | string | `lote_ave_engorde` | `lote_nombre` | directo | `LoteNombre` |
| 5 | `GalponId` | string? | `lote_ave_engorde` | `galpon_id` | directo | `GalponId` |
| 6 | `GalponNombre` | string | `galpones` | `galpon_nombre` | JOIN `galpones ON galpon_id` | `lote.Galpon.GalponNombre` |
| 7 | `AvesEncasetadas` | int | `lote_ave_engorde` | `aves_encasetadas` (fallback: `hembras_l+machos_l+mixtas`) | `COALESCE(aves_encasetadas, hembras_l+machos_l+mixtas, 0)` | línea 490, 924 |
| 8 | `AvesSacrificadas` | int | `movimiento_pollo_engorde` | `cantidad_hembras+cantidad_machos+cantidad_mixtas` | `SUM(...) WHERE tipo_movimiento IN ('Venta','Despacho','Retiro') AND estado<>'Cancelado' AND deleted_at IS NULL AND lote_ave_engorde_origen_id = :loteId` | `AvesSacrificadasPolloEngordeAsync` (línea 696) |
| 9 | `Mortalidad` | int | `seguimiento_diario_aves_engorde` | `mortalidad_hembras+mortalidad_machos+sel_h+sel_m` | `SUM(mort_h+mort_m+sel_h+sel_m) WHERE lote_ave_engorde_id = :loteId` | `MortalidadSeleccionAvesEngordeAsync` (línea 711) |
| 10 | `MortalidadPorcentaje` | decimal | calculado | — | `Mortalidad / AvesEncasetadas * 100` (0 si AvesEncasetadas=0) | línea 495 |
| 11 | `SupervivenciaPorcentaje` | decimal | calculado | — | `(AvesEncasetadas - Mortalidad) / AvesEncasetadas * 100` | línea 496 |
| 12 | `ConsumoTotalAlimentoKg` | decimal | `seguimiento_diario_aves_engorde` | `consumo_kg_hembras+consumo_kg_machos` | `SUM(consumo_kg_h+consumo_kg_m) WHERE lote_ave_engorde_id = :loteId` | `ConsumoPolloEngordeAsync` (línea 731) |
| 13 | `ConsumoAveGramos` | decimal | calculado | — | `ConsumoTotalAlimentoKg / AvesSacrificadas * 1000` | línea 499 |
| 14 | `KgCarnePollos` | decimal | `movimiento_pollo_engorde` | `peso_bruto - peso_tara` | `SUM(peso_bruto - peso_tara) WHERE peso_bruto AND peso_tara IS NOT NULL AND tipo IN (Venta,Despacho,Retiro)` | `KgCarneYEdadPolloEngordeAsync` (línea 750) |
| 15 | `PesoPromedioKilos` | decimal | calculado | — | `KgCarnePollos / AvesSacrificadas` | línea 502 |
| 16 | `Conversion` | decimal | calculado | — | `ConsumoTotalAlimentoKg / KgCarnePollos` | línea 503 |
| 17 | `ConversionAjustada2700` | decimal | calculado | — | `Conversion + (PesoAjuste - PesoPromedio) / DivisorAjuste`<br>defaults: PesoAjuste=2.7, DivisorAjuste=4.5 | `CalcularConversionAjustada` (línea 1160) |
| 18 | `EdadPromedio` | decimal | `movimiento_pollo_engorde` | `edad_aves` | `AVG(edad_aves) WHERE tipo IN (Venta,Despacho,Retiro) AND edad_aves IS NOT NULL` | misma `KgCarneYEdadPolloEngordeAsync` (línea 763) |
| 19 | `MetrosCuadrados` | decimal | `galpones` | `ancho * largo` | `(ancho)::numeric * (largo)::numeric` con `galpon_id` específico o suma de todos los galpones de la granja si lote no tiene galpón | `CalcularMetrosCuadradosAsync` (línea 1166) |
| 20 | `AvesPorMetroCuadrado` | decimal | calculado | — | `AvesSacrificadas / MetrosCuadrados` | línea 507 |
| 21 | `KgPorMetroCuadrado` | decimal | calculado | — | `KgCarnePollos / MetrosCuadrados` | línea 508 |
| 22 | `EficienciaAmericana` | decimal | calculado | — | `(PesoPromedio / Conversion) * 100` | línea 510 |
| 23 | `EficienciaEuropea` | decimal | calculado | — | `((PesoPromedio * SupervivenciaPorcentaje) / (EdadPromedio * Conversion)) * 100` | línea 511 |
| 24 | `IndiceProductividad` | decimal | calculado | — | `(PesoPromedio / Conversion²) * 100` | línea 512 |
| 25 | `GananciaDia` | decimal | calculado | — | `(PesoPromedio / EdadPromedio) * 1000` | línea 513 |
| 26 | `FechaInicioLote` | DateTime? | `lote_ave_engorde` | `fecha_encaset` | directo | `FechaEncaset` |
| 27 | `FechaCierreLote` | DateTime? | `movimiento_pollo_engorde` | `fecha_movimiento` | `MAX(fecha_movimiento) WHERE tipo IN (Venta,Despacho,Retiro) AND estado<>'Cancelado'`; fallback: último seguimiento o último movimiento o fecha encaset si lote cerrado | `FechaCierrePolloEngordeAsync` (línea 768) + `FechaUltimaActividadLotePadreAveEngordeAsync` (790) |
| 28 | `LoteCerrado` | bool | calculado | — | `(AvesEncasetadas - Mortalidad - AvesSacrificadas - AvesTrasladadasAReproductores) ≤ 0` **OR** todos los reproductores asociados tienen aves=0 | líneas 515-522 |
| 29 | `FechaAlistamiento` | DateTime? | `lote_ave_engorde` | `fecha_alistamiento` | directo | `FechaAlistamiento` |

### Auxiliares (no van al DTO pero son intermedios)

| Campo intermedio | Fuente | Uso |
|------------------|--------|-----|
| `AvesTrasladadasAReproductores` | `movimiento_pollo_engorde WHERE tipo='Traslado' AND lote_ave_engorde_origen_id=:id AND lote_reproductora_ave_engorde_destino_id IS NOT NULL` | Descontar del saldo del padre para considerarlo cerrado | `AvesTrasladadasDesdePadreHaciaReproductoresAsync` (línea 663) |

---

## 3. Filtros administrativos (`TipoFiltroLotes`)

| Valor request | Significado | Función SQL | Manejo C# (línea actual) |
|---------------|-------------|-------------|--------------------------|
| `"todos"` | Sin filtro | `WHERE TRUE` | `ResolveFiltroLotes` → `(false, false)` |
| `"aves_cero"` | Saldo físico exactamente 0 | `WHERE saldo_aves = 0` | `(true, false)` |
| `"cerrados"` (default) | Cierre administrativo: `estado='Cerrado'` OR `liquidado_at IS NOT NULL` OR `loteCerrado=true` OR `ratio_vendidas >= 0.9` | `WHERE (estado_operativo_lote <> 'Abierto' OR liquidado_at IS NOT NULL OR aves_cero OR aves_sacrificadas/aves_encasetadas >= 0.9)` | `(true, true)` |

---

## 4. Endpoints expuestos y mapeo a primitivas

```
POST /api/IndicadorEcuador/calcular                          → CalcularIndicadoresAsync (lista por filtros)
POST /api/IndicadorEcuador/consolidado                       → CalcularConsolidadoAsync (totales granja)
POST /api/IndicadorEcuador/liquidacion-periodo               → CalcularLiquidacionPeriodoAsync (rango fechas)
GET  /api/IndicadorEcuador/lotes-cerrados                    → ObtenerLotesCerradosAsync
POST /api/IndicadorEcuador/indicadores-pollo-engorde-por-lote-padre → CalcularIndicadoresPolloEngordePorLotePadreAsync
POST /api/IndicadorEcuador/liquidacion-pollo-engorde-reporte → LiquidacionPolloEngordeReporteAsync (Modo: UnLote / Rango / TodosLiquidados)
```

Todos terminan llamando a **una** de dos primitivas:
- `CalcularIndicadorLoteAveEngordeAsync(lote, ...)` → línea 480 (lote padre)
- `CalcularIndicadorLoteReproductoraAveEngordeAsync(rep, ...)` → línea 583 (lote reproductora)

Esto significa que migrar estas dos a SQL cubre el 100% de los endpoints.

---

## 5. Validación de datos en BD local — Caso Lote 19 (Ecuador, granja 38)

Validé el flujo contra datos reales para asegurar que las fórmulas dan resultados consistentes.

| KPI | Valor calculado | Origen verificado |
|-----|-----------------|--------------------|
| `AvesEncasetadas` | **51,438** | `lote_ave_engorde.aves_encasetadas` (campo explícito, no fallback) |
| `AvesSacrificadas` | **48,605** | 42 movimientos tipo `Venta` |
| `Mortalidad` (mort + sel) | **2,832** | `SUM(mortalidad_hembras+mortalidad_machos+sel_h+sel_m)` desde `seguimiento_diario_aves_engorde` |
| **Saldo físico** | `51,438 - 48,605 - 2,832 = 1` | ≈ 0 ✅ Lote cierra correctamente |
| `ConsumoTotalAlimentoKg` | **232,175 kg** | `SUM(consumo_kg_hembras+consumo_kg_machos)` |
| `KgCarnePollos` | **138,618 kg** | `SUM(peso_bruto - peso_tara)` desde movs |
| `Conversion` | **232,175 / 138,618 = 1.6749** | Excelente (objetivo ≤1.8) |
| `PesoPromedio` | **138,618 / 48,605 = 2.852 kg** | Apropiado para sacrificio (~40 días) |
| `ConsumoAveGramos` | **232,175 / 48,605 × 1000 = 4,776 g** | Consistente con conversión |

**Conclusión:** las fórmulas del servicio **son matemáticamente correctas** para lotes "limpios". El bug real está en la performance, no en los números.

### ⚠️ Hallazgos de calidad de datos (para validar después)

- 74 lotes en BD local, **100 % tienen `aves_encasetadas` poblado** → el fallback `H+M+Mixtas` es código muerto en producción. Considerar eliminarlo en el refactor.
- Solo existe `tipo_movimiento='Venta'` en `movimiento_pollo_engorde` (706 filas). No hay `Despacho`, `Retiro`, `Traslado` actualmente. El filtro `IN (Venta, Despacho, Retiro)` es defensivo pero hoy solo `Venta` aporta.
- 11 lotes `Cerrado` por estado_operativo + 12 con `liquidado_at` poblado. Ambos criterios tienen overlap parcial; el cierre administrativo del servicio usa OR correctamente.

---

## 6. Plan de migración a función SQL

### 6.1 Función principal — firma propuesta

```sql
CREATE OR REPLACE FUNCTION fn_indicadores_pollo_engorde(
    p_lote_id            INT,
    p_peso_ajuste        NUMERIC DEFAULT 2.7,
    p_divisor_ajuste     NUMERIC DEFAULT 4.5
)
RETURNS TABLE (
    -- Identificación
    granja_id              INT,
    granja_nombre          TEXT,
    lote_id                INT,
    lote_nombre            TEXT,
    galpon_id              TEXT,
    galpon_nombre          TEXT,
    -- Datos básicos
    aves_encasetadas       INT,
    aves_sacrificadas      INT,
    aves_trasladadas_repr  INT,
    mortalidad             INT,
    mortalidad_pct         NUMERIC(10,4),
    supervivencia_pct      NUMERIC(10,4),
    -- Consumo
    consumo_total_kg       NUMERIC(18,3),
    consumo_ave_g          NUMERIC(18,3),
    -- Producción
    kg_carne               NUMERIC(18,3),
    peso_promedio_kg       NUMERIC(10,4),
    conversion             NUMERIC(10,4),
    conversion_ajustada    NUMERIC(10,4),
    -- Edad y área
    edad_promedio          NUMERIC(10,2),
    metros_cuadrados       NUMERIC(12,3),
    aves_por_m2            NUMERIC(10,4),
    kg_por_m2              NUMERIC(10,4),
    -- Eficiencias
    eficiencia_americana   NUMERIC(10,4),
    eficiencia_europea     NUMERIC(10,4),
    indice_productividad   NUMERIC(10,4),
    ganancia_dia           NUMERIC(10,4),
    -- Fechas y estado
    fecha_encaset          DATE,
    fecha_cierre           DATE,
    lote_cerrado           BOOLEAN,
    fecha_alistamiento     DATE,
    -- Marcadores administrativos (para filtros)
    estado_operativo_lote  TEXT,
    liquidado_at           TIMESTAMPTZ,
    ratio_sacrificadas     NUMERIC(10,4) -- aves_sacrificadas / aves_encasetadas
);
```

### 6.2 Esqueleto del cuerpo (PL/pgSQL con CTEs)

```sql
LANGUAGE SQL STABLE AS $$
WITH
  -- 1) Datos base del lote
  lote AS (
    SELECT l.lote_ave_engorde_id AS lote_id,
           l.granja_id, f.name AS granja_nombre,
           l.galpon_id, g.galpon_nombre,
           l.lote_nombre,
           COALESCE(l.aves_encasetadas,
                    COALESCE(l.hembras_l,0)+COALESCE(l.machos_l,0)+COALESCE(l.mixtas,0)) AS aves_encasetadas,
           l.fecha_encaset::DATE AS fecha_encaset,
           l.fecha_alistamiento::DATE AS fecha_alistamiento,
           l.estado_operativo_lote, l.liquidado_at
    FROM lote_ave_engorde l
    JOIN farms f ON f.id = l.granja_id
    LEFT JOIN galpones g ON g.galpon_id = l.galpon_id
    WHERE l.lote_ave_engorde_id = p_lote_id AND l.deleted_at IS NULL
  ),
  -- 2) Agregados desde movimientos (ventas, despachos, retiros, traslados)
  movs_agg AS (
    SELECT
      COALESCE(SUM(m.cantidad_hembras+m.cantidad_machos+m.cantidad_mixtas)
               FILTER (WHERE m.tipo_movimiento IN ('Venta','Despacho','Retiro')), 0)::INT AS aves_sacrificadas,
      COALESCE(SUM(GREATEST(0, COALESCE(m.peso_bruto,0)-COALESCE(m.peso_tara,0)))
               FILTER (WHERE m.tipo_movimiento IN ('Venta','Despacho','Retiro')
                         AND m.peso_bruto IS NOT NULL AND m.peso_tara IS NOT NULL), 0)::NUMERIC AS kg_carne,
      AVG(m.edad_aves) FILTER (WHERE m.tipo_movimiento IN ('Venta','Despacho','Retiro') AND m.edad_aves IS NOT NULL) AS edad_promedio,
      MAX(m.fecha_movimiento) FILTER (WHERE m.tipo_movimiento IN ('Venta','Despacho','Retiro'))::DATE AS fecha_cierre,
      COALESCE(SUM(m.cantidad_hembras+m.cantidad_machos+m.cantidad_mixtas)
               FILTER (WHERE m.tipo_movimiento = 'Traslado' AND m.lote_reproductora_ave_engorde_destino_id IS NOT NULL), 0)::INT AS aves_trasladadas_repr
    FROM movimiento_pollo_engorde m
    WHERE m.lote_ave_engorde_origen_id = p_lote_id
      AND m.deleted_at IS NULL AND m.estado <> 'Cancelado'
  ),
  -- 3) Agregados desde seguimiento diario
  seg_agg AS (
    SELECT
      COALESCE(SUM(COALESCE(s.mortalidad_hembras,0)+COALESCE(s.mortalidad_machos,0)+COALESCE(s.sel_h,0)+COALESCE(s.sel_m,0)),0)::INT AS mortalidad,
      COALESCE(SUM(COALESCE(s.consumo_kg_hembras,0)+COALESCE(s.consumo_kg_machos,0)),0)::NUMERIC AS consumo_total_kg,
      MAX(s.fecha)::DATE AS ult_seguimiento_fecha
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id
  ),
  -- 4) Metros cuadrados del galpón
  area AS (
    SELECT
      COALESCE(
        (SELECT NULLIF(g.ancho,'')::NUMERIC * NULLIF(g.largo,'')::NUMERIC
         FROM galpones g, lote l WHERE g.galpon_id = l.galpon_id AND g.deleted_at IS NULL),
        (SELECT SUM(NULLIF(g.ancho,'')::NUMERIC * NULLIF(g.largo,'')::NUMERIC)
         FROM galpones g, lote l WHERE g.granja_id = l.granja_id AND g.deleted_at IS NULL),
        0
      ) AS metros_cuadrados
  ),
  -- 5) Cálculo de cierre administrativo y derivados
  calc AS (
    SELECT
      l.*, m.*, s.*, ar.metros_cuadrados,
      GREATEST(0, l.aves_encasetadas - s.mortalidad - m.aves_sacrificadas - m.aves_trasladadas_repr) AS saldo_aves
    FROM lote l, movs_agg m, seg_agg s, area ar
  )
SELECT
  c.granja_id, c.granja_nombre, c.lote_id, c.lote_nombre, c.galpon_id, c.galpon_nombre,
  c.aves_encasetadas, c.aves_sacrificadas, c.aves_trasladadas_repr, c.mortalidad,
  CASE WHEN c.aves_encasetadas > 0 THEN ROUND((c.mortalidad::NUMERIC / c.aves_encasetadas) * 100, 4) ELSE 0 END,
  CASE WHEN c.aves_encasetadas > 0 THEN ROUND(((c.aves_encasetadas - c.mortalidad)::NUMERIC / c.aves_encasetadas) * 100, 4) ELSE 0 END,
  c.consumo_total_kg,
  CASE WHEN c.aves_sacrificadas > 0 THEN ROUND((c.consumo_total_kg / c.aves_sacrificadas) * 1000, 3) ELSE 0 END,
  c.kg_carne,
  CASE WHEN c.aves_sacrificadas > 0 THEN ROUND(c.kg_carne / c.aves_sacrificadas, 4) ELSE 0 END AS peso_promedio,
  CASE WHEN c.kg_carne > 0 THEN ROUND(c.consumo_total_kg / c.kg_carne, 4) ELSE 0 END AS conversion,
  -- Conversión ajustada = conversion + (PESO_AJUSTE - peso_promedio) / DIVISOR_AJUSTE
  ... etc.
  c.fecha_encaset,
  COALESCE(c.fecha_cierre, CASE WHEN c.saldo_aves = 0 THEN COALESCE(c.ult_seguimiento_fecha, c.fecha_encaset) END),
  c.saldo_aves = 0 AS lote_cerrado,
  c.fecha_alistamiento,
  c.estado_operativo_lote, c.liquidado_at,
  CASE WHEN c.aves_encasetadas > 0 THEN c.aves_sacrificadas::NUMERIC / c.aves_encasetadas ELSE 0 END
FROM calc c;
$$;
```

### 6.3 Variante bulk para reportes consolidados

```sql
CREATE OR REPLACE FUNCTION fn_indicadores_pollo_engorde_bulk(
    p_company_id   INT,
    p_granja_id    INT          DEFAULT NULL,
    p_nucleo_id    TEXT         DEFAULT NULL,
    p_galpon_id    TEXT         DEFAULT NULL,
    p_fecha_desde  DATE         DEFAULT NULL,
    p_fecha_hasta  DATE         DEFAULT NULL,
    p_tipo_filtro  TEXT         DEFAULT 'cerrados', -- 'todos'|'aves_cero'|'cerrados'
    p_peso_ajuste  NUMERIC      DEFAULT 2.7,
    p_divisor_ajuste NUMERIC    DEFAULT 4.5
)
RETURNS SETOF [misma tupla que fn_indicadores_pollo_engorde]
LANGUAGE SQL STABLE AS $$
  WITH lotes_filtrados AS (
    SELECT lote_ave_engorde_id
    FROM lote_ave_engorde
    WHERE company_id = p_company_id AND deleted_at IS NULL
      AND (p_granja_id IS NULL OR granja_id = p_granja_id)
      AND (p_nucleo_id IS NULL OR nucleo_id = p_nucleo_id)
      AND (p_galpon_id IS NULL OR galpon_id = p_galpon_id)
      AND (p_fecha_desde IS NULL OR fecha_encaset::DATE >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR fecha_encaset::DATE <= p_fecha_hasta)
  )
  SELECT i.*
  FROM lotes_filtrados lf
  CROSS JOIN LATERAL fn_indicadores_pollo_engorde(lf.lote_ave_engorde_id, p_peso_ajuste, p_divisor_ajuste) i
  WHERE
    CASE p_tipo_filtro
      WHEN 'aves_cero' THEN i.lote_cerrado
      WHEN 'cerrados'  THEN (i.estado_operativo_lote <> 'Abierto'
                              OR i.liquidado_at IS NOT NULL
                              OR i.lote_cerrado
                              OR i.ratio_sacrificadas >= 0.9)
      ELSE TRUE -- 'todos'
    END;
$$;
```

### 6.4 Función gemela para lote reproductora

```sql
CREATE OR REPLACE FUNCTION fn_indicadores_pollo_engorde_reproductora(
    p_lote_reproductora_id INT,
    p_peso_ajuste          NUMERIC DEFAULT 2.7,
    p_divisor_ajuste       NUMERIC DEFAULT 4.5
)
RETURNS TABLE (... misma estructura ...);
```

Reemplaza:
- `aves_encasetadas` ← `aves_inicio_hembras + aves_inicio_machos + mixtas`
- movs filtra `lote_reproductora_ave_engorde_origen_id = :id`
- seguimiento usa `seguimiento_diario_lote_reproductora_aves_engorde`

---

## 7. Cambios en el servicio C# (post función SQL)

El servicio queda como un wrapper de ~200 líneas (vs 1233 actuales):

```csharp
public class IndicadorEcuadorService : IIndicadorEcuadorService
{
    private readonly ZooSanMarinoContext _ctx;

    public async Task<IndicadorEcuadorDto?> CalcularIndicadorLoteAsync(int loteId, decimal pesoAjuste, decimal divisorAjuste)
    {
        var sql = "SELECT * FROM fn_indicadores_pollo_engorde({0}, {1}, {2})";
        return await _ctx.Database
            .SqlQueryRaw<IndicadorEcuadorRow>(sql, loteId, pesoAjuste, divisorAjuste)
            .AsNoTracking()
            .FirstOrDefaultAsync()
            ... map a IndicadorEcuadorDto ...;
    }

    public async Task<IEnumerable<IndicadorEcuadorDto>> CalcularIndicadoresBulkAsync(IndicadorEcuadorRequest req)
    {
        var sql = "SELECT * FROM fn_indicadores_pollo_engorde_bulk({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})";
        return (await _ctx.Database.SqlQueryRaw<IndicadorEcuadorRow>(sql, ...).ToListAsync())
                  .Select(MapRowToDto);
    }
}
```

Consolidado (`CalcularConsolidadoAsync`) y liquidación por período se mantienen en C# (son agregados sobre la salida de la función, no requieren SQL nuevo).

---

## 8. Estimación de ganancia de performance

| Escenario | Hoy (C#) | Post función SQL | Reducción |
|-----------|----------|-------------------|-----------|
| 1 lote padre + 0 reproductoras | 8-9 queries | 1 query | **~89 %** |
| 1 lote padre + 5 reproductoras | 8 + 5×8 = 48 queries | 1 + 5 = 6 queries (o 1 con bulk) | **~98 %** |
| Granja completa con 30 lotes | ~250 queries | 1 query (bulk) | **~99.6 %** |
| Reporte TodosLiquidados (80 lotes) | ~640 queries | 1 query (bulk) | **~99.8 %** |

Latencia estimada local (perfilada con `EXPLAIN ANALYZE` esperada):
- Reporte 30 lotes hoy: **~4-7 s**
- Reporte 30 lotes con bulk: **~150-400 ms**

---

## 9. Checklist de implementación (futura fase)

Esto es lo que debería incluir el plan de ejecución cuando se decida implementar:

- [ ] **CA1** — Crear `backend/sql/fn_indicadores_pollo_engorde.sql` con la función + variante reproductora + bulk.
- [ ] **CA2** — Crear `backend/sql/fn_indicadores_pollo_engorde_tests.sql` con tests SQL que cubran:
  - Lote 19 (granja 38) → conversion = 1.6749 ± 0.001
  - Lote sin ventas → todos los KPIs en 0, `lote_cerrado = false`
  - Lote con reproductoras → padre incluye trasladadas, no doble cuenta
  - Lote con `liquidado_at` pero saldo > 0 → `lote_cerrado = false`, filtro `'cerrados'` lo incluye
- [ ] **CA3** — Migración EF Core `Migrations/YYMMDDHHMMSS_AddFnIndicadoresPolloEngorde.cs` con `DROP FUNCTION IF EXISTS` + `CREATE` (idempotente, embed del SQL).
- [ ] **CA4** — Refactor `IndicadorEcuadorService.cs`: borrar primitivas privadas (líneas 663-811, 1039-1221), reemplazar `CalcularIndicadorLoteAveEngordeAsync` y `CalcularIndicadorLoteReproductoraAveEngordeAsync` por llamadas a las funciones SQL.
- [ ] **CA5** — Eliminar fallback `H+M+Mixtas` para `AvesEncasetadas` (validado: dead code en data actual).
- [ ] **CA6** — Endpoints `/calcular`, `/consolidado`, `/liquidacion-periodo`, `/lotes-cerrados`, `/indicadores-pollo-engorde-por-lote-padre`, `/liquidacion-pollo-engorde-reporte` siguen devolviendo el mismo JSON. **Compatibilidad backwards 100 %**.
- [ ] **CA7** — Tests E2E que comparen output JSON antes/después con tolerancia decimal de 0.001 — no debe haber regresiones de valor.
- [ ] **CA8** — Benchmark con `dotnet bench` o curl + time: reporte 30 lotes < 500 ms post-refactor.
- [ ] **CA9** — Frontend (Angular): los servicios `indicador-ecuador.service.ts` no requieren cambios — consumen el mismo JSON.

---

## 10. Riesgos y decisiones pendientes

| Riesgo | Mitigación |
|--------|-----------|
| Diferencias decimales entre `decimal` C# y `numeric` PG por rounding | Definir todos los `ROUND(..., 4)` en SQL coherentes con el comportamiento actual (C# usa decimal sin rounding intermedio). Validar con CA7. |
| `EXPLAIN ANALYZE` indica que el filtro `tipo_movimiento IN (...)` no aprovecha índice | Crear índice parcial `CREATE INDEX ix_mpe_lote_origen_tipo ON movimiento_pollo_engorde(lote_ave_engorde_origen_id, tipo_movimiento) WHERE deleted_at IS NULL AND estado <> 'Cancelado';` |
| Cuando el lote tiene reproductoras, el padre suma 0 directo pero las reproductoras aportan: el endpoint `indicadores-pollo-engorde-por-lote-padre` espera ambos | La migración mantiene las dos funciones (`fn_indicadores_pollo_engorde` + `fn_indicadores_pollo_engorde_reproductora`); el wrapper C# las orquesta para el endpoint compuesto. |
| Filtro `TipoLote` (`"Levante"`, `"Produccion"`, `"Reproductora"`) usa entidades viejas (`SeguimientoLoteLevante`, `SeguimientoProduccion`, `LoteSeguimientos`) que NO son las de Pollo Engorde Ecuador | El servicio tiene **dos caminos paralelos**: el viejo (`CalcularIndicadorPorLoteAsync`, líneas 913-1222) opera sobre `lotes` (general), no `lote_ave_engorde`. Decidir si el refactor SQL solo cubre los caminos Pollo Engorde (V2, líneas 437-580) y deja el viejo intacto o si también se migra. **Sugerencia:** solo migrar V2; marcar V1 como obsoleto. |
| El método `CalcularMetrosCuadradosAsync` usa `decimal.TryParse` para `ancho`/`largo` que son `varchar(32)` | En SQL usar `NULLIF(g.ancho,'')::NUMERIC` con `EXCEPTION WHEN invalid_text_representation THEN 0`. O migrar las columnas `ancho/largo` a `NUMERIC` (mejor a largo plazo). |
| Función SQL `STABLE` correcto vs `VOLATILE`: los datos del backing no cambian dentro de la transacción | Usar `STABLE` — permite que PG planifique y cachee llamadas dentro del mismo statement. |

---

## 11. Definición de DONE (cuando se implemente)

- [ ] Output JSON 100 % equivalente para todos los endpoints (tolerancia 0.001).
- [ ] Reducción de queries por reporte ≥ 95 %.
- [ ] Latencia P95 reporte 30 lotes ≤ 500 ms (vs ~5 s hoy).
- [ ] Migración EF Core aplica en local y en deploy de prod sin error.
- [ ] El servicio C# reduce su LOC en ≥ 70 % (de 1233 a < 400 líneas).
- [ ] Sin regresión en los endpoints de Liquidación que usan las primitivas.

---

## 12. Comandos útiles durante el refactor

```bash
# Verificar la función localmente
PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal \
  -c "SELECT * FROM fn_indicadores_pollo_engorde(19);"

# Comparar bulk con servicio actual
curl -X POST 'http://localhost:5002/api/IndicadorEcuador/calcular' \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  -d '{"granjaId":38,"tipoFiltroLotes":"cerrados"}' \
  | jq '[.[] | {lote: .loteId, conv: .conversion, sacr: .avesSacrificadas}]' \
  > /tmp/output_csharp.json

PGPASSWORD=123456789 psql -h localhost -p 5433 -U postgres -d sanmarinoapplocal -At \
  -c "SELECT json_agg(json_build_object('lote', lote_id, 'conv', conversion, 'sacr', aves_sacrificadas))
      FROM fn_indicadores_pollo_engorde_bulk(<companyId>, 38, NULL, NULL, NULL, NULL, 'cerrados');" \
  > /tmp/output_sql.json

diff <(jq -S . /tmp/output_csharp.json) <(jq -S . /tmp/output_sql.json)
```

---

## 13. Referencias del repo

- Servicio actual: [backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs) (1233 líneas)
- Controller: [backend/src/ZooSanMarino.API/Controllers/IndicadorEcuadorController.cs](backend/src/ZooSanMarino.API/Controllers/IndicadorEcuadorController.cs)
- DTOs: [backend/src/ZooSanMarino.Application/DTOs/IndicadorEcuadorDto.cs](backend/src/ZooSanMarino.Application/DTOs/IndicadorEcuadorDto.cs)
- Interface: [backend/src/ZooSanMarino.Application/Interfaces/IIndicadorEcuadorService.cs](backend/src/ZooSanMarino.Application/Interfaces/IIndicadorEcuadorService.cs)
- Frontend feature: [frontend/src/app/features/indicador-ecuador/](frontend/src/app/features/indicador-ecuador/)

## 14. Plan análogo previo en el repo (para guía)

La función SQL `fn_seguimiento_diario_engorde` (en `backend/sql/fn_seguimiento_diario_engorde.sql`) y su migración `20260528212753_FixFnSeguimientoEngordeYRecalcularSaldosMasivo.cs` siguieron exactamente el mismo patrón:
1. Identificar primitivas C# duplicadas.
2. Encapsular en función PL/SQL con CTEs.
3. Migración EF Core idempotente con DROP + CREATE.
4. Wrapper C# que llama a `Database.SqlQueryRaw`.

Este plan replica el patrón ya probado.

---
---

# 📋 PARTE B — Requerimiento adicional Ecuador (2026-05-29): Mermas, Ajuste de Aves y Sobrante de Aves

**Fuente:** Hilo de correo *"REVISION DE REQUERIMIENTOS APLICATIVO ITALGRANJAS-ECUADOR"* (Génesis Parrales — Asistente de Costos Avícolas ECU-ITALCOL / Lady Karina Rojas — Directora Desarrollo y Analítica). Archivo: `REVISION DE REQUERIMIENTOS APLICATIVO ITALGRANJAS-ECUADOR.docx`.
**Ejemplo de referencia usado en todo el documento:** lote **02** de la granja **Km 61** (reporte "Liquidación Técnica Pollo Engorde" del aplicativo vs "Análisis Técnico" de Costos).
**Alcance:** **Pollo Engorde** únicamente — toca **Lote Pollo Engorde** (`LoteAveEngorde`), **Seguimiento Pollo Engorde / Movimientos** (venta-despacho) y **Liquidación Técnica Ecuador** (`IndicadorEcuadorService`). Se construye **encima** del mapeo de la Parte A (la función SQL debe devolver también los campos nuevos).
**Estado:** 📐 Diseño / pendiente de aprobación. NO se implementa código hasta validar las decisiones abiertas (§B.7).

---

## B.0 Resumen ejecutivo de los dos requerimientos

Costos pide **dos cosas independientes** que conviven en el mismo flujo de liquidación de pollo engorde:

1. **Requerimiento 1 — Mermas + 11 campos nuevos en la Liquidación Técnica.** El reporte del aplicativo no muestra la **merma** (en unidades y en kilos) que se origina cuando se factura con el **peso recibido en planta del cliente** (lo que sale de granja ≠ lo que recibe el cliente). Tampoco muestra el **Ajuste de aves** ni varios campos que Costos sí calcula en su hoja Excel. Son **11 campos**, de los cuales **5 son del bloque Mermas**.
2. **Requerimiento 2 — Sobrante de aves.** El aplicativo **bloquea** registrar más ventas/despachos de las aves encasetadas por galpón. En la práctica un galpón puede tener **sobrante** y otro **faltante**, y la venta real supera lo encasetado. Hay que **permitir vender de más** y **guardar ese excedente** ("aves agregadas de más") en el lote, conservando el histórico de cuántas aves se encasetaron originalmente.

> 📌 Cita clave del correo (merma): *"El valor de merma corresponde a la diferencia entre el peso y aves que salen de granja y lo que recibe el cliente… esto no debe afectar al registro de producción diario, por lo cual sugiero estos datos se coloquen cuando ya el lote está por liquidar y que sea **costos** quien lo digite… estos datos afectan al resultado de ajuste de aves."*
>
> 📌 Cita clave del correo (sobrante): *"El aplicativo no permite adicionar más venta de aves a lo encasetado… ejemplo en el lote 02 del km 61 tenemos un sobrante en el galpón #1 de **8 aves**, se vendió un total de **19596** aves, sin embargo en el aplicativo solo permitió registrar **19588** aves, afectando directamente al último despacho el cual debió ser de **230** aves y solo se pudo registrar **222**."*

---

## B.1 Requerimiento 1 — Mermas y nuevos campos de la Liquidación Técnica

### B.1.1 Los 11 campos nuevos (mapeo campo → origen → fórmula → ejemplo)

Validado contra la imagen "ANÁLISIS TÉCNICO" de Costos (lote 02 Km 61): aves encasetadas **44.301**, aves vendidas **42.532**, mortalidad **1.772**, merma und **5**, merma kg **10,66**.

| # | Campo (reporte Costos) | Tipo | Ingreso/Cálculo | Fórmula / origen | Ejemplo |
|---|------------------------|------|-----------------|------------------|---------|
| 1 | **Fecha de alistamiento** | fecha | Existe en entidad | `lote_ave_engorde.fecha_alistamiento` (sólo mostrar en reporte) | 13/3/2026 |
| 2 | **Fecha de encasetamiento** | fecha | Existe en entidad | `lote_ave_engorde.fecha_encaset` (sólo mostrar) | 23/1/2026 |
| 3 | **Fecha de liquidación** | fecha | Existe en entidad | `lote_ave_engorde.liquidado_at` (sólo mostrar) | 13/5/2026 |
| 4 | **Días de engorde** | int | Cálculo | días entre `fecha_encaset` y la fecha de cierre/último despacho — **fórmula a confirmar** (§B.7-D3; el ejemplo muestra 51) | 51 |
| 5 | **Merma (unidades)** | int | **INPUT (Costos)** | Lo digita Costos antes de liquidar. Diferencia de aves granja vs cliente | 5 |
| 6 | **Merma (kilos)** | decimal | **INPUT (Costos)** | Lo digita Costos antes de liquidar. Diferencia de kg granja vs cliente | 10,66 |
| 7 | **Merma (%)** | decimal | Cálculo | `merma_und / aves_vendidas × 100` (denominador a confirmar, §B.7-D4) | 0,01 |
| 8 | **Ajuste en aves** | int | Cálculo | `aves_encasetadas − aves_vendidas − mortalidad − merma_und` | −8 |
| 9 | **Porcentaje de ajuste** | decimal | Cálculo | `ajuste_aves / aves_encasetadas × 100` | −0,02 |
| 10 | **Producción kilo en pie** | decimal | Cálculo (ya existe) | = `KgCarnePollos` actual (kg que salen de granja, `SUM(peso_bruto − peso_tara)`) | 135.091 |
| 11 | **Total kilos despachados a cliente** | decimal | Cálculo | `produccion_kilo_en_pie − merma_kilos` | 135.080 |

**Verificación aritmética del ejemplo (todas dan exacto):**
- Ajuste en aves = 44.301 − 42.532 − 1.772 − 5 = **−8** ✅
- % de ajuste = −8 / 44.301 × 100 = **−0,018 ≈ −0,02** ✅
- Merma % = 5 / 42.532 × 100 = **0,0118 ≈ 0,01** ✅
- Total kg a cliente = 135.091 − 10,66 = **135.080,34 ≈ 135.080** ✅

> ⚠️ **Signo del Ajuste:** ajuste **negativo** ⇒ se vendieron/contabilizaron **más** aves que el saldo físico ⇒ es un **sobrante** (enlaza con el Requerimiento 2, §B.2). Ajuste positivo ⇒ faltante.

> ⚠️ **Refinamientos técnicos detectados en el comparativo (marcados en rojo "Requerimiento técnico — no quitar del reporte"), a confirmar en §B.7:**
> - **Edad ponderada** (Costos 43,41) vs **Edad promedio** actual del app (43,2): Costos usa promedio **ponderado por aves despachadas** `SUM(edad×aves)/SUM(aves)`, no `AVG(edad)` simple. → evaluar añadir `EdadPonderada` o ajustar el cálculo.
> - **Consumo ave** se muestra en **kg** en el reporte de Costos (4,85) y en **gramos** en el DTO actual (`ConsumoAveGramos`). Es el mismo número (4.850 g) — sólo es formato de presentación.
> - 🔴 **Discrepancia material a aclarar:** el "Kg carne pollo" del app en el ejemplo (**154.609**) ≠ "Producción kilo en pie" de Costos (**135.091**). La diferencia (~19.518 kg) **no** se explica por la merma (10,66 kg). Es un tema de **datos/fuente de kilos**, fuera del alcance de este requerimiento, pero **debe aclararse con Costos** porque `Producción kilo en pie` y `Peso promedio` dependen de cuál kg sea el correcto (§B.7-D5).

### B.1.2 ¿Dónde y quién ingresa la merma? — Modal "Liquidar lote"

El correo es explícito: **lo digita Costos**, **cuando el lote está por liquidar**, y **no debe afectar el registro de producción diario**. El punto de entrada natural ya existe:

- Componente: [modal-liquidacion-lote-engorde.component.ts](frontend/src/app/features/aves-engorde/pages/modal-liquidacion-lote-engorde/modal-liquidacion-lote-engorde.component.ts) → método `cerrarLote()` (línea 361) que llama `loteEngorde.cerrarLote(loteId, uid)`.
- Este modal se abre desde el **Seguimiento Diario Pollo Engorde** (opción de liquidar el lote), tal como indicó el usuario.

**Decisión de diseño (recomendada):** capturar `merma_unidades` y `merma_kilos` como **dos inputs nuevos** en ese modal y persistirlos en `lote_ave_engorde`. Como el correo dice "antes de dar click en liquidar", la merma debe poder guardarse **con el lote aún abierto**; por eso se expone un endpoint dedicado (`PUT …/merma`) además de aceptarla en el request de cierre. Ver §B.7-D1 para la alternativa (tabla separada).

### B.1.3 Modelo de datos — columnas nuevas en `lote_ave_engorde`

Tabla `public.lote_ave_engorde` (config: [LoteAveEngordeConfiguration.cs:47-49](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteAveEngordeConfiguration.cs)).

| Columna nueva | Tipo SQL | Nullable | Default | Propósito |
|---------------|----------|----------|---------|-----------|
| `merma_unidades` | `integer` | sí | `NULL` | Merma en aves (input Costos) |
| `merma_kilos` | `numeric(18,3)` | sí | `NULL` | Merma en kg (input Costos) |
| `merma_registrada_at` | `timestamptz` | sí | `NULL` | Auditoría: cuándo se digitó la merma |
| `merma_registrada_por_user_id` | `varchar(450)` | sí | `NULL` | Auditoría: quién (rol Costos) digitó |

Propiedades C# correspondientes en [LoteAveEngorde.cs](backend/src/ZooSanMarino.Domain/Entities/LoteAveEngorde.cs): `MermaUnidades` (`int?`), `MermaKilos` (`decimal?`), `MermaRegistradaAt` (`DateTime?`), `MermaRegistradaPorUserId` (`string?`). (Columna `aves_sobrante` se define en §B.2.2.)

### B.1.4 Cambios en DTO `IndicadorEcuadorDto`

Añadir al record [IndicadorEcuadorDto](backend/src/ZooSanMarino.Application/DTOs/IndicadorEcuadorDto.cs:23) (todos al final, con default para no romper llamadas):

```csharp
// Mermas y ajuste (Ecuador — requerimiento Costos 2026-05)
int     MermaUnidades = 0,
decimal MermaKilos = 0,
decimal MermaPorcentaje = 0,           // merma_und / aves_vendidas × 100
int     AjusteAves = 0,                 // encasetadas − vendidas − mortalidad − merma_und
decimal PorcentajeAjuste = 0,           // ajuste / encasetadas × 100
decimal ProduccionKiloEnPie = 0,        // = KgCarnePollos (kg que salen de granja)
decimal TotalKilosDespachadosCliente = 0, // produccion − merma_kilos
int     DiasEngorde = 0,
DateTime? FechaLiquidacion = null,
int     AvesSobrante = 0                 // §B.2 — excedente acumulado del lote
```

También propagar los totales al **consolidado** (`IndicadorEcuadorConsolidadoDto`): `TotalMermaUnidades`, `TotalMermaKilos`, `TotalAjusteAves`, `TotalProduccionKiloEnPie`, `TotalKilosDespachadosCliente`, `TotalAvesSobrante`.

### B.1.5 Cambios en el cálculo (servicio C# + función SQL de la Parte A)

- **Servicio:** [IndicadorEcuadorService.cs](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs) — en las dos primitivas (`CalcularIndicadorLoteAveEngordeAsync` y la de reproductora) leer `merma_unidades`/`merma_kilos` del lote y calcular los 6 derivados (merma %, ajuste, % ajuste, producción kilo en pie, total a cliente, días de engorde).
- **Función SQL (Parte A):** cuando se implemente `fn_indicadores_pollo_engorde` / `_bulk` (§9 CA1-CA4), **debe incluir** estas columnas nuevas en su `RETURNS TABLE` y leer `merma_unidades`, `merma_kilos`, `liquidado_at`, `aves_sobrante` desde `lote_ave_engorde`. Es decir: la Parte A y la Parte B se implementan **coordinadas** — la firma de la función ya nace con los campos de merma/ajuste. Añadir a la tabla maestra §2 las filas 30-39 (campos B).
- **Compatibilidad:** ningún cálculo existente cambia su valor; sólo se **agregan** columnas (salvo los refinamientos opcionales de §B.7-D5).

### B.1.6 Cambios en el endpoint de cierre (capturar merma)

- DTO [CerrarLoteAveEngordeRequest](backend/src/ZooSanMarino.Application/DTOs/LiquidacionLoteEngordeDto.cs:20): agregar `int? MermaUnidades`, `decimal? MermaKilos`.
- [LoteAveEngordeService.CerrarLoteAsync](backend/src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs:411): persistir merma + `merma_registrada_at/por` al cerrar.
- **Nuevo** endpoint `PUT /api/LoteAveEngorde/{id}/merma` (+ `ActualizarMermaAsync`) para que Costos digite/edite la merma **con el lote abierto o cerrado**, sin tener que reabrir. Controller: [LoteAveEngordeController.cs](backend/src/ZooSanMarino.API/Controllers/LoteAveEngordeController.cs) (junto a `cerrar`/`abrir`, líneas 142/159).
- **Permiso:** idealmente restringido al rol Costos (§B.7-D2). Como mínimo, registrar quién lo hizo (`merma_registrada_por_user_id`).

### B.1.7 Cambios en frontend (Requerimiento 1)

- **Modal liquidar** ([…/modal-liquidacion-lote-engorde](frontend/src/app/features/aves-engorde/pages/modal-liquidacion-lote-engorde/)): inputs "Merma (unidades)" y "Merma (kilos)", botón "Guardar merma" (llama `PUT …/merma`) y enviarla también al cerrar. Mostrar derivados (ajuste, % ajuste, total kg a cliente) en vivo.
- **Servicio** [lote-engorde.service.ts](frontend/src/app/features/lote-engorde/services/lote-engorde.service.ts): método `actualizarMerma(loteId, {mermaUnidades, mermaKilos})` y ampliar `cerrarLote(...)`.
- **Reporte de liquidación** ([…/indicador-ecuador/components/liquidacion-reporte](frontend/src/app/features/indicador-ecuador/components/liquidacion-reporte/) + [pages/indicador-ecuador-list](frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/)): agregar las 11 filas nuevas (fechas, días de engorde, bloque Mermas, producción kilo en pie, total a cliente) — tanto en la vista como en la **exportación a Excel** del reporte.
- **Interfaz TS** del indicador en [indicador-ecuador.service.ts](frontend/src/app/features/indicador-ecuador/services/indicador-ecuador.service.ts): añadir los campos nuevos.

---

## B.2 Requerimiento 2 — Sobrante de aves (la venta puede superar lo encasetado)

### B.2.1 Problema y comportamiento actual

Hoy el backend **bloquea** vender más aves de las disponibles. Disponibles se calcula en [GetAvesDisponiblesLotesAsync](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:938) como:

```
disp = encasetado/inicial − mort_caja − asignadas_a_reproductora − (mortalidad+selección+error_sexaje del seguimiento) − reservas_pendiente
```

y la venta se rechaza en **dos** puntos:
1. [ValidarDisponibilidadParaCrearAsync](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:119) — movimiento simple (`CreateAsync`, línea 105-108).
2. El loop inline de [CreateVentaGranjaDespachoAsync](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:1614-1626) — despacho multi-lote desde granja.

Ambos lanzan `"No hay aves suficientes disponibles en el lote…"`. **Ese es el bloqueo a relajar.** (Nota: la **liquidación** ya NO bloquea por aves — ver `puedeLiquidarPorAves => true` en el modal, línea 247 — el problema es exclusivamente la **venta/despacho**.)

### B.2.2 Modelo de datos — registrar el sobrante

| Dónde | Columna nueva | Tipo | Propósito |
|-------|---------------|------|-----------|
| `lote_ave_engorde` | `aves_sobrante` | `integer NOT NULL DEFAULT 0` | **Acumulado** de aves vendidas por encima del saldo físico ("aves agregadas de más"). El histórico original queda intacto en `aves_encasetadas`. |
| `movimiento_pollo_engorde` | `aves_sobrante` | `integer NOT NULL DEFAULT 0` | Cuántas aves **de ese** despacho fueron sobrante (excedente sobre el disponible al momento de la venta). Permite "mostrar algo en ventas". |

Props C#: `LoteAveEngorde.AvesSobrante` (`int`), `MovimientoPolloEngorde.AvesSobrante` (`int`). Configs: [LoteAveEngordeConfiguration.cs](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteAveEngordeConfiguration.cs), [MovimientoPolloEngordeConfiguration.cs](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs).

> **Consistencia:** `lote.aves_sobrante` ≈ `SUM(movimiento.aves_sobrante)` del lote, y coincide en magnitud con el `AjusteAves` negativo del reporte (§B.1.1 #8). El reporte deriva el ajuste; el lote/movimiento lo registran como dato.

### B.2.3 Relajar la validación de venta (con opt-in explícito)

**Decisión de diseño (recomendada):** **no** quitar la validación del todo (evita sobreventas accidentales), sino **permitir excederla bajo bandera explícita**.

- Agregar `bool PermitirSobrante = false` a los DTOs de venta: `CreateMovimientoPolloEngordeDto` y `CreateVentaGranjaDespachoDto` / sus líneas ([MovimientoPolloEngordeDto.cs](backend/src/ZooSanMarino.Application/DTOs/MovimientoPolloEngordeDto.cs)).
- En los dos puntos de validación:
  - Si `PermitirSobrante == false` → comportamiento actual (rechaza).
  - Si `PermitirSobrante == true` → **no rechaza**; calcula el excedente por sexo `excedente = max(0, solicitado − disponible)`, lo guarda en `movimiento.aves_sobrante` (suma H+M+X) y **incrementa** `lote_ave_engorde.aves_sobrante` en esa cantidad.
- El excedente se imputa al lote del movimiento. (En el ejemplo del correo, el último despacho pidió 230 y sólo cabían 222 → `aves_sobrante = 8`.)

### B.2.4 Mostrar el sobrante

- **En ventas/movimientos:** la lista de movimientos y el detalle de venta muestran "incluye N aves sobrantes" cuando `aves_sobrante > 0`. Componentes [movimientos-pollo-engorde-list](frontend/src/app/features/movimientos-pollo-engorde/pages/movimientos-pollo-engorde-list/) y [modal-movimiento-pollo-engorde](frontend/src/app/features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/).
- **En lote pollo engorde:** el detalle del lote muestra `aves_sobrante` ("Aves agregadas de más"). Feature [lote-engorde](frontend/src/app/features/lote-engorde/).
- **En el modal de liquidar:** ya calcula desbalances (`diferenciaHembras/Machos`, `sobreventaTotal`); reutilizar para mostrar el sobrante registrado.

### B.2.5 Frontend (Requerimiento 2)

- Form de venta/despacho ([modal-movimiento-pollo-engorde](frontend/src/app/features/movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/)): si el backend rechaza por disponibilidad, ofrecer checkbox/confirmación **"Registrar sobrante de aves"** que reenvía con `permitirSobrante = true`. Mostrar cuántas serían sobrante antes de confirmar.
- Servicio [movimiento-pollo-engorde.service.ts](frontend/src/app/features/movimientos-pollo-engorde/services/movimiento-pollo-engorde.service.ts): propagar `permitirSobrante` y el nuevo campo `avesSobrante` en las interfaces.

---

## B.3 Migración EF Core (columnas nuevas, idempotente)

Una sola migración `AddMermaYSobranteAvesEngorde` (patrón idempotente de CLAUDE.md). Toca dos tablas:

```sql
ALTER TABLE lote_ave_engorde         ADD COLUMN IF NOT EXISTS merma_unidades integer;
ALTER TABLE lote_ave_engorde         ADD COLUMN IF NOT EXISTS merma_kilos numeric(18,3);
ALTER TABLE lote_ave_engorde         ADD COLUMN IF NOT EXISTS merma_registrada_at timestamptz;
ALTER TABLE lote_ave_engorde         ADD COLUMN IF NOT EXISTS merma_registrada_por_user_id varchar(450);
ALTER TABLE lote_ave_engorde         ADD COLUMN IF NOT EXISTS aves_sobrante integer NOT NULL DEFAULT 0;
ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS aves_sobrante integer NOT NULL DEFAULT 0;
```

Comando: `dotnet ef migrations add AddMermaYSobranteAvesEngorde --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext`, luego sustituir el `Up()` por `migrationBuilder.Sql("… ADD COLUMN IF NOT EXISTS …")`. Probar local con `dotnet ef database update` antes de mergear. El deploy ECS aplica solo (`Database__RunMigrations=true`).

---

## B.4 Coordinación Parte A ↔ Parte B

| Tema | Implicación |
|------|-------------|
| Función SQL `fn_indicadores_pollo_engorde` (Parte A) | Nace ya con las columnas de merma/ajuste/sobrante en `RETURNS TABLE` y leyendo `lote_ave_engorde.merma_*` y `aves_sobrante`. |
| Tabla maestra §2 | Extender con filas 30-39 (los 11 campos B). |
| Checklist §9 (CA1-CA9) | CA1/CA3 (SQL + migración) incluyen las columnas B; CA7 (tests E2E) agrega aserciones sobre ajuste/merma del lote 02 Km 61. |
| Orden sugerido | **Primero** Parte B (columnas + captura de merma + sobrante en C#), **luego** Parte A (mover el cálculo a SQL ya con los campos B). Así la BD y los DTO quedan estables antes del refactor de performance. |

---

## B.5 Validación y pruebas (CLAUDE.md §Testing)

- **Backend:** request de venta con `permitirSobrante=true` que excede disponible → 200, `movimiento.aves_sobrante = excedente`, `lote.aves_sobrante` incrementado. Sin la bandera → sigue dando 400 con el mensaje actual.
- **Backend:** `PUT …/merma` setea merma con lote abierto; el reporte de indicadores devuelve `AjusteAves`, `MermaPorcentaje`, `PorcentajeAjuste`, `TotalKilosDespachadosCliente` correctos. Validar contra el ejemplo lote 02 Km 61 (ajuste = −8, total a cliente = producción − 10,66).
- **DB local:** levantar con `make up`, aplicar migración, probar; **`make down` al terminar** (no dejar procesos).
- **Frontend:** `yarn tsc --noEmit` y `yarn build` sin errores; el reporte exporta las 11 columnas nuevas a Excel.

---

## B.6 Checklist de implementación (resumen — el detalle granular va al tracker)

**Modelo de datos / migración**
- [ ] **CA-B1** Props nuevas en `LoteAveEngorde` y `MovimientoPolloEngorde` + EF configs.
- [ ] **CA-B2** Migración idempotente `AddMermaYSobranteAvesEngorde` (6 columnas, 2 tablas); probada en local.

**Requerimiento 1 — Mermas / 11 campos**
- [ ] **CA-B3** Ampliar `IndicadorEcuadorDto` (+10 campos) y consolidado (+totales).
- [ ] **CA-B4** Cálculo de los 6 derivados en `IndicadorEcuadorService` (y previsión para la fn SQL de Parte A).
- [ ] **CA-B5** `CerrarLoteAveEngordeRequest` + `CerrarLoteAsync` capturan merma; nuevo `PUT …/merma` + `ActualizarMermaAsync`.
- [ ] **CA-B6** Frontend: inputs de merma en modal liquidar + 11 filas nuevas en reporte y Excel.

**Requerimiento 2 — Sobrante de aves**
- [ ] **CA-B7** `PermitirSobrante` en DTOs de venta; relajar los 2 puntos de validación; calcular y persistir `aves_sobrante` (movimiento + acumulado en lote).
- [ ] **CA-B8** Frontend: confirmación "Registrar sobrante" en el form de venta/despacho; mostrar sobrante en lista de ventas y en detalle del lote.

**Cierre**
- [ ] **CA-B9** Tests backend (venta con/ sin sobrante, merma → ajuste) y validación contra lote 02 Km 61.
- [ ] **CA-B10** `dotnet build` + `yarn tsc --noEmit`/`yarn build` limpios; migración aplica en local; `make down`.

---

## B.7 Decisiones abiertas — confirmar antes de implementar

| # | Decisión | Opciones | Recomendación |
|---|----------|----------|---------------|
| **D1** | Dónde guardar la merma | (a) columnas en `lote_ave_engorde`; (b) tabla `liquidacion_lote_engorde` separada | **(a)** — alineado con `liquidado_at`/`liquidado_por` que ya viven en el lote; menos superficie. |
| **D2** | ¿Restringir la digitación de merma al rol Costos? | (a) sólo Costos; (b) cualquiera con permiso de liquidar, registrando autor | Confirmar si existe rol/permiso "Costos" en el modelo de seguridad. Mínimo: auditar autor. |
| **D3** | Fórmula de "Días de engorde" (ejemplo=51) | encaset→liquidación / encaset→último despacho / valor fijo de guía | Proponer **encaset → fecha de último despacho (cierre)**; **confirmar con Costos** porque las fechas del ejemplo no dan 51 de forma obvia. |
| **D4** | Denominador de "Merma %" | aves vendidas vs aves encasetadas | **Aves vendidas** (5/42.532≈0,01); ambas redondean a 0,01 en el ejemplo → confirmar. |
| **D5** | Refinamientos técnicos del comparativo | (a) Edad **ponderada** vs promedio simple; (b) discrepancia Kg carne 154.609 vs Producción 135.091 | Tratar como **sub-requerimiento aparte**; D5(b) requiere aclaración de Costos sobre la fuente de kilos antes de tocar `PesoPromedio`/`ProduccionKiloEnPie`. |
| **D6** | Sobrante: ¿bandera explícita o permitir siempre? | (a) `PermitirSobrante` opt-in; (b) permitir siempre y registrar | **(a)** — conserva la protección contra sobreventa accidental; el sobrante es una acción consciente de Costos. |

---

## B.8 Referencias del repo (Parte B)

- Entidades: [LoteAveEngorde.cs](backend/src/ZooSanMarino.Domain/Entities/LoteAveEngorde.cs), [MovimientoPolloEngorde.cs](backend/src/ZooSanMarino.Domain/Entities/MovimientoPolloEngorde.cs)
- Configs EF: [LoteAveEngordeConfiguration.cs](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteAveEngordeConfiguration.cs), [MovimientoPolloEngordeConfiguration.cs](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs)
- Servicios: [MovimientoPolloEngordeService.cs](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs) (validación 119, despacho 1590, disponibilidad 938), [LoteAveEngordeService.cs](backend/src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs) (cerrar 411), [IndicadorEcuadorService.cs](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs)
- DTOs: [IndicadorEcuadorDto.cs](backend/src/ZooSanMarino.Application/DTOs/IndicadorEcuadorDto.cs), [LiquidacionLoteEngordeDto.cs](backend/src/ZooSanMarino.Application/DTOs/LiquidacionLoteEngordeDto.cs), [MovimientoPolloEngordeDto.cs](backend/src/ZooSanMarino.Application/DTOs/MovimientoPolloEngordeDto.cs)
- Controllers: [LoteAveEngordeController.cs](backend/src/ZooSanMarino.API/Controllers/LoteAveEngordeController.cs), [MovimientoPolloEngordeController.cs](backend/src/ZooSanMarino.API/Controllers/MovimientoPolloEngordeController.cs), [IndicadorEcuadorController.cs](backend/src/ZooSanMarino.API/Controllers/IndicadorEcuadorController.cs)
- Frontend: [modal-liquidacion-lote-engorde](frontend/src/app/features/aves-engorde/pages/modal-liquidacion-lote-engorde/), [indicador-ecuador](frontend/src/app/features/indicador-ecuador/), [movimientos-pollo-engorde](frontend/src/app/features/movimientos-pollo-engorde/), [lote-engorde](frontend/src/app/features/lote-engorde/)

---
---

# 📋 PARTE C — Requerimiento adicional Ecuador (2026-05-29): Proceso de venta por factura/despacho (prorrateo individual persistido + factura única con UID)

**Fuente:** Reunión/nota de voz del usuario + capturas del modal **"Nueva venta por granja (despacho)"** y de la lista **"Venta de Pollo Engorde"** (granja Kilometro 86).
**Alcance:** **Venta/Despacho Pollo Engorde** (`MovimientoPolloEngorde`) y su consumo en **Seguimiento Diario** y **Liquidación Técnica Ecuador**. Se conecta con la Parte A (cálculo de Kg) y la Parte B (mermas/ajuste). Mismo módulo Pollo Engorde.
**Estado:** 📐 Diseño / pendiente de aprobación.

---

## C.0 Resumen del requerimiento (3 puntos)

1. **El peso prorrateado individual debe persistir y consumirse de verdad** — no clonar el peso global de la factura en cada lote. Cada línea de venta debe llevar **su peso individual** (bruto/tara/neto real, proporcional a sus aves) **y** el **peso general de la factura** (bruto/tara/neto global). El front ya muestra el prorrateo en el preview ("Distribución de pesos por lote"); falta **garantizar** que eso es lo que se guarda y lo que leen los reportes.
2. **Un despacho de varios lotes/galpones = una sola factura** identificada por un **UID aleatorio**, que amarra todas las salidas. Hoy, sin número de despacho, aparecen como **registros sueltos**; deben verse como **un solo registro/factura** expandible con el detalle de galpones y aves.
3. **Seguimiento diario y Liquidación consumen el peso individual** de cada lote (las 100 aves que salieron de *ese* lote), **no** el peso general de la factura ni promedios globales.

> 📌 Cita del usuario: *"…anteriormente se clonaba ese peso general… pero no tiene que ser así. Cada lote tiene que ir con su peso general, su peso individual… crear un campo que se llame peso general factura, peso tara general factura… cuando haga un despacho de varios lotes en diferentes galpones, se amarran a una factura con un ID random (un UID)… en la liquidación del lote no es el peso general… sino ya es individual."*

---

## C.1 Estado actual auditado (el código es la fuente de verdad — CLAUDE.md)

| Hecho | Evidencia |
|-------|-----------|
| El prorrateo individual **ya existe** | [CreateVentaGranjaDespachoAsync](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:1639-1673): reparte bruto/tara/neto por línea proporcional a aves, con ajuste de residuo de redondeo al lote de mayor cantidad. |
| **Semántica confusa de columnas** | Por línea se guarda: `peso_bruto`/`peso_tara` = **GLOBAL clonado** (de `dto.PesoBruto/PesoTara`, líneas 1712-1716); `peso_neto`, `peso_bruto_real`, `peso_tara_real`, `promedio_peso_ave` = **INDIVIDUAL prorrateado**; `peso_bruto_global`/`peso_tara_global`/`peso_neto_global` = **GLOBAL**. |
| `peso_bruto_real`/`peso_tara_real` se mapean **por convención** (no explícitos) | [MovimientoPolloEngordeConfiguration.cs:58-64](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs) sólo declara `peso_bruto`, `peso_tara`, `peso_*_global`, `peso_neto`, `promedio_peso_ave`. |
| 🔴 **BUG de cálculo** | La liquidación suma **`peso_bruto − peso_tara`** (= el GLOBAL clonado): [IndicadorEcuadorService.cs:762](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs) y [:1147-1148](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs). En un despacho de **N** líneas, esto cuenta el peso global **N veces** → **sobrecuenta los Kg de carne**. Debe sumar `peso_neto` (individual). |
| Agrupación por `numero_despacho` (opcional, lo digita el usuario) | [OrganizarPeso GroupBy](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:1808-1810): sin `numero_despacho` cada movimiento es su propio "despacho". Por eso se ven registros sueltos (en la captura el campo "Número de despacho" estaba vacío). |
| **No existe** un identificador único de factura | La config sólo tiene `numero_despacho varchar(50)`. No hay `factura_id`/UID. |
| El seguimiento diario **no muestra el peso** de las ventas | [SeguimientoDiarioTablaFilaDto](backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs:50-53) sólo expone `DespachoHembras/Machos/Mixtas` y `Documento`; **ningún campo de peso**. |
| Existe la herramienta **"Organizar Peso"** | Reprocesa y reparte el global por grupo `numero_despacho` ([OrganizarPesoAsync](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs:1788-1857)). Útil para backfill tras los cambios. |

**Conclusión:** el prorrateo individual **se guarda** (`peso_neto`, `peso_*_real`), pero (a) los **reportes leen la columna equivocada** (`peso_bruto`/`peso_tara` = global) → bug; (b) la **agrupación** depende de un campo opcional → facturas partidas; (c) el **seguimiento no expone** el peso individual. Eso es justo lo que pide el usuario.

> 🔗 **Esto resuelve la incógnita §B.7-D5(b):** la discrepancia "Kg carne 154.609 (app) vs 135.091 (Costos)" es coherente con el **sobreconteo** por sumar el global clonado en despachos multi-lote (el app queda **por encima**). El fix de C.2 debería cerrar esa diferencia.

---

## C.2 R3.1 — Reportes consumen el peso INDIVIDUAL (fix del bug)

- **Regla:** Kg de carne / peso despachado = **`SUM(peso_neto)`** por lote (o `SUM(peso_bruto_real − peso_tara_real)` como equivalente), **nunca** `SUM(peso_bruto − peso_tara)`.
- **Fallback** (movimientos viejos sin prorrateo): `COALESCE(peso_neto, peso_bruto − peso_tara)` para no romper datos de 1 sola línea (donde global = individual).
- **Cambios:**
  - [IndicadorEcuadorService.KgCarneYEdadPolloEngordeAsync:762](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs) y [CalcularKgCarneYEdadAsync:1147-1148](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs) → usar `peso_neto` con fallback.
  - **Parte A, tabla maestra §2 campo #14 (`KgCarnePollos`)** y la futura `fn_indicadores_pollo_engorde`: redefinir la fórmula a `SUM(COALESCE(peso_neto, peso_bruto−peso_tara))`. **Coordinar**: la función SQL nace ya con esta fórmula corregida.
- **Validar** contra el ejemplo de la captura (despacho 5.089 aves, neto 90.000 kg): `SUM(peso_neto)` = 88.602,869 + 1.397,131 = **90.000** ✅ (con la fórmula vieja daría 180.000).

---

## C.3 R3.2 — "Peso general de factura" por línea (validar/poblar)

El usuario pide explícitamente `peso general factura` y `peso tara general factura` por lote. **Ya existen** como `peso_bruto_global` / `peso_tara_global` / `peso_neto_global` en la entidad. Tareas:
- **Validar** que **todos** los flujos de venta los pueblan (no sólo `CreateVentaGranjaDespachoAsync`): revisar `CreateAsync` simple, `UpdateAsync`, edición de despacho.
- **Mapear explícitamente** `peso_bruto_real`/`peso_tara_real` en la config (hoy por convención) para dejar la semántica clara y evitar futuros errores como el de C.2.
- **Documentar la semántica** en la entidad: `peso_bruto`/`peso_tara` = entrada cruda del formulario (global del camión); `peso_*_real`/`peso_neto` = individual prorrateado; `peso_*_global` = factura general. (Opcional: renombrar `peso_bruto`/`peso_tara` para que no se confundan con "individual"; evaluar impacto en "Organizar Peso" que los lee como global, línea 1822.)

---

## C.4 R3.3 — Factura única con UID

- **Nueva columna** `factura_id uuid` en `movimiento_pollo_engorde` (NULL para histórico). Prop C#: `MovimientoPolloEngorde.FacturaId (Guid?)`.
- En `CreateVentaGranjaDespachoAsync`: generar **un** `Guid.NewGuid()` por despacho y asignarlo a **todas** las líneas creadas en esa transacción (independiente de que el usuario ponga o no `numero_despacho`).
- `numero_despacho` se mantiene como **referencia legible opcional** del usuario; la **llave de agrupación pasa a ser `factura_id`** (robusta, no depende de input).
- Índice `ix_mpe_factura_id ON movimiento_pollo_engorde(factura_id) WHERE factura_id IS NOT NULL`.
- **Backfill** (script SQL): para movimientos existentes con `numero_despacho` no nulo, asignar un `factura_id` común por grupo `(company_id, numero_despacho, fecha_movimiento)`; los sueltos reciben su propio UID. Reusa la lógica de agrupación de "Organizar Peso".

---

## C.5 R3.4 — Agrupación visual: una fila por factura

- **Backend** (listado de ventas): agrupar por `factura_id` (fallback `numero_despacho`, luego `id`). Un ítem-factura con: total aves, total kg neto (= `SUM(peso_neto)`), peso global de la factura, y **detalle** de líneas (galpón/lote/aves/peso individual). DTO `VentaFacturaAgrupadaDto { FacturaId, Fecha, Cliente, TotalAves, TotalNetoKg, PesoBrutoGlobal, PesoTaraGlobal, Lineas[] }`.
- **Frontend** [movimientos-pollo-engorde-list](frontend/src/app/features/movimientos-pollo-engorde/pages/movimientos-pollo-engorde-list/): una fila por factura, **expandible** (ya hay precedente: la fila "155 · 2 lotes · mismo viaje"). Mostrar en el detalle todos los galpones y aves. Acciones (completar/eliminar) operan sobre **toda la factura**.
- "Total despacho" y el preview del modal ya están bien; sólo se formaliza el agrupado en la **lista**.

---

## C.6 R3.5 — Seguimiento diario y Liquidación con peso individual

- **Seguimiento diario:** añadir a [SeguimientoDiarioTablaFilaDto](backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs) los campos de **peso individual** de la venta de ese lote/fecha: `DespachoPesoNeto`, `DespachoPesoTaraReal` (o bruto/tara real) y `DespachoPromedioPesoAve`. La función SQL [fn_seguimiento_diario_engorde.sql](backend/sql/fn_seguimiento_diario_engorde.sql) debe **traer esas columnas desde `movimiento_pollo_engorde`** (hoy sólo trae aves de despacho). El front (tabla del seguimiento) muestra el **peso real** de las aves que salieron de *ese* lote, no el global.
- **Liquidación:** ya cubierto por C.2 (usa `peso_neto`). El "Producción kilo en pie" y "Peso promedio" de la Parte B (§B.1.1) pasan a basarse en el individual correcto.

---

## C.7 Modelo de datos / migración

Migración EF idempotente `AddFacturaIdAndIndividualWeightMappingEngorde`:

```sql
ALTER TABLE movimiento_pollo_engorde ADD COLUMN IF NOT EXISTS factura_id uuid;
CREATE INDEX IF NOT EXISTS ix_mpe_factura_id ON movimiento_pollo_engorde(factura_id) WHERE factura_id IS NOT NULL;
-- peso_bruto_real / peso_tara_real ya existen como columnas (mapeo por convención); sólo se formaliza el mapeo en EF, sin DDL.
```

+ **Script de backfill** `backend/sql/backfill_factura_id_engorde.sql` (idempotente, en transacción, con tabla snapshot) para poblar `factura_id` de movimientos históricos por grupo `(company_id, numero_despacho, fecha)`.

---

## C.8 Coordinación con Parte A y Parte B

| Tema | Implicación |
|------|-------------|
| Parte A — fórmula de Kg (§2 #14) | Se **corrige** a `SUM(COALESCE(peso_neto, peso_bruto−peso_tara))`. La fn SQL nace corregida. |
| Parte A — función SQL | Debe leer `peso_neto` (individual). Sin esto, el refactor de performance **propagaría el bug**. |
| Parte B — §B.7-D5(b) | La discrepancia de Kg (154.609 vs 135.091) se atribuye a este bug → **D5(b) se cierra con C.2** (validar). |
| Parte B — Producción kilo en pie / Total a cliente | Se calculan sobre el Kg **corregido** (individual). |
| Orden global sugerido | **C.2 (fix individual) → B (mermas/ajuste/sobrante) → A (refactor SQL)**. El fix de peso es prerrequisito para que B y A reporten números correctos. |

---

## C.9 Checklist de implementación (resumen — detalle al tracker)

- [ ] **CA-C1** Fix: liquidación/indicador usan `SUM(COALESCE(peso_neto, peso_bruto−peso_tara))` (service C# + previsión fn SQL Parte A). Validar despacho 5.089 → 90.000 kg.
- [ ] **CA-C2** Mapear explícito `peso_bruto_real`/`peso_tara_real`; documentar semántica de columnas; validar que todos los flujos pueblan `peso_*_global`.
- [ ] **CA-C3** Columna `factura_id uuid` + prop + config + índice; migración idempotente.
- [ ] **CA-C4** `CreateVentaGranjaDespachoAsync` genera un `factura_id` único por despacho para todas las líneas.
- [ ] **CA-C5** Backfill `factura_id` de históricos (script SQL idempotente con snapshot).
- [ ] **CA-C6** Listado de ventas agrupado por `factura_id`: una fila/factura expandible (backend DTO + frontend list).
- [ ] **CA-C7** Seguimiento diario expone peso individual de venta (DTO + `fn_seguimiento_diario_engorde.sql` + tabla front).
- [ ] **CA-C8** Tests: despacho multi-lote → `SUM(peso_neto)` correcto; agrupación por factura; seguimiento muestra peso individual. `dotnet build` + `yarn build` limpios; `make down`.

---

## C.10 Decisiones abiertas (Parte C)

| # | Decisión | Recomendación |
|---|----------|---------------|
| **DC1** | ¿`factura_id` reemplaza o complementa `numero_despacho`? | **Complementa**: `factura_id` = llave técnica (UID); `numero_despacho` = referencia legible opcional del usuario. |
| **DC2** | ¿Renombrar `peso_bruto`/`peso_tara` (hoy guardan el global) para evitar confusión? | Documentar primero; renombrar sólo si no rompe "Organizar Peso" ni reportes. Bajo riesgo si se hace junto al fix C.2. |
| **DC3** | ¿Backfill de `factura_id` e `peso_neto` para histórico ya cargado? | Sí, idempotente con snapshot; correr "Organizar Peso" (ReprocesarTodo) para rellenar `peso_neto` de ventas viejas sin prorrateo. |
| **DC4** | ¿La acción "Completar/Eliminar" opera por factura completa? | Sí, por `factura_id` (ya existe "Completar/Eliminar despacho" para grupos). |

---

## C.11 Referencias (Parte C)

- Servicio venta/despacho: [MovimientoPolloEngordeService.cs](backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs) (prorrateo 1639-1673, OrganizarPeso 1788-1857, grouping 1808)
- Entidad/config: [MovimientoPolloEngorde.cs](backend/src/ZooSanMarino.Domain/Entities/MovimientoPolloEngorde.cs:56-68), [MovimientoPolloEngordeConfiguration.cs](backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/MovimientoPolloEngordeConfiguration.cs:58-64)
- Cálculo Kg (bug): [IndicadorEcuadorService.cs:762](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs), [:1147](backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs)
- Seguimiento: [SeguimientoDiarioTablaFilaDto.cs](backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs), [fn_seguimiento_diario_engorde.sql](backend/sql/fn_seguimiento_diario_engorde.sql)
- Frontend: [movimientos-pollo-engorde](frontend/src/app/features/movimientos-pollo-engorde/) (modal venta + list), [aves-engorde tabs-principal-engorde](frontend/src/app/features/aves-engorde/pages/tabs-principal-engorde/) (tabla seguimiento)
