# DB Studio — Rediseño "pro", endurecimiento y permisos por tabla

> Plan aprobado. Reemplaza el uso directo de PostgreSQL desde el módulo `db_studio` (menú id 17, `/config/db-studio`).

## Contexto y objetivo
Ver/editar/crear/borrar tablas, vistas y funciones; ver y editar registros; ejecutar SQL; **dar permiso por usuario de qué tablas ver (lectura o escritura)** mientras el admin ve todo; **monitorear y controlar hilos/concurrencia**; acelerar lectura/escritura. Ya existe un esqueleto a medio construir (~1.800 líneas backend + ~4.900 frontend) con fallas graves.

### Diagnóstico (auditoría)
1. **Dos backends compitiendo**: el *ingenuo* cableado al controller (`Infrastructure/Services/DbStudioService.cs`, SQL crudo en `query/select` y `query/execute`, identificadores interpolados, conexión de EF prestada, ~13 métodos `NotImplementedException`) y el *endurecido huérfano* (`ReadOnlyQueryService`/`DbSchemaService`/`DbIntrospectionService` + helpers `Infrastructure/DbStudio/`, con parametrización, guard SELECT-only, auditoría — pero **ningún controller lo usa** y la tabla `dbstudio_audit` no existe).
2. **Autorización app-wide neutralizada**: `Program.cs` con `AllowAllPolicyProvider` + `allowAll` → todo `[Authorize(Policy=...)]` pasa. Hoy DB Studio está abierto a cualquier autenticado. La protección debe ir **dentro del controller/servicio**.
3. **No existen**: permisos por tabla, monitor/control de concurrencia, edición de vistas/funciones.
4. **Frontend** a medio construir; llama endpoints que hoy 500.

### Decisiones confirmadas (usuario)
- **Alcance BD**: opera igual en local y prod, sin distinción de entorno. Protección por **rol + grants + auditoría + confirmación**, no por ambiente. Kill-switch `DbStudio.Enabled`.
- **Concurrencia**: monitoreo en vivo + admin puede cancelar/terminar sesiones (`pg_cancel_backend`/`pg_terminate_backend`). Sin tuning de `max_connections`.
- **Permisos no-admin**: grant por tabla/vista con nivel lectura o escritura (no DDL). Admin ve/opera todo.
- **Frontend**: rediseño pro reusando lo válido (paleta Italfoods, clean-code `funciones/`+`models/`).

## Arquitectura objetivo
Un solo backend endurecido (partials en `Funciones/` + cálculo puro en `Application/Calculos/`). Toda operación: identificadores validados (`EnsureValidIdent`/`QI`/`QTable`), valores parametrizados, una sola sentencia, `statement_timeout`, límite de filas, **autorización por rol/grant** y **auditoría**. Conexión `NpgsqlDataSource` **singleton** propio (pool configurable, `ApplicationName="DbStudio"`), no la conexión de EF. Lectura en transacción `READ ONLY`. Seguridad en 3 capas: permiso de menú `db_studio.access` → `DbStudioAuthorization` (admin vs grants) → auditoría.

## Backend — cambios
1. **DTOs**: canónico `Application/DTOs/DbStudioDtos.cs` (clases camelCase). Extender: `ViewDto, FunctionDto, RoutineSourceDto, ObjectGrantDto, GrantRequest, ActivitySessionDto, PoolStatsDto, LockDto, MyAccessDto, SqlClassificationDto`. Eliminar records duplicados de `DTOs/DbStudio/Contracts.cs`.
2. **Servicio en partials** `Infrastructure/Services/DbStudio/` (namespace plano `...Services`): ancla `DbStudioService.cs` + `Funciones/DbStudioService.{Introspection,Query,Ddl,Data,ExportImport,Audit}.cs`. Cálculo puro → `Application/Calculos/DbStudioSqlCalculos.cs` (clasificar sentencia, multi-statement, armar DDL, validar ident.).
3. **Autorización**: `DbStudioAuthorization.cs` (`IsAdminAsync` patrón `LotePosturaProduccionService`; `CanRead/CanWrite`; `FilterVisibleObjectsAsync`; `EnsureCanExecuteArbitrarySql`). `DbStudioPermissionService.cs` (CRUD de grants).
4. **Concurrencia**: `DbStudioConcurrencyService.cs` (`GetActivityAsync` con `pg_stat_activity`+`pg_blocking_pids`+`max_connections`; `GetPoolStatsAsync`; `CancelBackend`/`TerminateBackend` admin, auto-protección, auditado).
5. **Entidades + migración idempotente** `AddDbStudioGrantsAndAudit`: `DbStudioObjectGrant` (UserId, CompanyId, SchemaName, ObjectName, AccessLevel Read|Write, GrantedBy, GrantedAtUtc; único por usuario+company+objeto) y `DbStudioAudit` (Action, Schema?, Object?, SqlText, ResultSummary jsonb, Success, ActorUserId, ActorEmail, CompanyId, IpAddress?, CreatedAtUtc) + Configurations + `DbSet`s + `CREATE TABLE/INDEX IF NOT EXISTS`.
6. **Permisos/menú**: `PermissionSeed.cs` + keys `db_studio.access|admin|manage_grants|concurrency`; asociar `db_studio.access` al menú id 17 (`MenuSeed.cs`/`MenuPermission`).
7. **Controller** rewrite con enforcement explícito; nuevos endpoints (views, functions+source, grants, my-access, concurrency activity/pool/locks/cancel/terminate); `403` claro.
8. **DI/opciones**: `NpgsqlDataSource` singleton; `Configure<DbStudioOptions>`; registrar nuevos servicios; un solo `IDbStudioService`. `DbStudioOptions` += `PoolMinSize, PoolMaxSize, StatementTimeoutSeconds, ReadOnlyConnectionString?, MaxExportRows`.

## Frontend — `features/db-studio/`
`models/` (tipos), `funciones/` (puras: export, árbol, fila→{data,where(PK)}, clasificar SQL), `data/db-studio.service.ts` reescrito. `pages/`: `db-studio-main` (shell), `explorer` (árbol filtrado por grants + detalle), `data-grid` (CRUD por PK, paginado), `query-console` (lectura vs escritura/DDL admin + confirmación), `object-editor` (tabla/columna/índice/FK + fuente de vistas/funciones), `permissions` (grants admin), `activity` (concurrencia en vivo). UI Italfoods; getters estables; guards por permiso.

## DB / migraciones
Migración EF idempotente; probar local `dotnet ef database update`; en deploy se aplica sola.

## Casos de prueba
- **Unit (xUnit)** `tests/ZooSanMarino.Application.Tests/DbStudioSqlCalculosTests.cs`: clasificador, multi-statement, validación de identificadores, armado DDL.
- **Integración** (`make up`, JWT admin/no-admin): no-admin solo ve tablas con grant; `query/select` rechaza no-SELECT; DDL/execute no-admin → 403; admin CRUD/DDL/vista/función/export/import OK; concurrencia lista/termina sesión; fila en `dbstudio_audit`.
- **Frontend** `yarn build` + smoke. Cierre `make down`.

## Fases
0. Docs + tracker. 1. Backend núcleo + seguridad. 2. Escritura (CRUD/DDL/vistas/funciones/export/import + grants). 3. Concurrencia. 4. Frontend pro. 5. Tests + limpieza.
