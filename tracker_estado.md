# Tracker — Módulo "Implementación" (cronogramas de entrega por empresa)

**Plan:** [fase_de_desarrollo/modulo_implementacion_plan.md](fase_de_desarrollo/modulo_implementacion_plan.md)

## Backend
- [x] Entidades `ImplementacionPlan` + `ImplementacionTarea` (Domain/Entities/Implementacion/)
- [x] DTOs records (Application/DTOs/Implementacion/)
- [x] `IImplementacionService` (Application/Interfaces)
- [x] `ImplementacionCalculos` puro (Application/Calculos)
- [x] Configurations EF (Persistence/Configurations/Implementacion/) + DbSets en contexto
- [x] Service partial: ancla + Funciones/ (Planes, Tareas, Consultas)
- [x] `ImplementacionController` + DI en Program.cs
- [x] Migración `AddImplementacionModule` (tablas idempotentes)
- [x] Migración `AddImplementacionMenu` (seed menú por key)
- [x] Tests `ImplementacionCalculosTests` (xUnit)
- [x] `dotnet build` 0 errores + `dotnet test` verde (456 pasan)
- [x] Migraciones aplicadas en BD local (tablas + menú verificados por psql)

## Frontend
- [x] `models/implementacion.models.ts`
- [x] `services/implementacion.service.ts`
- [x] `funciones/` (agrupar por categoría, estado visual) + README
- [x] Página `planes-list` (lista + modal crear/editar + cancelar/reactivar)
- [x] Página `plan-detail` (cronograma checklist + completar/reabrir + modal tarea)
- [x] Página `mis-tareas` (confirmar cumplimiento)
- [x] `implementacion.routes.ts` + registro en `app.config.ts`
- [x] `yarn build` 0 errores (solo warning de bundle budget preexistente)

## Cierre
- [ ] **Pendiente (manual, post-deploy):** asignar el menú "Implementación" a los roles que corresponda vía UI de Roles (`role_menus` no se siembra, igual que Vacunación)
- [ ] Smoke con usuario real: crear plan con plantilla → completar tarea → confirmar desde "Mis tareas" (requiere login; no automatizable sin credenciales)
