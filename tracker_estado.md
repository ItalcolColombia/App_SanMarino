# Tracker — Reporte Técnico Semanal Postura (Sanmarino): Levante + Producción vs Guía

**Plan:** [fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md](fase_de_desarrollo/reporte_tecnico_semanal_postura_plan.md)

## Fase 0 — Análisis
- [x] Mapear los dos Excel (Técnico Levante A382 / TEC. PRODUCCION A346): columnas, fórmulas, consolidación Gral
- [x] Exploración del código (workflow 6 agentes): guía genética, seguimientos, lotes, reportes existentes, multi-tenant, front
- [x] Plan escrito en fase_de_desarrollo

## Fase 1 — Backend: fn SQL nueva (diseño afinado: 1 fn, no 2)
- [x] `backend/sql/fn_reporte_semanal_levante_extras.sql` (conteos/kg/peso/unif/nutrición POR SEXO; misma semántica que fn_indicadores_levante_postura; TEMP propia `_seg_sem_rx`)
- [x] Producción SIN fn nueva: reusa `fn_indicadores_produccion_postura` + guía cruda (`ProduccionAvicolaRaw`) parseada en C# (menos superficie)
- [x] Migración `20260726160000_AddFnReporteSemanalLevanteExtras` (Designer clonado; Up=DROP+CREATE, Down=DROP)
- [x] Migración aplicada en BD local :5433 + sanity con lote real K345A (bases 7999H/1132M, arrastre de peso, kcal null-tolerante)

## Fase 2 — Backend: DTOs + service + controller
- [x] `ReporteSemanalLevanteExtrasRow` + `ReporteTecnicoSemanalDtos` (request, header, semana levante/producción, tabs, responses)
- [x] `ReporteTecnicoSemanalCalculos` (puro): % base fija (Excel), acumulados, incrementos, nutrición, masa/conversión/apareo, consolidación multi-galpón
- [x] Tests xUnit `ReporteTecnicoSemanalCalculosTests` — 30/30 verdes
- [x] `IReporteTecnicoSemanalService` + service (ancla + Funciones/Levante + Funciones/Produccion) con empresa efectiva + `ILocationScopeResolver` fail-closed
- [x] `ReporteTecnicoSemanalController` (POST api/ReporteTecnicoSemanal/levante | /produccion) + DI en Program.cs
- [x] `dotnet build` 0 errores 0 warnings + `dotnet test` verde

## Fase 3 — Menú solo Sanmarino
- [x] Migración `20260726160100_AddMenuReporteTecnicoSemanal`: ítem bajo Reportes (icon chart-bar), company_menus SOLO 'Agroavicola Sanmarino', role_menus SOLO roles Sanmarino con '/reportes-tecnicos' (verificado en local: 1 empresa, 4 roles)

## Fase 4 — Frontend
- [x] models + service TS (espejo DTOs); filtros reusando `ReporteTecnicoLevanteFilterService` con instancia propia (providers del componente)
- [x] Página main: toggle Levante/Producción + filter-card cascada 1-4 + tabs Consolidado/galpones + tabla 2 niveles (vista precalculada, sin NG0103)
- [x] funciones/: `columnas-reporte-semanal` (spec única de columnas) + `construir-aoa-reporte-semanal` (export multi-hoja: Gral + hoja por galpón) + README
- [x] Ruta `/reporte-tecnico-semanal` en app.config.ts (loadComponent + authGuard)
- [x] `ng build` 0 errores (solo warning budget preexistente)

## Fase 5 — Validación y cierre
- [x] Smoke API local con JWT minteado (empresa 1): POST levante K345 → 200, 2 tabs × 25 semanas + consolidado, guía casa con el Excel (21 gr/a/d, 147 acum, 145 peso, 70 U%); POST produccion → 200, 2 tabs × 44 semanas, semana 25 con guía parcial (REQ-012b OK)
- [x] Smoke UI en dev server: `/reporte-tecnico-semanal` carga y redirige a login (authGuard), 0 errores de consola

## Fase 6 — Vista Gráficas (réplica de las gráficas embebidas de los Excel)
- [x] `funciones/construir-graficas-reporte-semanal.funcion.ts` (pura): 8 gráficas Levante (Peso, Desv Peso %, Consumo, Increm H, Increm M, Retiro+Mort H, Retiro+Mort M, Uniformidad) + 6 Producción (% Producción, HTAA-HIAA-%HI, Consumo ave, Desv Peso %, Retiro+Mort H, Retiro M) — Real sólido / Guía punteada, colores del repo
- [x] Toggle Tabla ↔ Gráficas por tab en el componente (charts precalculados por generación, NgChartsModule/Chart.js)
- [x] `ng build` 0 errores (solo warning budget preexistente)
- [x] Smoke UI COMPLETO en dev server con sesión dev inyectada (sessionStorage, sin credenciales): Levante K345 → tabs Consolidado/K345A/K345B, tabla 2 niveles y 8 canvas pintados; Producción → P-K345A/P-K345B y 6 canvas pintados; 0 errores de consola
- [x] Servidores detenidos (sin procesos huérfanos)
- [x] Commit `3dd1f4a` en main (autor moisesmurillo, sin atribución)

## Fase 7 — Bloque POLLITOS con datos reales (fase 2 del plan)
- [x] Exploración BD local: `traslado_huevos` (340 filas, company 1) tiene tipo_destino='Planta' + estado='Completado' + cantidad_limpio/tratado + lote_postura_produccion_id → **HI Cargado es real y cruza por semana de vida** (verificado con P-K345A: sem 26=8.455 … sem 37=42.277)
- [x] Auditoría (3 agentes + BD): pollitos nacidos / % nacimiento **NO existen** en el esquema (no hay retorno de incubadora); el reporte técnico viejo arrastra el mismo hueco (`porcentajeNacimientos`/`pollitosVendidos` null hardcodeados). Documentado en el plan §9
- [x] `AgruparCargadosPorSemana` (puro, misma fórmula de semana que la fn) + consulta EF a `traslado_huevos` (Completado + destino Planta, limpio+tratado) con la misma resolución de fecha de encaset que la fn
- [x] DTO: `HuevosCargadosPlanta`, `HuevosCargadosPlantaAcum`, `PorcentajeCargaSobreIncubables` + suma en el consolidado
- [x] Tests xUnit: 35/35 verdes (5 nuevos: fórmula de semana, hora ignorada, acumulados/%, sin traslados, consolidado)
- [x] Front: 3 columnas nuevas en el bloque Pollitos + gráfica "Incubables producidos vs cargados a planta" (7ª de producción)
- [x] `dotnet build` 0/0 · `ng build` 0 errores · smoke API (totales 1.470.623 + 911.090 = 2.381.713 = BD) y smoke UI (tabla + 7 gráficas, 0 errores de consola)
- [x] Servidores detenidos
- [ ] Commit fase 2
