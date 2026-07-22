# Tracker — Gestión de Granjas: cascada + refresco entre tabs + scoping por granja asignada

Plan: [gestion_granjas_cascada_refresh_plan.md](fase_de_desarrollo/gestion_granjas_cascada_refresh_plan.md)

## Backend
- [x] `GestionGranjasCalculos.cs` (Application/Calculos) — reglas puras (visibilidad + cascada)
- [x] `FarmService.DeleteAsync` — cascada soft-delete núcleos + galpones
- [x] `FarmService.HardDeleteAsync` — cascada hard-delete (consistencia)
- [x] `NucleoService.GetAllAsync` — scoping estricto por granjas asignadas + granja activa
- [x] `GalponService.GetAllAsync` — scoping estricto por granjas asignadas + granja activa
- [x] `GestionGranjasCalculosTests.cs` (xUnit) — 7 tests
- [x] `dotnet build` (0 errores, 0 warnings) + `dotnet test` verde (558/558)

## Frontend
- [x] `GestionGranjasRefreshService` (bus de refresco entre tabs) + spec
- [x] `NucleoService` — `getAll(force)`, `invalidate()`, invalidar en CRUD
- [x] `farm-list` — notify('farm') en create/update/delete
- [x] `nucleo-list` — notify('nucleo') en CRUD + reacción a 'farm'
- [x] `galpon-list` — notify('galpon') en CRUD + reacción a 'farm'/'nucleo'
- [x] `yarn build` (0 errores; solo warning preexistente de bundle budget)
- [x] Typecheck aislado de los 2 specs nuevos (tsc --noEmit, exit 0)

## Notas / hallazgos
- ⚠️ Harness de tests del FRONT roto de ANTES (no lo introduje): `tsconfig.spec.json`
  hereda `exclude:["**/*.spec.ts"]` del base → `ng test` compila 0 specs. Además hay specs
  preexistentes rotos (`app.component.spec.ts` usa `title` inexistente; specs con `Can't find
  stylesheet`). `yarn test` no corre en este estado. Pendiente decidir si se arregla aparte.

## Verificación pendiente (opcional, requiere stack vivo)
- [ ] Preview E2E: crear granja → visible en tabs Núcleos/Galpones sin recargar
- [ ] Preview E2E: eliminar granja → núcleos/galpones desaparecen
- [ ] Preview E2E: núcleos/galpones scopeados a granjas asignadas
  (No ejecutado: BD local :5433 es compartida con el trabajo en paralelo del usuario y el
   backend en Dev tiene landmines de entorno/correos; ofrezco correrlo bajo pedido.)

---

# Tracker — Corrida por lote base + galpón (Panamá) en Lote Pollo Engorde

> Sesión paralela (independiente de la de arriba; no toca sus archivos).
> Plan: [lote_engorde_corrida_panama_plan.md](fase_de_desarrollo/lote_engorde_corrida_panama_plan.md)

## Backend
- [x] `GestionLotesEngordeCalculos.cs` (Application/Calculos) — `SiguienteNumeroCorrida` + `ConstruirNombreCorrida`
- [x] `LoteAveEngorde` entity — `NumeroCorrida` + config `numero_corrida` + índice `(company, base, galpon)`
- [x] Migración idempotente `AddNumeroCorridaLoteAveEngorde` (generada en worktree aislado, `ADD COLUMN/CREATE INDEX IF NOT EXISTS`)
- [x] `CreateLoteAveEngordeDto` — `AutoNombrePorCorrida` (flag) · `LoteAveEngordeDetailDto` — `NumeroCorrida`
- [x] `LoteAveEngordeService.CreateAsync` — asignar MAX+1 y nombre (gate por flag+base+galpón); `ProjectToDetail`; `UpdateAsync` preserva
- [x] `GestionLotesEngordeCalculosTests.cs` (xUnit) — 9 casos verdes
- [x] `dotnet build` (API+Infra "Build succeeded" en worktree) + `dotnet test` verde (9/9)

