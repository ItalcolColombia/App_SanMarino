# Plan — Limpieza del artefacto `$safeNavigationMigration(...)` en templates HTML

**Fecha:** 2026-07-25 · **Rama:** `claude/busy-lovelace-bcedd5` (worktree aislado)

## Contexto

Una migración automática de Angular dejó llamadas a `$safeNavigationMigration(expr)` en 25 templates HTML del front. La función **no existe en ningún `.ts`** (verificado con `rg safeNavigationMigration frontend` → solo 25 `.html`, 81 ocurrencias). Al evaluarse en runtime produce `TypeError` (ctx.$safeNavigationMigration is not a function) en las vistas afectadas.

Ya se arregló solo en `features/aves-engorde/pages/seguimiento-aves-engorde-list/` (plan `seguimiento_pollo_engorde_ux_cascada_scroll_plan.md`, donde quedó registrado que el resto era tarea aparte — esta). El fix de referencia es la envoltura quitada tal cual: `calcularEdadDias(selectedLote?.fechaEncaset)`.

## Enfoque arquitectónico

**Limpieza 100 % mecánica, sin cambio de comportamiento ni rediseño:** `$safeNavigationMigration(expr)` → `expr` tal cual (la expresión interna ya trae su optional chaining). Todo lo que rodea la llamada se preserva byte a byte:

- `...(expr)!` → `expr!` (el `!` queda fuera y se conserva — lo exige `strictTemplates: true`).
- `...(expr)?.prop` → `expr?.prop`.
- Llamadas **anidadas** (`$safeNavigationMigration(f($safeNavigationMigration(x), $any(y)))`) → se resuelven con pasadas repetidas hasta 0 ocurrencias.
- Argumentos con comas, `$any(...)`, literales `'...'` e índices `?.['key']` → el reemplazo usa **escaneo de paréntesis balanceados consciente de comillas**, no regex ingenua.

Herramienta: script Node de un solo uso en el scratchpad (no queda en el repo), ejecutado con el Node portable 22.23.1. Sin cambios de BD/SQL, backend ni servicios.

## Archivos a modificar (25 templates, 81 ocurrencias)

| Archivo | Ocurr. |
|---|---|
| `shared/components/hierarchical-filter/hierarchical-filter.component.html` | 5 |
| `features/profile/profile.component.html` | 12 |
| `features/lote-levante/pages/modal-create-edit/modal-create-edit.component.html` | 10 |
| `features/lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.html` | 7 |
| `features/lote-levante/pages/graficas-principal/graficas-principal.component.html` | 6 |
| `features/traslados-huevos/components/modal-traslado-huevos/modal-traslado-huevos.component.html` | 5 |
| `features/traslados-aves/pages/inventario-dashboard/inventario-dashboard.component.html` | 5 |
| `features/engorde-comun/pages/modal-seguimiento-engorde/modal-seguimiento-engorde.component.html` | 4 |
| `features/galpon/components/galpon-list/galpon-list.component.html` | 3 |
| `features/movimientos-aves/components/modal-movimiento-aves/modal-movimiento-aves.component.html` | 3 |
| `features/lote-levante/pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component.html` | 3 |
| `features/farm/components/farm-list/farm-list.component.html` | 2 |
| `features/config/user-management/components/modal-create-edit/modal-create-edit.component.html` | 2 |
| `features/engorde-comun/pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component.html` | 2 |
| `features/lote-reproductora/pages/lote-reproductora-list/lote-reproductora-list.component.html` | 2 |
| `features/db-studio/pages/db-studio-main/db-studio-main.component.html` | 1 |
| `features/config/master-lists/master-lists.component.html` | 1 |
| `features/config/user-management/pages/tabla-lista-registro/tabla-lista-registro.component.html` | 1 |
| `features/traslados-huevos/pages/traslados-huevos-list/traslados-huevos-list.component.html` | 1 |
| `features/seguimiento-diario-lote-reproductora/pages/modal-seguimiento-reproductora/modal-seguimiento-reproductora.component.html` | 1 |
| `features/lote/components/modal-create-edit-lote/modal-create-edit-lote.component.html` | 1 |
| `features/lote/components/lote-list/lote-list.component.html` | 1 |
| `features/lote-levante/components/liquidacion-comparacion/liquidacion-comparacion.component.html` | 1 |
| `features/lote-levante/pages/tabla-lista-registro/tabla-lista-registro.component.html` | 1 |
| `features/reporte-tecnico-produccion/pages/reporte-tecnico-produccion-main/reporte-tecnico-produccion-main.component.html` | 1 |

## Reglas de negocio

Ninguna se toca. Regla rectora del refactor: **comportamiento idéntico** — la única diferencia observable es que las expresiones dejan de lanzar `TypeError` y evalúan lo que siempre debieron evaluar.

## Casos de prueba / validación

1. `rg "safeNavigationMigration" frontend/src` → **0 ocurrencias** tras el reemplazo.
2. `git diff` — auditar que cada hunk solo quita la envoltura (nada más cambia en el template).
3. `cd frontend && yarn build` (con Node portable 22.23.1 en PATH) → **0 errores**; único warning aceptado: bundle budget preexistente. Si `strictTemplates` exigiera un `!` en algún sitio puntual, se agrega solo ahí (aserción de tipo, sin efecto runtime) y se documenta.
