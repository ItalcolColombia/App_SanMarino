# Plan — Consolidar migraciones Santa Reyes + activar features en la empresa Demo

## Contexto / problema
Las fases 1-5 de Santa Reyes (plan `santa_reyes_implementacion_plan.md`) quedaron implementadas
y aplicadas en la BD local, pero **sin commitear** → las 6 migraciones jamás llegarían al deploy
(prod las aplica solo al arrancar). Además `20260725230000_AlistarDemoParaPruebas` dejó la Demo
en flujo **clásico** (flags en false); decisión nueva del 25-jul-2026 PM: la Demo debe **exhibir
las features** para que el cliente evalúe sobre ella y vea los cambios que tendrá que realizar
(códigos ERP a diligenciar, clasificación de huevo por ítems, traslado cross-etapa).

## Enfoque
1. **Migración nueva `20260726000000_ActivarFeaturesSantaReyesEnDemo`** (data-only, Designer
   clonado del 230000, ModelSnapshot intacto), siguiendo el patrón "Features por EMPRESA" del
   CLAUDE.md (lookups por nombre, `IS DISTINCT FROM`, `NOT EXISTS`, timestamp posterior a todo SR):
   - Enciende en Demo los 3 flags: `maneja_codigos_erp_avicola`, `clasificacion_huevo_por_items`,
     `permite_traslado_aves_cross_etapa` (mismo set que Santa Reyes).
   - Copia a Demo el catálogo de huevo de SR (21 ítems `item_type='huevo'`, 10 Primera / 11 Pnc)
     en `catalogo_items` — sin él, el modal de clasificación no tiene ítems. Guarda alineada al
     índice único `(company_id, pais_id, codigo)`; auditado: 0 colisiones con ítems de Demo.
   - Campos ERP de Demo quedan vacíos a propósito (son "lo que hay que llenar").
   - `Down()` best-effort: borra solo ítems sin movimientos/stock (FKs RESTRICT) y apaga flags.
2. **Commits en `main`** (checkout principal): (a) paquete completo Santa Reyes fases 1-5 +
   diseño unificado de filtros (trabajo ya validado, estaba sin commitear), (b) migración Demo.
3. **Merge de la rama worktree `claude/determined-agnesi-104f60`** (fix `fn_rekey_nucleo` copia
   bodega, migración `20260725210000`) para que la cadena de migraciones quede completa en main.
4. **Sin push/deploy** — eso requiere OK explícito.

## Cadena de migraciones resultante (orden de aplicación)
`20260725175311` schema ERP → `190000` seed SR → `200000` huevo ítems → `210000` fn_rekey fix
(worktree) → `210323` cohortes cross-etapa → `220000` fn clasificación → `230000` alistar Demo →
`20260726000000` activar features en Demo.

## Casos de prueba
- Build backend checkout principal 0 err/0 warn ANTES de commitear (protege el commit grande).
- `database update` aplica solo la nueva; flags Demo = t/t/t; huevo Demo = 10 Primera + 11 Pnc.
- Idempotencia: re-ejecutar el SQL del Up() a mano → `UPDATE 0`, `INSERT 0 0`.
- `dotnet ef migrations list` final sin pendientes; `dotnet test` Application.Tests verde;
  `yarn build` front (node portable 22.23) verde.
