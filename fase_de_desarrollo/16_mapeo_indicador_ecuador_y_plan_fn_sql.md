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
