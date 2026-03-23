# Indicador Ecuador – Alineación frontend ↔ backend

Referencia para validar que la UI y la API usan los mismos significados de filtros y parámetros.

## 1. Endpoints y contratos

| Acción UI (vista General) | Método | Ruta | Parámetros clave |
|---------------------------|--------|------|------------------|
| Calcular indicadores | POST | `/api/IndicadorEcuador/calcular` | Cuerpo `IndicadorEcuadorRequest` (camelCase JSON). `fechaDesde` / `fechaHasta` filtran por **fecha de encaset** en `ObtenerLotesAsync`. `soloLotesCerrados`: si es `true`, solo lotes con aves = 0 dentro de ese universo. |
| Ver consolidado | POST | `/api/IndicadorEcuador/consolidado` | Igual cuerpo que calcular. |
| Ver lotes cerrados / en rango | GET | `/api/IndicadorEcuador/lotes-cerrados` | Query: `fechaDesde`, `fechaHasta`, `granjaId` (opcional), `soloCerrados` (opcional, default `true`). **Si `soloCerrados=true`:** solo lotes cerrados cuya **fecha de cierre** (`FechaCierreLote`) está en el rango (no filtra por encaset en la consulta inicial). **Si `soloCerrados=false`:** lotes con **encaset** en el rango, incluye abiertos. |
| Liquidación por período | POST | `/api/IndicadorEcuador/liquidacion-periodo` | Cuerpo `LiquidacionPeriodoRequest`: `fechaInicio`, `fechaFin`, `tipoPeriodo`, `granjaId`. Cierre en período (lógica en servicio). |

| Acción UI (Pollo Engorde) | Método | Ruta | Parámetros clave |
|---------------------------|--------|------|------------------|
| **Liquidación técnica (principal en Ecuador)** | POST | `/api/IndicadorEcuador/liquidacion-pollo-engorde-reporte` | Cuerpo `LiquidacionPolloEngordeReporteRequest`: `modo` = `UnLote` \| `Rango`. **Solo lote padre liquidado (aves = 0), sin reproductoras.** `UnLote`: `loteAveEngordeId` obligatorio. `Rango`: `fechaDesde`, `fechaHasta` (franja por **fecha de cierre**), `alcance` = `TodasLasGranjas` \| `Granja` \| `Nucleo`; para `Granja`/`Nucleo` → `granjaId` obligatorio; para `Nucleo` → `nucleoId` obligatorio. |
| Indicadores padre + reproductores (legado / otros usos) | POST | `/api/IndicadorEcuador/indicadores-pollo-engorde-por-lote-padre` | `loteAveEngordeId`, `soloLotesCerrados`, etc. |

## 2. Nombres JSON (ASP.NET camelCase)

- `IndicadorEcuadorRequest`: `soloLotesCerrados`, `fechaDesde`, `fechaHasta`, `granjaId`, …
- GET `lotes-cerrados`: query **`soloCerrados`** (no `soloLotesCerrados`) — el `IndicadorEcuadorService` del front mapea el checkbox de la vista general a este query param.
- `IndicadorPolloEngordePorLotePadreRequest`: `loteAveEngordeId`, `soloLotesCerrados`, …

## 3. Ecuador (país)

- La vista queda en **Pollo Engorde**; los botones de **Calcular / Consolidado / Ver lotes en rango** solo aparecen en vista **General** (otro país o si se habilita el selector).
- Catálogo AE: `GET .../LoteReproductoraAveEngorde/filter-data` → `farms`, `nucleos`, `galpones`, `lotesAveEngorde` (cascada granja → núcleo → galpón → lote).

## 4. Checklist de coherencia

- [x] GET `lotes-cerrados` + `soloCerrados=true` → cierre en rango.
- [x] GET `lotes-cerrados` + `soloCerrados=false` → encaset en rango, abiertos incluidos.
- [x] POST calcular/consolidado → fechas = encaset (tabla `lotes`).
- [x] POST `liquidacion-pollo-engorde-reporte` modo `Rango` → fechas = **cierre** en rango.
- [ ] POST `indicadores-pollo-engorde-por-lote-padre` → fechas en contrato aún no recortan cálculo (legado).
