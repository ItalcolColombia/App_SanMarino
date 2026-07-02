# Plan — Migrar reportes de solo lectura a funciones SQL (PL/pgSQL)

> Análisis + planificación (agente dev en paralelo). NO modifica código ni crea migraciones.
> Objetivo: mover cómputo pesado (GroupBy/ToList/Sum/foreach en memoria + bucles O(n²) día-a-día)
> a funciones SQL, patrón engorde (`fn_indicadores_pollo_engorde`, `fn_informe_semanal_pollo_engorde`)
> con migración a mano idempotente (`CREATE OR REPLACE FUNCTION`, `Sql(..., suppressTransaction:true)`).

Servicios analizados:
1. `ReporteContableService.cs` (~1458)
2. `ReporteTecnicoService.cs` (~3110)
3. `ReporteTecnicoProduccionService.cs` (~1953)

## 0. Infraestructura (código de HOY — EL CÓDIGO MANDA)
- `_ctx.SeguimientoDiario` → **`public.seguimiento_diario_levante_reproductoras`** (NO `seguimiento_diario`). `tipo_seguimiento` discrimina `'levante'`/`'produccion'`. `lote_id varchar(64)`. FKs `lote_postura_levante_id`, `lote_postura_produccion_id`.
- `_ctx.SeguimientoProduccion` → `seguimiento_diario_produccion_reproductoras`.
- `_ctx.ProduccionAvicolaRaw` → **`guia_genetica_sanmarino_colombia`** (guía: Edad, PesoH/M, ConsAcH/M, GrAveDiaH/M, MortSemH/M, RetiroAcH/M, Uniformidad, ProdPorcentaje, HTotalAa).
- Otras: `lotes`, `lote_postura_levante/produccion/base`, `farms`, `nucleos`, `galpones`, `movimiento_aves`, `traslado_huevos`, `farm_inventory_movements`, `catalogo_items` (jsonb `metadata->>'type_item'`).
- **Power BI: riesgo NULO.** Las vistas `vw_*_engorde`/Ecuador son del dominio pollo de engorde; ninguno de estos 3 reportes de reproductoras las toca. Crear `fn_*` nuevas no las afecta.
- Migración de referencia: `20260623070401_AddFnInformeSemanalPolloEngorde.cs` (`Up = Sql(FN_SQL, suppressTransaction:true)`; `Down = DROP FUNCTION IF EXISTS`). SQL canónico en `backend/sql/`.

## SERVICIO 1 — ReporteContableService
Endpoints (`ReporteContableController.cs`): `GET generar` (`GenerarReporteContableRequestDto` → `ReporteContableCompletoDto`), `GET semanas-contables/{id}`, `GET exportar/excel`, `GET filtros-disponibles` (`FiltrosContablesDto`), `GET movimientos-huevos` (`ObtenerReporteMovimientosHuevosRequestDto` → `ReporteMovimientosHuevosDto`). DTOs en `ReporteContableDto.cs`, `ReporteMovimientosHuevosDto.cs`, `FiltrosContablesDto.cs`.
Agregaciones pesadas: `ObtenerDatosDiariosCompletosAsync` (cartesiano fecha×lote con `FirstOrDefault` lineal); `ObtenerVentasYTrasladosAsync` (N+1 por lote); `ObtenerDatosBultosAsync` (trae todos `CatalogItems` y filtra jsonb en memoria + inventario `PageSize=10000`); `CalcularSaldosAcumulativos` (apto window); `ConsolidarSemanaContable` + `ObtenerSaldoAnteriorSemana`; `ObtenerReporteMovimientosHuevosAsync` (GroupBy+Sum + N+1 traslados por lote).
Plan SQL (2 fn):
- (a) `fn_reporte_contable_diario(p_company_id, p_lote_padre_id, p_fase, p_fecha_inicio, p_fecha_fin)` → fila por lote×fecha, saldos acumulados aves+bultos vía `SUM() OVER (PARTITION BY lote_id ORDER BY fecha)` + `GREATEST(0,·)`. CTEs lotes/entradas_iniciales/diario_aves(union)/ventas_traslados(`movimiento_aves estado='Completado'`)/bultos(`farm_inventory_movements` + `catalogo_items metadata->>'type_item'='alimento'`, factor 40, unit).
- (b) `fn_reporte_contable_movimientos_huevos(p_company_id, p_lote_padre_id, p_fecha_inicio, p_fecha_fin)` → agg huevos por fecha/lote + agg `traslado_huevos` (entrada/venta/salida/planta/descarte).
Delegación: `SqlQueryRaw<Row>`. Semana contable (calendario, no de vida) + INICIO/LEVANTE en C# o `Application/Calculos/ReporteContableCalculos.cs` con tests.
Riesgos: `GREATEST(0,·)`; saldo bultos por fecha; jsonb `type_item`; `unit IN ('bulto','bultos')` vs kg/40; clamp `maxSemanas=200`.

