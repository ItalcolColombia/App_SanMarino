# Tracker — Liquidación Panamá por CORRIDA (tab Pollo Engorde)

> Nota: `tracker_estado.md` está en uso por otra sesión paralela (Módulo Implementación);
> este tracker vive en archivo propio (mismo patrón que `tracker_estado_inventario_rename.md`).

Plan: [fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md](fase_de_desarrollo/liquidacion_panama_por_corrida_plan.md)

## Backend
- [x] DTOs `ReporteCorridaPanamaDto` / `ReporteCorridaPanamaItemDto` / `LoteCorridaPanamaResumenDto`
- [x] Cálculo puro `ReporteIndicadorPanamaCalculos.ConsolidarCorrida` (fórmulas espejo de la fn)
- [x] Tests xUnit `ReporteIndicadorPanamaCalculosTests` (identidad, sumas, ponderación, guards, vacío)
- [x] Interfaz + servicio: `GetReportePorCorridaAsync` (scoping company + granja + lote_nombre)
- [x] Controller: `GET api/ReporteIndicadorPanama/por-corrida`
- [x] **FIX descubierto:** endpoint por-lote existente 500eaba con datos reales (fn devuelve numerics de 36+ decimales → overflow de `System.Decimal` en Npgsql); casts `::numeric(18,6)` en el SELECT del servicio
- [x] `dotnet build` sin errores + `dotnet test` verde (434/434)

## Frontend
- [x] Función pura `corridas-panama.funcion.ts` (corridas distintas + filtro por corrida) + README
- [x] Service: interfaces TS + `getReporteCorridaPanama`
- [x] Componente lista: estado corrida Panamá + recálculo memoizado (NG0103) + rama Panamá en generar
- [x] Template: select Corrida (Panamá) / trío Año-Corrida-Prefijo solo Ecuador / ocultar Estado de lote en Panamá
- [x] Componente nuevo `liquidacion-reporte-corrida-panama` (tabs consolidado + por galpón + avisos)
- [x] `yarn build` sin errores (solo warning preexistente de bundle budget)

## Validación de datos (local)
- [x] Sembrados insumos de liquidación corrida 92 (granja 89: lotes 161/171/182; 192 se dejó SIN liquidar a propósito) — tag `registrado_por_user_id='seed-local-corrida'`
- [x] `por-corrida` HTTP 200: ítems == fn por lote (peso 2.350011 / conv 1.305012 / eefDos 437.999257 en 161); PA-88 listado en `lotesSinLiquidacion`
- [x] Consolidado verificado a mano: metros 4560, avesFinal 134 174, kiloPie 314 782.5, benef 133 970, enc 138 490, faltante 204, días ponderados 40
- [x] 404 corrida inexistente · 404 con token de empresa 3 (fail-closed multi-empresa) · filtro `galponId` OK
- [x] Endpoint por-lote existente (161) ahora responde 200 (antes 500 por overflow)
- [ ] Verificación visual en navegador (empresa Panamá) — **requiere reiniciar el backend local (`dev-back.ps1`)**: el proceso actual en :5002 corre el código viejo (no se tocó por trabajo paralelo)
- [x] Ecuador sin cambios de comportamiento (mismo DOM/wire; `peLotesSelector` devuelve la misma referencia sin corrida)
