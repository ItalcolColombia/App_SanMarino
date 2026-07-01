# Tracker — Refactorización y optimización multi-país

**Plan:** [refactor_multipais_optimizacion_plan.md](./fase_de_desarrollo/refactor_multipais_optimizacion_plan.md)
**Rama:** `refactor/optimizacion-multipais` (main intocable)
**Modo:** loop iterativo — cada ciclo cierra 1 ítem: implementar → build back+front → validación visual → commit → marcar `[x]`.

---

## Fase 0 — Línea base
- [x] Rama `refactor/optimizacion-multipais` creada desde `main`
- [x] Build backend baseline: **0 errores, 6 advertencias** (SeguimientoDiarioService x3 null-refs, EmailQueueProcessorService x1)
- [ ] Build frontend baseline (`yarn build`) — en curso
- [ ] `dotnet test` baseline
- [ ] Entorno local arriba para validación visual (`make up` / preview)

## Fase 1 — Código muerto (bajo riesgo)
- [x] Eliminar `backend/.../Services/managerUser.cs` (namespace UserAdmin, 0 referencias) + build ✅ 0 errores
- [x] Eliminar `RolePermissionsController.cs` + `RoleService.cs` + `IRoleService.cs` — endpoint duplicado de `RoleController` (misma ruta `api/Role/{id}/permissions/*`), `IRoleService` nunca registrado en DI → habría dado 500; `RoleController` + `IRoleCompositeService` es la vía viva ✅ build 0 errores
- [ ] Eliminar `frontend/src/app/features/test/http-helper-test/` (sin ruta ni import) + `features/company/` (service huérfano, la vía viva es `core/services/company`) — esperar build baseline front
- [ ] DECISIÓN USUARIO: `features/test/company-admin-test` y `config/farm-management/company-test.component` SÍ están montados en la UI de farm-management (paneles "test" visibles) — quitarlos cambia UI
- [x] Barrido: servicios back sin registro DI → solo `RoleService` (eliminado); `TicketEmailTemplates` es estático y vivo
- [ ] Barrido: componentes/servicios front sin ruta ni import (madge o grep sistemático)
- [ ] Barrido: DTOs y modelos huérfanos
- [ ] Clasificar `/backend/sql/`: scripts vivos (fn_/vw_/triggers) vs diagnósticos históricos (solo documentar)

## Fase 2 — Unificación multi-país (un dominio por ciclo)
> Diagnóstico: `aves-engorde-panama` = clon de `aves-engorde` (33 de 45 archivos duplicados; `indicadores-diarios-engorde-compute.service.ts` byte-idéntico; modal seguimiento 2100 vs 2109 líneas con deriva). Riesgo actual: fix en un país no llega al otro.
- [ ] Front: `aves-engorde` vs `aves-engorde-panama` → funciones/modelos compartidos, orquestadores por país
- [ ] Back: `SeguimientoAvesEngorde{,Ecuador,Panama}Service` → cálculo puro común en `Application/Calculos/` + parametrización país
- [ ] Back: `MovimientoPolloEngorde` vs `MovimientoPolloEngordePanama` → compartir core
- [ ] Liquidaciones Colombia/Ecuador → core común (sin tocar vistas Power BI)
- [ ] Validación visual de cada módulo unificado (datos idénticos pre/post)

## Fase 3 — Optimización BD (cómputo → funciones/vistas SQL)
- [ ] Inventariar endpoints con agregaciones pesadas en C# (candidatos: indicadores, liquidaciones, informes semanales)
- [ ] Por candidato: función SQL + migración EF idempotente + test de equivalencia numérica
- [ ] Revisar índices para filtros frecuentes (lote, fecha, company_id, pais_id)

## Fase 4 — Normalización y limpieza BD
- [ ] Cruce `information_schema` local vs entidades mapeadas → informe de tablas sin uso
- [ ] Informe de columnas legacy no mapeadas
- [ ] Script `DROP IF EXISTS` propuesto (NO ejecutar sin OK explícito)

## Fase 5 — Segunda pasada del loop
- [ ] Re-barrido de mejoras sobre lo refactorizado
- [ ] Resolver las 6 advertencias baseline del backend
- [ ] Resumen final: diff vs `main` + checklist de regresión visual

## Registro de ciclos cerrados
| # | Ítem | Commit | Validación |
|---|---|---|---|
