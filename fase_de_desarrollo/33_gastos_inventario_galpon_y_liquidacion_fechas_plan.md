# Plan — Ecuador: (A) Gastos de inventario sin exigir galpón/núcleo + (B) Liquidación Técnica Pollo Engorde (fechas)

Dos requerimientos de Ecuador (correos de Costos), independientes entre sí, en dos módulos ya existentes. Este documento es el STEP 1 (plan) — **no se ha tocado código todavía**; queda para validación de Moises antes de implementar.

---

## PARTE A — Gastos de inventario: no debería exigir Núcleo/Galpón

### Lo que pide el correo
El consumo de insumos (no-alimento) se maneja en Costos **solo por Granja + Lote (corrida 01-07)**, nunca por galpón. Hoy la pantalla obliga (en apariencia) a elegir Granja, Núcleo, Galpón y Lote. Al elegir Granja debería desplegarse directo el listado de corridas.

### Lo que encontré (auditoría del código actual)
- **El backend YA descuenta el stock solo a nivel granja.** `InventarioGastoService.CreateAsync` ([InventarioGastoService.cs:434](backend/src/ZooSanMarino.Infrastructure/Services/InventarioGastoService.cs#L434)) llama `_inventario.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(req.FarmId, null, null, item.Id, ...))` — **núcleo y galpón siempre van `null`** al motor de inventario. `NucleoId`/`GalponId` en la entidad `InventarioGasto` son campos opcionales de **referencia/auditoría** únicamente (así lo dice el propio subtítulo de la pantalla: "El stock se descuenta a nivel granja; núcleo, galpón y lote quedan como referencia"). Esto ya cumple lo que pide Costos a nivel de negocio.
- **El front tampoco exige núcleo/galpón en la validación.** `save()` ([gastos-inventario-page.component.ts:259-276](frontend/src/app/features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component.ts#L259-L276)) solo valida `formFarmId`, `formLoteId`, `formConcepto` y que haya líneas — no bloquea por galpón/núcleo.
- **El bloqueo es 100% de percepción/UX.** El modal "Registrar gasto de inventario" reutiliza `<app-filtro-select>` ([filtro-select.component.html](frontend/src/app/features/lote-levante/pages/filtro-select/filtro-select.component.html)) — componente **compartido** (lo usan ~10+ módulos) que pinta 4 selects en una sola fila (Granja / Núcleo / Galpón / Lote) todos con la misma jerarquía visual, sin indicar que núcleo/galpón son opcionales. El encargado de granja los llena porque estén ahí, aunque el dropdown de Lote ya se puebla apenas se elige Granja (`applyFiltersToLotes()` filtra por granja únicamente cuando núcleo/galpón están vacíos — [filtro-select.component.ts:232-264](frontend/src/app/features/lote-levante/pages/filtro-select/filtro-select.component.ts#L232-L264)).
- **Complicación real detectada (no mencionada en el correo):** en Ecuador, una "corrida" puede tener **más de un registro `lote_ave_engorde`** — uno por galpón — compartiendo el mismo nombre/código de lote. Ejemplo real documentado: corrida "2601" = lote id 19 (Galpón-2) + lote id 20 (Galpón-1) (memoria `liquidacion-engorde-ecuador-descuadre`). Si el select de Lote se llena filtrando solo por Granja, el encargado vería **dos filas "2601"** (una por galpón) sin más diferencia que un id interno invisible. No rompe el stock (es a nivel granja), pero sí puede dejar el gasto "referenciado" a veces al lote-galpón-1 y a veces al lote-galpón-2 de la misma corrida real — dato de auditoría inconsistente aunque el negocio no lo note.

### Propuesta
1. **Frontend, modal de creación únicamente:** ocultar los selects de Núcleo y Galpón (dejar visible Granja → Lote → Fecha), agregando un input `@Input() showNucleoGalpon: boolean = true` a `FiltroSelectComponent` (default `true` = **cero cambio de comportamiento** en el resto de módulos que lo usan) y pasar `false` desde `gastos-inventario-page.component.html`. Los filtros de **lista/búsqueda** (fuera del modal) se mantienen igual — ahí sí sirven para acotar resultados ya registrados.
2. **Lotes duplicados por corrida multi-galpón (el punto no cubierto por el correo):** dos caminos, a elegir por Moises:
   - (a) Dejarlo como está — se ven las 2 filas de la corrida en el select, cualquiera de las dos sirve como referencia (no afecta stock ni reportes de costos).
   - (b) Deduplicar el select de Lote por nombre/ERP de corrida cuando no hay galpón seleccionado, guardando como referencia el primer `loteId` encontrado.
   → **Necesito que confirmes cuál preferís** antes de tocar el componente compartido.
3. Sin cambios de backend/payload — `NucleoId`/`GalponId` siguen siendo opcionales (ya son `string?` nullable), no hay migración.

### Archivos a tocar (si se aprueba)
- `frontend/src/app/features/lote-levante/pages/filtro-select/filtro-select.component.ts` (+html): nuevo `@Input() showNucleoGalpon`.
- `frontend/src/app/features/gastos-inventario/pages/gastos-inventario-page/gastos-inventario-page.component.html`: pasar `showNucleoGalpon="false"` en el `<app-filtro-select>` del modal de creación (no en el de filtros de lista).
- (Si se elige opción b) `filtro-select.component.ts`: dedup en `applyFiltersToLotes`/`buildGalponesFromFilterData` cuando `showNucleoGalpon=false`.

---

## PARTE B — Liquidación Técnica Pollo Engorde: fechas

Todo en `frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts` (+ `.html`), salvo B3 que también toca backend.

### B1 — Columna TOTAL no muestra fechas (debería tomar el primer galpón)

**Lo que pide el correo:** el TOTAL de Alistamiento/Encasetamiento/Liquidación debe tomar las fechas del **primer galpón** que encasetó (el que arrancó primero la corrida), no quedar vacío.

**Root cause encontrado:**
- `liquidacionTotales()` ([indicador-ecuador-list.component.ts:711-810](frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts#L711-L810)) fuerza `fechaInicioLote: null, fechaCierreLote: null` (líneas 806-807) y ni siquiera asigna `fechaAlistamiento`/`fechaLiquidacion`.
- Tanto la tabla en pantalla ([indicador-ecuador-list.component.html:643,650,657](frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.html#L643)) como el export a Excel ([indicador-ecuador-list.component.ts:1020-1022](frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts#L1020-L1022)) tienen el string `'—'` **hardcodeado** en la columna TOTAL para esas 3 filas — ni siquiera miran el valor de `tot`.

**Fix propuesto:** en `liquidacionTotales()`, ubicar el ítem con `fechaInicioLote` (encasetamiento) **más antiguo** entre los galpones de la corrida (= primer galpón encasetado) y usar sus 3 fechas (alistamiento/encasetamiento/liquidación) para el TOTAL. Reemplazar el `'—'` fijo en HTML y en export por el valor real (mismo helper `fecha()`/`formatearFechaLote()` que ya usan las demás filas).

**Pendiente de confirmar:** la fecha de liquidación del TOTAL, ¿también debe ser la del galpón que encasetó primero (mismo criterio), o un criterio distinto (ej. la liquidación más tardía de la corrida)? El correo no lo aclara explícitamente — asumo "mismo galpón" por consistencia salvo que Moises indique otra cosa.

### B2 — Bug de un día menos (registrás 18/05, el reporte muestra 17/05)

**Root cause encontrado — es de VISUALIZACIÓN, no de dato guardado:**
- Al guardar en "Editar Lote de Engorde", el front ancla la fecha elegida a medianoche UTC (`new Date(raw.fechaEncaset + 'T00:00:00Z').toISOString()`, [lote-engorde-list.component.ts:577-578](frontend/src/app/features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.ts#L577-L578)) y la columna es `timestamptz` — el dato en BD queda correcto.
- El bug está en `formatearFechaLote()` ([indicador-ecuador-list.component.ts:975-979](frontend/src/app/features/indicador-ecuador/pages/indicador-ecuador-list/indicador-ecuador-list.component.ts#L975-L979)): hace `new Date(fechaIso).toLocaleDateString('es-EC', {...})`. `toLocaleDateString` convierte el instante UTC a la **zona horaria local del navegador** antes de formatear. Ecuador es UTC-5: `2026-05-18T00:00:00Z` se interpreta como `2026-05-17 19:00` hora local → imprime "17/05/2026".
- Por eso el módulo "Editar Lote" (usa `<input type="date">`, sin conversión de zona horaria) muestra la fecha correcta, pero el reporte de Liquidación (que sí convierte a hora local) la muestra un día antes.
- **Posible bug sistémico más amplio:** el mismo patrón (pipe `date:` de Angular o `toLocaleDateString`) aparece también en `lote-engorde-list.component.html` (líneas 202, 521, 525, 735) para listar/ver lotes. No lo incluyo en el alcance de este fix (el correo reporta específicamente el reporte de Liquidación), pero lo dejo anotado como hallazgo — **avisame si querés que lo audite/corrija también ahí**.

**Fix propuesto:** cambiar `formatearFechaLote()` para extraer `YYYY-MM-DD` directamente del string ISO (sin construir `Date` ni usar `toLocaleDateString`) y reordenar a `DD/MM/YYYY`. Cambio acotado a este componente — no toca BD ni contrato de API.

### B3 — Fecha de liquidación: que se pueda elegir, no que se autogenere al hacer clic en "Liquidar lote"

**Root cause encontrado:**
- `CerrarLoteAveEngordeRequest` ([LiquidacionLoteEngordeDto.cs:22-25](backend/src/ZooSanMarino.Application/DTOs/LiquidacionLoteEngordeDto.cs#L22-L25)) solo tiene `ClosedByUserId`, `MermaUnidades`, `MermaKilos` — no hay fecha.
- `LoteAveEngordeService.CerrarLoteAsync` ([LoteAveEngordeService.cs:432](backend/src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs#L432)) hace `ent.LiquidadoAt = DateTime.UtcNow` a secas.
- El modal "Liquidar lote" ([modal-liquidacion-lote-engorde.component.html](frontend/src/app/features/aves-engorde/pages/modal-liquidacion-lote-engorde/modal-liquidacion-lote-engorde.component.html)) no tiene ningún campo de fecha. Ya existe un bloque análogo — "Merma (Costos)", visible solo para Ecuador (`esEcuador`) — donde encajaría un campo "Fecha de liquidación" (date input, default hoy).

**Fix propuesto:**
- Backend: agregar `FechaLiquidacion` (`DateTime?`, opcional) a `CerrarLoteAveEngordeRequest`. Si viene, usarla (anclada a medianoche UTC — mismo patrón que B2, para no reintroducir el bug de un día) en vez de `DateTime.UtcNow`. Si no viene, mismo comportamiento actual (compatibilidad hacia atrás, sin romper otros países que ya usan este endpoint).
- Frontend: `<input type="date">` "Fecha de liquidación" en el modal de cierre, default = hoy.

**Pendiente de confirmar:** ¿el campo debe verse solo para Ecuador (junto al bloque de Merma) o para todos los países que usan este mismo modal (Panamá ya tiene su propio bloque de 6 insumos)? El correo es específico de Ecuador, pero el modal es compartido entre países.

---

## Resumen de decisiones que necesito de Moises antes de codear
1. **Parte A:** lotes duplicados por corrida multi-galpón en el select — ¿dejar tal cual (2 filas visibles) o deduplicar por nombre de corrida?
2. **Parte B1:** fecha de liquidación del TOTAL — ¿mismo galpón que encasetó primero, o algún otro criterio?
3. **Parte B3:** campo "Fecha de liquidación" en el modal — ¿solo Ecuador o todos los países?

## Fuera de alcance (para no mezclar con esta tarea)
- Auditoría completa del bug de fecha (B2) en otras pantallas (`lote-engorde-list` y cualquier otra que use `| date:` sobre `fechaEncaset`/`fechaAlistamiento`).
- Cualquier cambio de esquema/BD — todo lo de arriba es frontend + un campo opcional de request en backend, sin migraciones.
