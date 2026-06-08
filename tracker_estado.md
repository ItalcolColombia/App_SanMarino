# Tracker de Estado — Desarrollo Actual

> **Tarea:** DB Studio — rediseño pro, endurecimiento de seguridad, permisos por tabla (lectura/escritura), monitor/control de concurrencia, y CRUD/DDL completo desde la app (reemplazar uso directo de PostgreSQL).
> **Plan:** [fase_de_desarrollo/db_studio_plan.md](fase_de_desarrollo/db_studio_plan.md)
> **Inicio:** 2026-06-07

## Decisiones confirmadas
- [x] Opera en local y prod sin distinción de entorno; protección por rol + grants + auditoría + confirmación (kill-switch `DbStudio.Enabled`)
- [x] Concurrencia: monitor en vivo + admin puede cancelar/terminar sesiones (sin tuning de `max_connections`)
- [x] Permisos no-admin: grant por tabla/vista con nivel lectura o escritura (nunca DDL ni SQL arbitrario)
- [x] Frontend: rediseño pro reusando lo válido (paleta Italfoods, clean-code `funciones/`+`models/`)

---

## FASE 0 — Docs (regla del repo)
- [x] Plan en `fase_de_desarrollo/db_studio_plan.md`
- [x] Reiniciar `tracker_estado.md` (este archivo)

## FASE 1 — Backend núcleo + seguridad  ✅
- [x] `DbStudioOptions` ampliado (Pool, StatementTimeout, ReadOnlyConnectionString, MaxExportRows) + bind `Configure<DbStudioOptions>` en `Program.cs`
- [x] `NpgsqlDataSource` singleton dedicado (`DbStudioRuntime`: write + read-only con statement_timeout) reusando `ConnectionStringResolver`
- [x] Entidades `DbStudioObjectGrant` + `DbStudioAudit` + Configurations + `DbSet`s en `ZooSanMarinoContext`
- [x] Migración EF idempotente `AddDbStudioGrantsAndAudit` (CREATE TABLE/INDEX IF NOT EXISTS) + `dotnet ef database update` local (se dropeó tabla `dbstudio_audit` stale vacía)
- [x] `Application/Calculos/DbStudioSqlCalculos.cs` (clasificar sentencia, multi-statement, armar DDL, validar ident.)
- [x] `IDbStudioAuthorization` + `DbStudioAuthorization` (admin/grants/filtros)
- [x] Consolidar DTOs en `DbStudioDtos.cs`; eliminado `DTOs/DbStudio/Contracts.cs` y servicios huérfanos
- [x] Servicio en partials: ancla + `Funciones/DbStudioService.{Introspection,Query,Audit}.cs`
- [x] Rewrite `DbStudioController` con enforcement explícito (403/400/500 mapeados)
- [x] `PermissionSeed` keys `db_studio.*`
- [x] DI: registrados servicios nuevos, un solo `IDbStudioService`
- [x] `dotnet build` 0 errores · SQL introspección validada contra BD real
- [ ] (pendiente) asociar `db_studio.access` al menú id 17 — el seed de menú solo corre en BD vacía; se hará por SQL idempotente o desde el módulo de menús

## FASE 2 — Escritura (CRUD / DDL / objetos)  ✅
- [x] `Funciones/DbStudioService.Data.cs` — insert/update/delete (parametrizado, WHERE obligatorio)
- [x] `Funciones/DbStudioService.Ddl.cs` — create/alter/drop tabla/columna/índice/FK + create-or-replace vista/función
- [x] `Funciones/DbStudioService.ExportImport.cs` — export CSV/JSON/SQL + import CSV/JSON
- [x] `ExecuteSql` (admin): clasifica, bloquea peligrosas, una sentencia, timeout, auditada
- [x] `IDbStudioPermissionService` + `DbStudioPermissionService` (CRUD grants) + endpoints
- [x] Endpoints views/functions(+source) en controller
- [x] `dotnet build` 0 errores

## FASE 3 — Concurrencia  ✅
- [x] `IDbStudioConcurrencyService` + `DbStudioConcurrencyService` (activity/pool/locks)
- [x] `CancelBackend`/`TerminateBackend` admin, auto-protección, auditado
- [x] Endpoints concurrency en controller
- [x] `dotnet build` 0 errores · SQL `pg_stat_activity`/`pg_blocking_pids` validada contra BD real

## FASE 4 — Frontend pro  ✅
- [x] `models/db-studio.models.ts` + `funciones/db-studio.funciones.ts` (puras)
- [x] `data/db-studio.service.ts` extendido al contrato consolidado (views/functions/grants/concurrency/my-access/classify/execute)
- [x] Workspace único `pages/db-studio-main` con tabs role-aware (Explorador / Consola SQL / Permisos / Actividad)
- [x] Explorador: árbol (tablas/vistas/funciones) filtrado por grants + detalle (datos/columnas/índices/FK)
- [x] Grilla de datos: editar inline / insertar / borrar por PK, paginado, export CSV (solo si write/admin)
- [x] Consola SQL admin (clasifica/confirma destructivas)
- [x] Permisos: gestor de grants (admin)
- [x] Actividad: dashboard de concurrencia en vivo + cancelar/terminar sesión (admin)
- [x] Routing/módulo simplificados a un solo workspace; UI Italfoods; signals/computed (sin getters que rompan CD)
- [x] `yarn build` OK
- [ ] (pendiente usuario) probar en navegador con sesión real (requiere X-Secret-Up + JWT del front)

## FASE 5 — Tests + limpieza  ✅
- [x] `DbStudioSqlCalculosTests.cs` (xUnit) verde — 36 tests
- [x] Removido código muerto/duplicado: servicios huérfanos, DTOs record duplicados, naive service, páginas/componentes placeholder del front
- [x] `dotnet build` 0 errores · `dotnet test` 57/57 · `yarn build` OK · sin procesos huérfanos
- [ ] (pendiente usuario) E2E autenticado admin/no-admin (audit, kill sesión) en entorno con sesión

---

## ⏳ Pendientes del usuario
- Asignar el permiso `db_studio.access` al menú id 17 desde el módulo de **Permisos/Menús** (el seed de menú solo corre en BD vacía). El backend ya bloquea por rol/grant aunque el menú esté visible.
- Crear grants para usuarios no-admin desde la pestaña **Permisos** del módulo.
- Validar el flujo en navegador (el backend ya quedó probado: build, 57 tests, migración aplicada, SQL validada contra la BD real).