## Frontend
- [x] `lote-engorde.service.ts` — `numeroCorrida` (DTO) + `autoNombrePorCorrida` (create DTO)
- [x] `lote-engorde-list.component.ts` — `recomputeNombrePanama()` (create-only, base+galpón), flag en `save()`, sin reescribir en edición
- [x] `lote-engorde-list.component.html` — preview "Nombre del lote → 96 - 1" en Panamá; referencia en detalle
- [x] `yarn build` (0 err; solo warning bundle budget preexistente)

## Migración BD / Verificación
- [ ] Aplicar migración en BD local :5433 (la aplica el deploy sola; local pendiente — BD compartida con las otras sesiones, se ofrece bajo pedido)
- [ ] Smoke Panamá: base 96 galpón A → `96 - 1`; repetir → `96 - 2`; galpón B → `96 - 1` (requiere que el usuario reconstruya+reinicie su backend)
- [ ] Ecuador sin regresión: nombre libre + base opcional, `numero_corrida` NULL

---

# Tracker — Seguimiento Producción (Postura): heredar Lote padre al cerrar Levante

> Sesión paralela (compartida — NO borrar este archivo, solo agregar). Empresa detectada: **Demo**;
> afecta a todas (módulo Postura Levante/Producción compartido).
> Plan: [seguimiento_produccion_hereda_lote_padre_plan.md](fase_de_desarrollo/seguimiento_produccion_hereda_lote_padre_plan.md)

## Diagnóstico (hecho)
- [x] Causa raíz 400: `CrearLoteProduccion` no copia `LoteId`/`LotePadreId` del levante
      (`LotePosturaLevanteService.cs:86`) → `lote_postura_produccion.lote_id = NULL` →
      `ProduccionService` exige `LoteId>0` y lanza 400 (`ProduccionService.cs:270` y `:444`)
- [x] Confirmado: `LotePosturaProduccion` solo se crea al cerrar levante (no hay create directo)
- [x] Confirmado: levante siempre tiene `LoteId` (lo pone `LoteService.cs:366`) → herencia válida
- [x] Inventario "400 vs 4000" NO es bug: `4000`=código del ítem, `400 kg`=disponible real
      (label `modal-seguimiento-diario.component.ts:464`); validación de consumo correcta

## Backend — fix herencia (hecho)
- [x] `CrearLoteProduccion` — agregar `LoteId = lev.LoteId` + `LotePadreId = lev.LotePadreId`
- [x] `dotnet build` backend (0 errores, sin nuevas advertencias)
- [ ] Test unit: cerrar levante `LoteId=X/LotePadreId=Y` → producción hereda X/Y (pendiente escribir)
- [ ] `dotnet test` verde

## Datos — backfill (script listo, NO aplicado)
- [x] `backend/sql/backfill_lote_postura_produccion_lote_id.sql` (idempotente, solo filas NULL/≤0)
- [ ] Aplicar backfill en BD (lo corre la sesión/flujo que controla la BD; NO tocar :5433 compartida)
- [ ] Verificar: lote de producción existente (p.ej. id 10) queda con `lote_id` poblado

## Verificación E2E (HECHA — backend vivo :5002, JWT+X-Secret-Up minteados, BD :5433 restaurada)
- [x] Cerrar levante (id 15) → `POST /api/LotePosturaLevante/15/cerrar` = **200**
- [x] Producción creada (id 12 `P-LOTE 235A`) **heredó `lote_id=123`** (antes NULL) — fix confirmado
- [x] `POST /api/Produccion/seguimiento` (lote 12, all-zeros) = **201 Created** (id 670), ya NO el 400
- [x] Restaurado por SQL: borrado prod 12 + seguimiento 670 + espejo; levante 15 → `Abierto` (updated_at original) → **cero huella**
- [ ] (No corrido) Consumo con stock 400 kg: 3000 bloquea, ≤400 permite — validación de front ya confirmada por lectura de código

## Fase 2 — Inventario "400 vs 4000" (HECHA — verificación por SQL de lectura)
- [x] `inventario_gestion_movimiento` item 208/farm 88: **un solo Ingreso = 400 kg** ("Llegada a planta"); stock = 400 kg
- [x] Confirmado: NO existe ningún 4000; el `4000` es el **código** del ítem (`codigo="4000"`) en el label del dropdown. Sin bug, sin valor omitido.

