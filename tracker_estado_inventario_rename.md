# Tracker — Rename neutro del módulo Inventario

Plan: [`fase_de_desarrollo/inventario_rename_neutro_plan.md`](fase_de_desarrollo/inventario_rename_neutro_plan.md)

> Tracker PROPIO de esta sesión. **No usar `tracker_estado.md`** (en uso por la feature "alimentos múltiples por género en levante", cambios sin commitear en el working tree — no revertir).

## Fase 0 — Auditoría y plan (en curso)
- [x] Leer CLAUDE.md + workflow obligatorio
- [x] Auditar backend catálogo (`ItemInventarioEcuador*`)
- [x] Auditar backend stock/movimiento (`InventarioGestion*` + FK `ItemInventarioEcuadorId`)
- [x] Auditar consumo Colombia + gate + parser metadata (capa A / jsonb)
- [x] Auditar frontend (service, config module, consumidores seguimiento)
- [x] Confirmar gotcha capa A: claves jsonb `itemInventarioEcuadorId`/`catalogItemId` persistidas + `[JsonPropertyName]`
- [x] Confirmar que `ColombiaInventarioIdResolutionCalculos.cs` NO existe (lógica inline en service)
- [x] Escribir plan por fases + esquema de nombres neutros
- [x] Escribir este tracker
- [x] **Presentar plan + decisiones (D1/D2/D3) al usuario** → OK: D1a (BD igual) + D2a (wire estable+alias) + D3 (primitivos)

### Estrategia final (post-OK)
Rename de token `ItemInventarioEcuador(?!Id)` → `ItemInventario` (case-sensitive, negative lookahead): neutraliza TIPOS + DbSet + navegación, y **conserva** el escalar FK `ItemInventarioEcuadorId` (= columna `item_inventario_ecuador_id` + wire `itemInventarioEcuadorId`, ambos retenidos por D1a/D2a). → **cero pins de JsonPropertyName, cero cambio de wire, cero DDL.**

## Fase 1-3 — Backend (catálogo + stock/mov + seguimiento) ✅ HECHO
- [x] Replace token (19 archivos) + git mv de 6 archivos catálogo a nombre neutro
- [x] Ruta neutra `api/inventario/items` como alias (se conserva `api/item-inventario-ecuador`) + Tag neutro
- [x] Escalar FK / claves jsonb / query params INTACTOS (verificado: 0 tokens tipo sueltos, 113 `...EcuadorId` conservados)
- [x] `dotnet build` 0/0 · `dotnet test` 61/61 verde

## Fase 4 — Migración BD → DESCARTADA por decisión D1a (BD igual)

## Fase 5 — Frontend catálogo + servicio ✅ HECHO
- [x] `git mv` carpeta `config/item-inventario-ecuador` → `config/item-inventario` + 9 archivos (routing/module/service/list/form)
- [x] Token `ItemInventarioEcuador(?!Id)` → `ItemInventario` + kebab de selectores/templateUrl/imports
- [x] Rutas API del service → `/inventario/items` (ruta neutra del backend; alias viejo vigente)
- [x] `app.config.ts`: import path + `ItemInventarioModule`; **URL `item-inventario-ecuador` conservada** (menú en BD)
- [x] `gestion-inventario.service.ts`: tipo `ItemInventarioDto` + rutas `/inventario/items` + **alias deprecado `ItemInventarioEcuadorDto`** (compat consumidores/otra sesión); wire `itemInventarioEcuadorId` intacto
- [x] Tipo migrado en 3 consumidores (engorde-comun, lote-produccion, gestion-inventario-page)
- [x] `ng build` OK (único warning = bundle budget preexistente)

## Fase 6 — Consumidores + parametrización
- [x] Tipo catálogo neutral en consumidores (vía rename + alias)
- [x] **Decisión de diseño:** los símbolos `*EcuadorPanama` / `isEcuadorOrPanama` / `updateEcuadorOrPanamaStatus` nombran el **flujo país-gated EC/PA** (mismo concepto que la directiva sanciónada `ShowIfEcuadorPanamaDirective` que el objetivo pide CONSERVAR y usar) → **se conservan** (renombrar un subconjunto sería inconsistente). El "smell" real era el catálogo (`ItemInventarioEcuador*`), ya neutralizado.
- [x] Campos `*ItemInventarioEcuadorId` (form) espejan la clave wire `itemInventarioEcuadorId` → se conservan (D2a).
- [x] Parametrización: se usan los primitivos existentes (`ShowIfEcuadorPanamaDirective`, `isEcuadorOrPanama`, `ManejaAlimentoPorGalpon`) → sin hardcode nuevo de "ecuador"; visibilidad de agua/galpón ya parametrizada.
- [ ] ⚠️ `lote-levante/modal-create-edit` (otra sesión, otro worktree): NO tocado; usa el alias `ItemInventarioEcuadorDto`. Coordinar migración del alias cuando esa feature aterrice.

