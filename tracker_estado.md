# Tracker — Fix Ajuste de Aves / % Ajuste (Indicador Ecuador)

Plan: [indicador_ecuador_fix_ajuste_aves_plan.md](fase_de_desarrollo/indicador_ecuador_fix_ajuste_aves_plan.md)

## Fórmula objetivo
- Ajuste de aves = `aves_encasetadas − aves_vendidas − (mortalidad + selección)`
- % Ajuste = `(ajuste_aves / aves_encasetadas) × 100`
- Aplica a TODOS los lotes (sin gate de merma).

## Checklist

### Backend — cálculo puro + SQL
- [x] `IndicadorEcuadorCalculos.AjusteAves` sin parámetro `mermaUnidades`
- [x] Test `IndicadorEcuadorCalculosTests` actualizado (ajuste = -3, sin merma)
- [x] `backend/sql/fn_indicadores_pollo_engorde.sql` — fórmula nueva, sin gate merma
- [x] `backend/sql/vw_liquidacion_ecuador_pollo_engorde.sql` — fórmula nueva, sin gate merma
- [x] Aplicado a BD local (psql) y verificado: ajuste = enc - vend - mort, no NULL

### Frontend — recalcula local (no depende del backend)
- [x] `liquidacion-reporte.component.ts` — `ajusteAves`/`porcentajeAjuste` siempre recalculan
- [x] `indicador-ecuador-list.component.ts` — `ajusteDe`/`porcentajeAjusteDe` + total + Excel
- [x] `indicador-ecuador-list.component.html` — matriz + tarjeta usan los métodos calculados
- [x] `yarn build` 0 errores

### Migración EF (despliegue a prod)
- [x] Detener backend local (libera el build)
- [x] `dotnet ef migrations add UpdateFnIndicadoresAjusteAvesTodosLotes`
- [x] Llenar `Up()` (función + vista con fix) y `Down()` (versión previa con gate merma) — OWNER/GRANT guardados por rol
- [x] `dotnet ef database update` local sin error (registrada en __EFMigrationsHistory)
- [x] `dotnet build` 0 errores
- [x] Verificar BD post-migración (ajuste = enc-vend-mort, no NULL en lotes sin merma)

### Prod (requiere confirmación explícita)
- [ ] Deploy → la migración aplica las funciones en RDS prod