## Opcional / seguimiento aparte
- [ ] UX: en el label del alimento, prefijar el código (`Cód. 4000 — …`) para no confundir con cantidad
- [ ] Aplicar backfill a lotes existentes (p.ej. lote 10 `P-LOTE 217` → `lote_id=125` desde levante 17) — pendiente en BD
- [ ] Decidir si el backfill se envuelve en migración EF idempotente (coordinar snapshot con sesión Panamá)

---

# Tracker — "Reabrir lote" reproductora engorde no persiste (confirma sin aplicar)

> Sesión de esta conversación (agregada, NO borrar lo de arriba).
> Plan: [reabrir_lote_reproductora_no_persiste_plan.md](fase_de_desarrollo/reabrir_lote_reproductora_no_persiste_plan.md)

## Diagnóstico (hecho)
- [x] Causa raíz: `LoteReproductoraAveEngordeService.ReabrirAsync` carga la entidad con join+`AsNoTracking`
      → queda SIN rastrear → `SaveChangesAsync` es no-op → `reabierto` nunca se escribe en BD
- [x] El endpoint devuelve DTO `reabierto=true` (mutado en memoria) → engaña al front (`puedeEliminar=true`),
      pero `SeguimientoDiarioLoteReproductoraService.DeleteAsync` relee el valor REAL (false) → lanza el guard
- [x] Evidencia interna: `SeguimientoDiario...UpdateAsync` documenta y sortea ESTE mismo comportamiento con
      `EntityState.Modified`; `ConfirmarAsync` lo evita cargando sin join (rastreada) → sí persiste
- [x] Bug gemelo detectado: `LoteReproductoraAveEngordeService.UpdateAsync` (editar maestro) mismo patrón →
      las ediciones del lote reproductora tampoco persisten
- [x] Columnas `reabierto/novedad_apertura/reabierto_por/reabierto_at` verificadas en BD local `:5433`
- [x] Lote de la captura (`LR-6654597192` / "32") NO está en BD local → el usuario prueba en otro entorno

## Backend — fix (hecho)
- [x] `ReabrirAsync` — cargar entidad RASTREADA (patrón `ConfirmarAsync`: `AnyAsync` scope + load directo)
- [x] `UpdateAsync` — `_ctx.Entry(ent).State = EntityState.Modified;` antes de `SaveChangesAsync` (bug gemelo)
- [x] `dotnet build` (Infra + API) → **0 errores, 0 advertencias**
- [x] `dotnet test` → **568/568 verde** (567 Application + 1 Domain), sin regresión

## Verificación E2E (pendiente, requiere stack vivo + token local)
- [ ] Reproducir: lote Cerrado (7 confirmados) → reabrir → `SELECT reabierto` sigue `false` (bug)
- [ ] Post-fix: reabrir con novedad → `reabierto=true` + `novedad_apertura`/`reabierto_at` poblados
- [ ] Post-fix: eliminar registro del lote reabierto → 200 OK; back resetea `reabierto=false` (recierra)
- [ ] Post-fix: editar lote reproductora (nombre/aves) → cambios persisten (fix bug gemelo)

## Alcance NO tocado
- Sin migración (columnas ya existen), sin cambios de front ni de contrato del DTO.
- Estado/guard/reset de `DeleteAsync`, aritmética de aves/saldos y el mini-modal de novedad: intactos.

---

# Tracker — Lote Reproductora Aves de Engorde: ajustes de creación/edición

> Sesión paralela (compartida — NO borrar este archivo, solo AGREGAR esta sección).
> Plan: [lote_reproductora_engorde_ajustes_creacion_plan.md](fase_de_desarrollo/lote_reproductora_engorde_ajustes_creacion_plan.md)
> ⚠️ Solape: otra sesión toca `LoteReproductoraAveEngordeService` (`ReabrirAsync`/`UpdateAsync`, sección de arriba).
>   Mi R4 toca `DeleteAsync` (método distinto) y el controller `Delete`. Coordinar al editar el mismo archivo.

## R1 — Quitar "Código reproductora" al crear ✅ (edit modal lo conserva)
- [x] `createBulkRow()` — eliminado control `codigoReproductora`
- [x] HTML modal bulk — eliminado campo "Código reproductora"
- [x] `saveBulk()` — ya no envía `codigoReproductora` (queda null)

