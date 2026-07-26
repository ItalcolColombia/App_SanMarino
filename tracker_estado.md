# Tracker — Limpieza artefacto `$safeNavigationMigration(...)` en 25 templates HTML

**Plan:** [fase_de_desarrollo/limpieza_safe_navigation_migration_plan.md](fase_de_desarrollo/limpieza_safe_navigation_migration_plan.md)

## Preparación
- [x] Localizar ocurrencias: `rg "\$safeNavigationMigration" frontend/src --glob "*.html"` → 81 ocurrencias / 25 archivos
- [x] Confirmar que NO existe declaración en ningún `.ts`/`.d.ts` (solo los 25 `.html`)
- [x] Auditar patrones: envoltura simple · `(expr)!` · `(expr)?.prop` · anidadas · args con comas/`$any(...)`/literales
- [x] Referencia del fix ya aplicado en `aves-engorde/seguimiento-aves-engorde-list` (envoltura quitada tal cual)

## Ejecución
- [x] Script de reemplazo (paréntesis balanceados, consciente de comillas, pasadas hasta 0 ocurrencias) en scratchpad
- [x] Aplicar sobre los 25 templates → **93 reemplazos** (81 eran líneas; varias con llamadas múltiples/anidadas)
- [x] Verificar 0 ocurrencias restantes (`rg safeNavigationMigration frontend` → 0)
- [x] Auditar `git diff` (cada hunk = solo quitar envoltura; `!` conservado fuera: `selectedLote()?.granjaId!`; precedencia de `!== undefined`, pipes y ternarios intacta)

## Validación
- [x] `cd frontend && yarn build` con Node portable 22.23.1 → **0 errores** en 223 s; único warning: bundle budget preexistente (1.89 MB > 1.50 MB). `strictTemplates` aceptó todas las expresiones desenvueltas (incl. los `?.granjaId!`)
- [x] Reporte final — cambios sin commitear en worktree `busy-lovelace-bcedd5`, listos para revisión