## SERVICIO 2 — ReporteTecnicoService (LEVANTE)
Endpoints (`ReporteTecnicoController.cs`): `levante/filter-data`, `diario/sublote/{id}`, `diario/consolidado`, `semanal/sublote/{id}`, `semanal/consolidado`, `POST generar`, `sublotes`, `POST exportar/excel/diario|semanal`, `levante/completo/{id}`, `levante/tabs/{id}`, `POST levante/obtener`, exportar excel. DTOs: `ReporteTecnicoDto.cs`, `ReporteTecnicoLevanteCompletoDto.cs`, `GuiaGeneticaDto.cs`.
Agregaciones pesadas (las peores): **O(n²)** día-a-día en `ObtenerDatosDiariosLevanteAsync`, `GenerarReporteDiarioMachosAsync`, `GenerarReporteDiarioHembrasAsync` (`registrosHastaFecha` recomputa todo desde cero). N+1 en loop diario: `ObtenerIngresosAlimentoAsync`/`ObtenerTrasladosAlimentoAsync`. Consolidados + cruce guía `GenerarReporteLevanteCompletoAsync` (25 sem × ~80 campos + ParseGuiaRaw).
Plan SQL:
- (a) `fn_reporte_tecnico_levante_diario(p_lote_postura_levante_id, p_company_id, p_fecha_inicio, p_fecha_fin)` → grano diario H/M, saldos/acumulados vía window (reemplaza O(n²)) + JOIN pre-agregado `farm_inventory_movements` por fecha/granja (mata N+1). `sel>0`=selección, `sel<0`=traslado (`abs(min(0,sel))`); %mort diario sobre saldo previo (`LAG`); %acum sobre aves_iniciales; edad=(fecha-encaset)+1, sem=ceil(/7), filtro ≤25.
- (b) `fn_reporte_tecnico_levante_semanal(..., p_lote_postura_base_id DEFAULT NULL)` → `CROSS JOIN LATERAL` sobre (a), agrupa por semana + cruce genético `guia_genetica_sanmarino_colombia` (edad 1..25), ~80 campos. Consolidado: denominadores = suma aves_h/m_inicial. Parseo guía = `NULLIF(replace(trim(x),',','.'),'')::numeric`; incrementos = `LAG() OVER (ORDER BY semana)`.
Delegación: `SqlQueryRaw<Row>` mapeo 1:1; Machos/Hembras proyectan subconjunto. Matemática pura → `Application/Calculos/ReporteTecnicoLevanteCalculos.cs` + tests.
**Riesgos (ALTO — bit-exactitud):** `GenerarReporteLevanteCompletoAsync` NO redondea vs `GenerarSemanalesConsolidados` usa `Math.Round` (4/2/3); banker's rounding (.NET) vs half-away (Postgres); `double` vs `numeric`. → **En la 1ª versión SQL traer crudo y redondear en C#**. `AVG FILTER` + coalesce vs `DefaultIfEmpty`; dos `CalcularEdadDias`/`ExtraerSublote` distintos; leer guía directo de tabla.

## SERVICIO 3 — ReporteTecnicoProduccionService (PRODUCCIÓN)
Endpoints (`ReporteTecnicoProduccionController.cs`): `filter-data`, `POST obtener` (produccion_diaria), `POST obtener-tabs`, `POST generar`, `GET diario/{id}`, `sublotes`, `GET cuadro/{id}`, `GET clasificacion-huevo-comercio/{id}`, exportar excel. DTOs: `ReporteTecnicoProduccionDto.cs`, `ReporteTecnicoProduccionTabsDto.cs`.
Agregaciones pesadas: **N+1 severo (el peor)** en `ObtenerDatosDiariosPorLPPAsync` = 4 queries/día (`movimiento_aves` + 3× `traslado_huevos`). `EsSemanaCompletaConsolidada(LPP)Async` (`CountAsync` por semana×sublote). `ConsolidarDatosDiarios` (~40 col), `ConsolidarSemanales`, `GenerarReporteCuadroAsync` (`datosHastaSemana` O(n²)), `ObtenerReporteProduccionTabsAsync`.
Plan SQL:
- (a) `fn_reporte_tecnico_produccion_diario(p_lote_postura_produccion_id, p_company_id, p_fecha_inicio, p_fecha_fin, p_desde_produccion_diaria BOOL DEFAULT true)` → grano diario, saldos window + JOINs pre-agregados `movimiento_aves` y `traslado_huevos` por fecha (mata las 4 queries/día). semana=25+ceil(edad/7), clamp edad<1→26.
- (b) `fn_reporte_tecnico_produccion_semanal` → `CROSS JOIN LATERAL`; semana completa = `COUNT(*)>=7`; consolidado por `lote_postura_base_id` con `bool_and`.
- (c) `fn_reporte_tecnico_produccion_cuadro` → semanal + guía + acumulados `SUM() OVER (ORDER BY semana ROWS UNBOUNDED PRECEDING)`.
- (d) `fn_reporte_tecnico_produccion_tabs` → por galpón + general por fecha/semana + guía.
- (e) clasificacion-huevo-comercio: GroupBy(semana).Sum simple, fn opcional.
Riesgos: redondeo menor pero `decimal`→`numeric` (no double); `SaldoHembras` usa `g.Max` (¿último del día? replicar MAX literal salvo confirmación); parámetro `p_desde_produccion_diaria`; `traslado_huevos` sumas columna por columna.

## PRIORIZACIÓN (recomendada por el agente)
- **🥇 P1 — ReporteTecnicoProduccionService** (diario → semanal → cuadro → tabs): mayor ROI, peor N+1 (4 queries/día), redondeo bajo → menor riesgo de bit-exactitud.
- **🥈 P2 — ReporteTecnicoService levante** (diario primero): O(n²) + N+1 grandes, pero **bit-exactitud ALTO** → extraer `Calculos` puros + tests como oráculo y redondear en C# al inicio.
- **🥉 P3 — ReporteContableService**: beneficio medio, más casos borde; empezar por `movimientos-huevos` (aislado y barato).
- Transversal: validar cada `fn_*` vs el endpoint vivo ANTES de conmutar a `SqlQueryRaw`; migración idempotente a mano; sin DDL en prod sin OK del usuario.