## R2 — "Nombre del lote" obligatorio pero vacío (no prellenar con lote principal) ✅
- [x] `createBulkRow()` — `nombreLote: ['']` (quitado `baseNombre`)
- [x] `save()` (crear) — quitado fallback `|| loteSeleccionado.loteNombre`

## R3 — "Edad (días)": congelar al cerrar (DECIDIDO — sin migración) ✅
- [x] `ReproStats`+`GetReproStatsAsync` — agregado `MaxFecha` (fecha de cierre = último registro)
- [x] `ReproductoraEngordeCalculos.CalcularEdadDias(...)` puro + 6 tests xUnit
- [x] `Map()` — usa el cálculo (Vigente = hoy−fecha; Cerrado = congela en MaxFecha)

## R4 — Bloquear eliminar reproductora con registros cargados ✅
- [x] `DeleteAsync` — si hay `SeguimientoDiarioLoteReproductoraAvesEngorde` → InvalidOperationException
- [x] Controller `Delete` — try/catch → 400 (ValidationProblem con Detail)
- [x] Front `deleteRegistro()` — guard por `numRegistros > 0` + Toast

## R5 — Permiso editar/eliminar la reproductora ✅ (aprobado: implementar ahora)
- [x] Keys `lote_reproductora_engorde.editar` / `.eliminar`
- [x] Migración-seed `20260722190000_SeedPermisosEditarEliminarLoteReproductoraAveEngorde` (idempotente NOT EXISTS)
      → otorga a roles con menú `/config/lote-reproductora-ave-engorde`. Designer derivado del snapshot actual
      (mismo modelo, sin tocar `ZooSanMarinoContextModelSnapshot.cs` → no colisiona con Panamá; diff = solo 4 líneas)
- [x] Front — `HasPermissionDirective` + `UserPermissionService` + `PERM_EDITAR/PERM_ELIMINAR` + getters
      + guards en `edit()`/`deleteRegistro()` + `*appHasPermission` en botones (Ver siempre visible)
- Nota: en LOCAL requiere aplicar la migración + **re-login** (permisos viajan en la sesión). En prod se aplica sola al deploy.

## Validación
- [x] Front build (Node portable 22.23.1 + `ng build`) — 0 errores; solo warning bundle budget preexistente
- [x] `dotnet build` solución (previo) 0/0 + `dotnet test` Application = **572/572 verdes** (incluye 6 tests edad)
- [x] Build Infrastructure con migración+Designer = 0/0; diff Designer↔snapshot = solo las 4 transformaciones esperadas
- [~] Build final de solución dio **lock de `Infrastructure.dll`** por la API del usuario corriendo (PID 4740) —
      NO es error de código (la compilación pasó; es el copy-to-bin contra el proceso vivo)

## Pendiente de aplicar (BD)
- [ ] Aplicar la migración-seed en BD local :5433 (compartida — se ofrece bajo pedido) + re-login para ver el gate
- [ ] Smoke E2E: crear (sin campo código, nombre vacío obligatorio); editar/borrar según permiso; borrar con registros → 400; edad congelada al cerrar

---

# Tracker — CRUD de ubicación seguro: mover/editar/eliminar Núcleo · Galpón · Lote (transversal multipaís)

> Sesión independiente (compartida — NO borra ni toca las secciones de arriba; otras sesiones las usan; solo AGREGA esta).
> Plan: [gestion_ubicacion_nucleo_galpon_lote_plan.md](fase_de_desarrollo/gestion_ubicacion_nucleo_galpon_lote_plan.md)
> Incidente raíz prod: editar el núcleo de un galpón creó otro galpón y hubo que migrar los lotes por BD.

## Diagnóstico (auditado en el código — no implementar sin OK)
- [x] RC1 — Granja es parte de la PK del núcleo `{NucleoId,GranjaId}`; `UpdateAsync` solo cambia nombre → mover granja imposible
- [x] RC2 — Mover galpón no arrastra lotes (ubicación denormalizada `granja_id/nucleo_id/galpon_id`, FKs `Restrict`)
- [x] RC3 — `Galpon.CreateAsync` auto-regenera ID e inserta si el `GalponId` ya existe → duplicado silencioso
- [x] RC4 — Front núcleo deja editar granja en edición (no bloqueada)
- [x] RC5 — Borrado de núcleo/galpón sin validar hijos ni cascada

