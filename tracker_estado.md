# Tracker — Consolidar migraciones Santa Reyes + activar features en Demo

**Plan:** [fase_de_desarrollo/activar_features_santa_reyes_demo_plan.md](fase_de_desarrollo/activar_features_santa_reyes_demo_plan.md)

## Consolidación
- [x] Build backend del checkout (árbol sin commitear) 0 err / 0 warn ANTES de commitear
- [x] Commit `feat(santa-reyes)` — fases 1-5 + ux filtros de contexto + CLAUDE.md (paquete completo)
- [x] Merge rama worktree `claude/determined-agnesi-104f60` (fix `fn_rekey_nucleo` + migración `20260725210000`)

## Migración Demo (`20260726000000_ActivarFeaturesSantaReyesEnDemo`)
- [x] Flags Demo: `maneja_codigos_erp_avicola` + `clasificacion_huevo_por_items` + `permite_traslado_aves_cross_etapa` = true (lookup por nombre, `IS DISTINCT FROM`)
- [x] Catálogo huevo SR → Demo (21 ítems, guarda por índice único company+pais+codigo; 0 colisiones auditadas)
- [x] Designer clonado del 230000, ModelSnapshot intacto; `Down()` best-effort con guardas FK
- [x] Aplicada en `sanmarinoapplocal:5433`; Demo = t/t/t + 10 Primera / 11 Pnc
- [x] Idempotencia probada re-ejecutando el SQL del Up (`UPDATE 0`, `INSERT 0 0`)

## Validación final
- [x] `yarn build` front (node portable 22.23) — verde, solo warning de bundle budget preexistente
- [x] `dotnet ef migrations list` sin pendientes tras el merge
- [x] `dotnet test` Application.Tests verde
- [ ] Push + deploy — REQUIERE OK EXPLÍCITO (no ejecutado)
