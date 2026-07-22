# Tracker — Seguimiento Reproductora Engorde: fechas y edición

Plan: [seguimiento_reproductora_engorde_fechas_edicion_plan.md](./fase_de_desarrollo/seguimiento_reproductora_engorde_fechas_edicion_plan.md)

## Frontend
- [x] F1 — `formatDate` del modal de detalle usa `ymdSinTz` (fix off-by-one tabla↔modal)
- [x] F2 — `@Input() fechaEncasetamiento` + getters `minFechaYmd`/`maxFechaYmd` + validator de rango en `fecha`
- [x] F2 — HTML modal: `[min]`/`[max]`, hint y mensajes de error `fechaMin`/`fechaMax`
- [x] F3 — Edición: `disable()` de campos duros (alimento/consumo, mortalidad/sel/error) + `enable()` en creación
- [x] F3 — `onSave()` usa `getRawValue()` (preserva valores bloqueados)
- [x] F3 — HTML: banner "eliminar y recrear" + candado en secciones bloqueadas al editar
- [x] F4 — Wire `[fechaEncasetamiento]` en el HTML de la lista

## Backend
- [x] B1 — `ReproductoraEngordeCalculos.EdadSeguimientoDias` + `EsEdadSeguimientoValida`
- [x] B2 — Validación edad [1,7] en `CreateAsync` y `UpdateAsync`
- [x] B3 — Tests xUnit de la lógica pura de edad

## Validación
- [x] `cd frontend && yarn build` (exit 0; único warning = bundle budget preexistente/aceptado)
- [x] `cd backend && dotnet build` (Infrastructure OK, exit 0)
- [x] `cd backend && dotnet test` (596 passed, 0 failed)
- [ ] Verificación funcional en preview — NO ejecutada: hay sesión paralela activa en el mismo repo (puertos :5002/:4200 + BD :5433 compartida + lock de API/bin). Se ofrece bajo pedido para no interrumpir esa sesión; el usuario puede verlo por HMR en su instancia.

## Notas / decisiones
- Fecha mínima = encaset+1; máxima = encaset+7 (guarda coherente, vetable).
- No se toca `fn_cruce_reproductora_a_engorde` ni datos de prod.

---

# Tracker — Alinear nombres de Lote Pollo Engorde (Panamá) al lote base asignado

> Sesión independiente (compartida — NO borra ni toca la sección de arriba; solo AGREGA esta).
> Plan: [fix_nombres_lote_engorde_panama_por_lote_base_plan.md](./fase_de_desarrollo/fix_nombres_lote_engorde_panama_por_lote_base_plan.md)
> Objetivo: los lotes de Panamá creados ANTES de la feature de corrida (nombre libre, `numero_corrida` NULL)
> quedan alineados a `"{lote base} - {corrida}"` según el lote base que ya tienen asignado.

## Diagnóstico (auditado contra la BD local = dump de prod, solo lectura)
- [x] Regla de nombre confirmada en código: `ConstruirNombreCorrida` = `trim(base) + " - " + n`;
      `numero_corrida = MAX(company, base, galpón) + 1` (`GestionLotesEngordeCalculos` + `LoteAveEngordeService.CreateAsync`)
- [x] Panamá = `pais_id=3`, `pais_nombre='Panama'` (SIN tilde en BD), `company_id=5`; front resuelve por ID **o** nombre
- [x] Estado real: 31 lotes Panamá; solo 4 con base (138,139,140,141); 139/140/141 ya cumplen; **138 `"94"` con corrida NULL** → a alinear
- [x] Ecuador (`pais_id=2`) con base = **0 filas** → sin riesgo de tocarlo; ningún lote con base tiene `pais_id` NULL
- [x] Simulación read-only del backfill: 138 → **`94 - 2`** (continúa desde max=1 del grupo base 1/galpón GALPON) ✔ (único cambio en local)

## Backend — migración (hecha)
- [x] `20260722210000_FixNombresLoteEngordePanamaPorLoteBase.cs` — backfill DML idempotente
      (Panamá + base + galpón + `numero_corrida IS NULL`; asigna `MAX(grupo)+ROW_NUMBER()` y reescribe `lote_nombre`)
- [x] `...Designer.cs` derivado del Designer de `20260722190000` (su `BuildTargetModel` verificado **idéntico** al snapshot actual)
- [x] **Model-neutral** → NO se toca `ZooSanMarinoContextModelSnapshot.cs` (evita lock de `API/bin` y colisión con sesiones paralelas)
- [x] `dotnet build` en worktree aislado (Infrastructure + API = **0/0**; el lock en el árbol principal es del build de la otra sesión, no error de código)
- [x] EF autoritativo: `has-pending-model-changes` = **sin cambios**; `migrations list` muestra la migración **al final de la cadena**
- [x] Commit `4893032` (main) — solo los 2 archivos de migración + el plan (sin tocar el tracker ni el trabajo de la otra sesión)

## Alcance / decisiones
- Idempotente: solo filas con `numero_corrida IS NULL` (re-ejecución = no-op; no pisa los ya correctos)
- NO se tocan: lotes sin base, Ecuador/Colombia, columnas de auditoría; `Down()` = no-op documentado (backfill de una vía)
- Panamá resuelto por NOMBRE del país (`LIKE 'panam%'`, robusto a tilde/ID)

## Aplicación
- [ ] Prod: la aplica el deploy sola (idempotente). Verificación post-deploy por `SELECT`.
- [ ] BD local :5433: NO se aplica desde aquí (compartida con sesiones paralelas + lock de bin) — se ofrece bajo pedido.