## 🤝 HANDOFF para la próxima sesión (dejar alineado 100%)

**Commit base:** el rename del catálogo + config front está commiteado en `claude/epic-mendel-dac9f3` (ver `git log`). Backend `dotnet build` 0/0 + `dotnet test` 61/61, front `ng build` OK. **Nada de esto tocó BD, wire ni jsonb** (decisión D1a/D2a).

**Contexto imprescindible (leer antes de tocar):**
- Técnica usada: token `ItemInventarioEcuador(?!Id)` → `ItemInventario` (case-sensitive, negative lookahead). **Conservar SIEMPRE** el escalar `ItemInventarioEcuadorId` (espeja columna `item_inventario_ecuador_id` + wire `itemInventarioEcuadorId`) y las claves jsonb `itemInventarioEcuadorId`/`catalogItemId` (metadata seguimiento).
- Ruta backend: viva la vieja `/api/item-inventario-ecuador` + alias neutro `/api/inventario/items` (2 `[Route]` en `ItemInventarioController`). Front ya pega a la neutra.
- Toolchain: dotnet portable SDK10 (`"C:/Users/SAN MARINO/dotnet-portable/dotnet.exe"`), node portable 22.23.1. En worktree sin `node_modules` → junction a `App_SanMarino\frontend\node_modules` (ya creado aquí).
- ⚠️ `lote-levante/pages/modal-create-edit` lo edita OTRA sesión (feature multi-alimento). Compila por el **alias deprecado** `ItemInventarioEcuadorDto` en `gestion-inventario.service.ts`. **NO reescribir ese archivo**; coordinar.

**Tareas para alineación total (en orden):**
1. **Decidir símbolos de flujo país-gated** `*EcuadorPanama` / `isEcuadorOrPanama` / `updateEcuadorOrPanamaStatus` en consumidores (engorde-comun, lote-produccion, gestion-inventario-page, y levante):
   - Recomendación de esta sesión: **conservarlos** (nombran el flujo EC/PA, coherentes con la directiva sancionada `ShowIfEcuadorPanamaDirective`; el smell era el catálogo, ya resuelto).
   - Si el usuario prefiere neutralizarlos: hacerlo **coherente** (incluir `isEcuadorOrPanama` y valorar renombrar la directiva) y **actualizar templates**; verificar que no queden bindings rotos. Excepto `itemInventarioEcuadorId` (wire) y `*ItemInventarioEcuadorId` (form) que se conservan.
2. **Migrar `modal-create-edit` (levante) del alias** cuando la feature multi-alimento aterrice/mergee: cambiar import `ItemInventarioEcuadorDto` → `ItemInventarioDto` y las 3 usos. Coordinar con esa sesión para evitar conflicto.
3. **Quitar el alias TS** `export type ItemInventarioEcuadorDto = ItemInventarioDto` de `gestion-inventario.service.ts` cuando NADIE lo use (grep `ItemInventarioEcuadorDto` en `frontend/src` = 0).
4. **(Opcional, futuro — requiere OK de DDL prod) Fase C BD física:** migración EF idempotente rename `item_inventario_ecuador`→`item_inventario` + columna `item_inventario_ecuador_id`→`item_inventario_id` + índices `ix_item_inventario_ecuador_*` + vista `vw_validacion_alimento_engorde` + regen snapshot + **probar local**. Al hacerla, renombrar JUNTAS columna + prop `ItemInventarioEcuadorId` + wire `itemInventarioEcuadorId` (front+back mismo deploy) para mantener coherencia.

**Validación de cada paso:** `dotnet build` 0/0 + `dotnet test` verde · `ng build` (único warning aceptado = bundle budget). Grep de control: `grep -rEn 'ItemInventarioEcuador(?!Id)'` (perl) en `backend/src` y `frontend/src` debe seguir en 0 fuera de `/Migrations/`.

## Notas / gotchas
- Toolchain: `"C:/Users/SAN MARINO/dotnet-portable/dotnet.exe"` (SDK 10) y Node portable 22.23.1.
- Detener API antes de build de solución (bloquea DLLs).
- Capa A (jsonb) CONGELADA: nunca renombrar claves `itemInventarioEcuadorId`/`catalogItemId`.
- Modal levante (`lote-levante/pages/modal-create-edit`) tiene cambios sin commitear de OTRA sesión → coordinar, no revertir.
