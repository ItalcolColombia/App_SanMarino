# Tracker — Ecuador: Gastos de inventario (sin galpón) + Liquidación Técnica Pollo Engorde (fechas)

Plan: [33_gastos_inventario_galpon_y_liquidacion_fechas_plan.md](fase_de_desarrollo/33_gastos_inventario_galpon_y_liquidacion_fechas_plan.md)

**Estado: CÓDIGO IMPLEMENTADO (Partes A y B) — falta validación E2E manual (login/datos reales) y confirmar 2 decisiones menores tomadas por defecto.**

Moises confirmó la decisión #1: la corrida (ej. "2601") se reporta **a nivel granja**, aunque tenga varios `lote_ave_engorde` (uno por galpón) — por eso se deduplica en el selector. Las decisiones #2 y #3 del plan quedaron con el criterio por defecto propuesto (ver plan) — avisar si hay que cambiarlas.

---

## Fase 0 — Diagnóstico (COMPLETA)
- [x] Auditar módulo Gastos de inventario: confirmar que el stock ya descuenta a nivel granja (backend) y que el front no bloquea por galpón/núcleo
- [x] Detectar la complicación no mencionada en el correo: corridas multi-galpón = múltiples `lote_ave_engorde` con mismo nombre
- [x] Auditar `liquidacionTotales()` y la columna TOTAL (pantalla + Excel): confirmar `'—'` hardcodeado
- [x] Diagnosticar el bug de un día menos: dato se guarda bien (UTC medianoche), el bug es de visualización (`toLocaleDateString` convierte a hora local)
- [x] Confirmar que "Liquidar lote" no acepta fecha (siempre `DateTime.UtcNow`)
- [x] Plan + tracker

## Fase 1 — Parte A: Gastos de inventario ✅ código + build OK
- [x] Confirmado con Moises: la corrida se reporta a nivel granja → deduplicar
- [x] `FiltroSelectComponent`: `@Input() showNucleoGalpon = true` (default sin cambios en otros módulos)
- [x] `applyFiltersToLotes()`: dedup por nombre de corrida (`loteId` más bajo como representante) cuando `showNucleoGalpon=false`
- [x] HTML: selects de Núcleo/Galpón ocultos con `@if (showNucleoGalpon)`
- [x] `gastos-inventario-page`: `showNucleoGalpon="false"` en el modal de creación (filtros de lista sin cambios) + `openCreate()` ya no hereda núcleo/galpón de la lista
- [x] `yarn build` OK

## Fase 2 — Parte B1: TOTAL con fechas del primer galpón ✅ código + build OK
- [x] `liquidacionTotales()`: `primerGalpon` = ítem con `fechaInicioLote` (encasetamiento) más antiguo; sus 3 fechas alimentan el TOTAL (alistamiento/encasetamiento/liquidación — mismo criterio para las 3, por defecto del plan)
- [x] Reemplazado el `'—'` hardcodeado en HTML (pantalla) y en export Excel por el valor real
- [x] `yarn build` OK

## Fase 3 — Parte B2: fix bug de un día menos ✅ código + build OK
- [x] `formatearFechaLote()`: extrae YYYY-MM-DD del ISO string con regex, sin pasar por `Date`/`toLocaleDateString`
- [x] `yarn build` OK
- [ ] Verificación visual con dato real (18/05/2026 registrado → debe mostrar 18/05/2026) — pendiente de sesión con login

## Fase 4 — Parte B3: fecha de liquidación editable ✅ código + build OK
- [x] Backend: `FechaLiquidacion` opcional en `CerrarLoteAveEngordeRequest` + uso en `CerrarLoteAsync` (ancla a medianoche UTC vía `DateTime.SpecifyKind(...Utc)`), default = comportamiento previo si no viene
- [x] Frontend: input date "Fecha de liquidación" en modal "Liquidar lote" — visible solo para Ecuador (`esEcuador`, junto al bloque de Merma); otros países no envían el campo → cero cambio de comportamiento para ellos
- [x] `dotnet build` (proyectos Application/Infrastructure) OK, 0 errores · `yarn build` OK

## Fase 5 — Validación E2E (PENDIENTE — no se pudo completar en esta sesión)
- [ ] Gastos de inventario: registrar gasto solo con Granja + Lote, confirmar stock descuenta a nivel granja y que una corrida multi-galpón aparece UNA sola vez en el selector
- [ ] Liquidación: corrida multi-galpón muestra fechas correctas en TOTAL
- [ ] Liquidación: fecha registrada en Editar Lote coincide con la mostrada en el reporte (sin el día de diferencia)
- [ ] Liquidar lote (Ecuador) con fecha elegida distinta a "hoy" y confirmar que persiste
- **Nota:** se intentó levantar el front (puerto 4300, sin chocar con la sesión paralela en :4200) para verificar visualmente — compiló limpio y cargó sin errores de consola, pero no había credenciales para loguearse y llegar a las pantallas concretas. El build del backend (`dotnet build` completo) chocó con locks de archivo de la API corriendo en paralelo (proceso 34588) — se verificó compilando solo `ZooSanMarino.Infrastructure`/`Application` (0 errores) para no interferir con esa sesión.