> Enfoque decidido con el usuario: **cascada por función SQL** en `/backend/sql/fn_mover_ubicacion.sql` + **incluir Fase 3**.
> Estado: **BACKEND + FRONTEND COMPLETOS** (build 0/0, front `ng build` OK, 13/13 xUnit). Funciones SQL validadas contra BD real (smoke con ROLLBACK). Falta: aplicar `fn_mover_ubicacion.sql` en PROD (manual, como las demás fns) + smoke E2E con stack vivo (BD :5433 compartida → bajo pedido).

## Fase 0 — Blindaje anti-duplicado / anti-huérfano
- [x] `GalponService.CreateAsync` — quitado auto-regenerar-ID; `GalponId` duplicado → `InvalidOperationException`; carrera 23505 → falla explícita (helper `EsClaveDuplicada`)
- [x] `Galpon/Nucleo Controller` Create/Update/Delete — `try/catch InvalidOperationException → 400 {message}`
- [x] Front `nucleo-list` — granja bloqueada en edición (disabled) + `getRawValue()` en save
- [x] Front `galpon-list` — edición siempre `update`; granja+núcleo+galponId bloqueados en edición
- [x] `GalponService.UpdateAsync` — ya NO cambia ubicación (solo nombre/medidas/tipo); mover va por MoverAsync

## Fase 1 — Mover Lote de ubicación
- [x] **Auditoría bloqueante (HECHA):** ubicación denormalizada en 18 tablas (verdad del esquema por information_schema). `seguimiento_diario` NO denormaliza (usa `lote_id`).
- [x] `fn_mover_lote` (lotes + lote_postura_levante/produccion) + `MoverLoteDto` + `LoteService.MoverUbicacionAsync` (valida destino, no toca nombre/fase)
- [x] `POST /api/Lote/{loteId}/mover` + front acción "Mover ubicación" (permite misma granja; nuevo, distinto de traslado de aves)

## Fase 2 — Mover Galpón (cascada a lotes)
- [x] `fn_mover_galpon` (galpón + 13 tablas hijas por `galpon_id`) + `MoverGalponDto` + `GalponService.MoverAsync`
- [x] `POST /api/Galpon/{galponId}/mover` + front "Mover" separado de "Editar" (datos)
- [x] Smoke SQL (ROLLBACK): re-key movió 4 galpones + 2 lotes; cero duplicados

## Fase 3 — Mover Núcleo a otra granja (re-key) — INCLUIDA
- [x] `fn_rekey_nucleo` (insert-repoint-delete transaccional; colisión/inexistencia → RAISE) + `MoverNucleoDto` + `NucleoService.MoverAsync`
- [x] `POST /api/Nucleo/{nucleoId}/{granjaId}/mover` + front "Mover a otra granja" con impacto
- [x] Smoke SQL con ROLLBACK verde (núcleo 324 g5→g2 arrastró galpones+lotes; ROLLBACK sin cambios)

## Fase 4 — Flujo completo de eliminar
- [x] `Nucleo.DeleteAsync` — bloquea si hay galpones o lotes (postura+engorde) activos
- [x] `Galpon.DeleteAsync` — bloquea si hay lotes (postura+engorde) activos
- [x] Controllers `Delete` → 400 {message}; front ya muestra `err.error.message`

## Transversal
- [x] `Application/Calculos/UbicacionCalculos.cs` (puro) + `UbicacionCalculosTests.cs` — **13/13 verdes**
- [x] País-agnóstico: los `move` no alteran nombres/numeración de lote (CO/EC/PA)
- [x] Validación: `dotnet build` API 0/0 · `dotnet test` 13/13 · `ng build` OK (solo warning bundle preexistente)
- [ ] **Aplicar `backend/sql/fn_mover_ubicacion.sql` en PROD** (manual; en local ya aplicado)
- [ ] Smoke E2E con stack vivo (rebuild+restart backend del usuario; BD :5433 compartida) — bajo pedido
