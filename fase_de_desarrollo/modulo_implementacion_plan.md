# Plan — Módulo "Implementación" (cronogramas de entrega por empresa con checklist confirmable)

**Fecha:** 2026-07-20 · **Rama:** `postura-verenice-rev-6jul26`

## Objetivo

Módulo nuevo para **gestionar y auditar la implementación de la aplicación por empresa (y país)**: se crean **planes de implementación** (cronogramas) con un **checklist de tareas** (ej. "Parametrizaciones", "Capacitaciones", "Carga de datos", "Puesta en marcha"). Cada tarea se puede asignar a un **rol responsable** y/o a un **usuario concreto**. Flujo de doble check para garantizar la entrega:

1. El gestor marca la tarea **Completada** → queda fecha + usuario que marcó.
2. El **usuario asignado confirma** desde su vista "Mis tareas" que efectivamente se cumplió → queda fecha + usuario que confirmó.
3. Cuando todas las tareas están confirmadas, el plan pasa a **Completado** → evidencia auditable de la entrega/capacitación.

## Enfoque arquitectónico

Copiar el patrón del módulo **Vacunación** (el más reciente y completo: entidades + Configurations auto-aplicadas + migración + menú por migración idempotente + tests de Calculos) y la organización **partial class + `Funciones/`** de `MovimientoPolloEngorde`. Scoping multiempresa 100 % por `_currentUser.CompanyId` (header `X-Active-Company-Id` resuelto por `ActiveCompanyMiddleware`) + filtro por `PaisId` cuando hay país activo. Asignación de usuario por **Guid real** (`users.id`, vía `ICurrentUser.UserGuid`) para poder validar "solo el asignado confirma" y joinear nombres.

## Modelo de datos (2 tablas nuevas, snake_case, schema public)

### `implementacion_planes`
| Columna | Tipo | Nota |
|---|---|---|
| id | int identity PK | |
| company_id | int NOT NULL | scoping empresa |
| pais_id | int NULL | del contexto activo al crear |
| nombre | varchar(200) NOT NULL | |
| descripcion | varchar(2000) NULL | |
| fecha_inicio / fecha_fin | date NULL | rango del cronograma |
| estado | varchar(20) NOT NULL default 'borrador' | `borrador·en_progreso·completado·cancelado` (check) |
| created_at, created_by_user_id, updated_at, updated_by_user_id, deleted_at | auditoría estándar (`AuditableEntity`), borrado suave |

### `implementacion_tareas`
| Columna | Tipo | Nota |
|---|---|---|
| id | int identity PK | |
| plan_id | int NOT NULL FK → implementacion_planes (Cascade) | |
| company_id | int NOT NULL | denormalizado para scoping directo |
| categoria | varchar(100) NOT NULL | ej. "Parametrizaciones" |
| titulo | varchar(300) NOT NULL · descripcion varchar(2000) NULL · orden int | |
| fecha_programada | date NULL | vencida si < hoy y sigue pendiente |
| role_id | int NULL FK → roles (Restrict) | rol responsable |
| asignado_user_id | uuid NULL FK → users (Restrict) | usuario que confirma |
| estado | varchar(20) NOT NULL default 'pendiente' | `pendiente·completada·confirmada` (check) |
| fecha_completada timestamptz NULL · completada_por_user_id uuid NULL | check del gestor |
| fecha_confirmada timestamptz NULL · confirmada_por_user_id uuid NULL | confirmación del asignado |
| observaciones | varchar(2000) NULL | |
| auditoría estándar + deleted_at | |

Índices: `(company_id, pais_id)` en planes; `plan_id`, `asignado_user_id`, `(company_id)` en tareas.

## Reglas de negocio

- Toda lectura/escritura filtra `CompanyId == _currentUser.CompanyId && DeletedAt == null`. Con país activo, listar planes con `pais_id IS NULL OR pais_id = paisActivo`.
- **Completar**: solo tareas `pendiente` → `completada` + `fecha_completada = now` + `completada_por_user_id = UserGuid`.
- **Confirmar**: solo tareas `completada` y **solo por el usuario asignado** (`asignado_user_id == UserGuid`), si no → 403. Guarda fecha, usuario y observaciones opcionales.
- **Reabrir**: vuelve a `pendiente` y limpia fechas/usuarios de check y confirmación (para correcciones; queda updated_by como rastro).
- **Estado del plan derivado** tras cada cambio de tarea: `cancelado` se respeta; 0 tareas → `borrador`; todas confirmadas → `completado`; resto → `en_progreso`.
- **Plantilla por defecto** opcional al crear plan (categorías Parametrizaciones / Capacitación / Datos / Puesta en marcha) para estandarizar entregas.
- % avance = tareas completadas+confirmadas / total; % confirmado = confirmadas / total (redondeo a 1 decimal, total 0 → 0).

## Backend — archivos

| Acción | Archivo |
|---|---|
| Crear | `Domain/Entities/Implementacion/ImplementacionPlan.cs`, `ImplementacionTarea.cs` |
| Crear | `Application/DTOs/Implementacion/ImplementacionDtos.cs` (records) |
| Crear | `Application/Interfaces/IImplementacionService.cs` |
| Crear | `Application/Calculos/ImplementacionCalculos.cs` (resumen %, estado plan, vencida, puede-confirmar, plantilla) |
| Crear | `Infrastructure/Persistence/Configurations/Implementacion/ImplementacionPlanConfiguration.cs`, `ImplementacionTareaConfiguration.cs` |
| Modificar | `Infrastructure/Persistence/ZooSanMarinoContext.cs` (2 DbSet) |
| Crear | `Infrastructure/Services/Implementacion/ImplementacionService.cs` (ancla) + `Funciones/ImplementacionService.Planes.cs`, `.Tareas.cs`, `.Consultas.cs` (mis-tareas + usuarios/roles asignables) |
| Crear | `API/Controllers/ImplementacionController.cs` |
| Modificar | `API/Program.cs` (DI `AddScoped<IImplementacionService, ImplementacionService>()`) |
| Crear | Migración `AddImplementacionModule` (tablas, **idempotente** `CREATE TABLE IF NOT EXISTS` estilo `20260702014925_EnsureSeguimientoDiarioAvesEngordePanamaTable`) |
| Crear | Migración `AddImplementacionMenu` (seed menú por `key`, estilo `20260714193209_AddVacunacionMenu`; `role_menus` se asigna por UI de Roles) |
| Crear | `tests/ZooSanMarino.Application.Tests/ImplementacionCalculosTests.cs` |

### Endpoints (`api/Implementacion`, `[Authorize]`)
- `GET planes` · `GET planes/{id}` (detalle con tareas + resumen) · `POST planes` (con `usarPlantilla`) · `PUT planes/{id}` · `DELETE planes/{id}` (soft)
- `POST planes/{id}/tareas` · `PUT tareas/{id}` · `DELETE tareas/{id}` (soft)
- `POST tareas/{id}/completar` · `POST tareas/{id}/confirmar` (403 si no es el asignado) · `POST tareas/{id}/reabrir`
- `GET mis-tareas` (asignadas al usuario actual en la empresa activa)
- `GET usuarios-asignables` (users de la empresa vía `user_companies`) · `GET roles-asignables` (vía `role_companies`)

## Frontend — archivos (`frontend/src/app/features/implementacion/`)

| Acción | Archivo |
|---|---|
| Crear | `implementacion.routes.ts` (`IMPLEMENTACION_ROUTES`: `planes`, `planes/:id`, `mis-tareas`) |
| Crear | `models/implementacion.models.ts` (interfaces espejo de DTOs) |
| Crear | `services/implementacion.service.ts` (`HttpClient`, base `${environment.apiUrl}/Implementacion`; companyId viaja por interceptor) |
| Crear | `funciones/agrupar-tareas-por-categoria.funcion.ts`, `estado-tarea.funcion.ts` (puras) + `README.md` |
| Crear | `pages/planes-list/` (lista con % avance + modal crear/editar plan) |
| Crear | `pages/plan-detail/` (cronograma agrupado por categoría, KPIs, modal tarea, acciones completar/reabrir) |
| Crear | `pages/mis-tareas/` (tareas asignadas al usuario; botón **Confirmar cumplimiento**) |
| Modificar | `app.config.ts` (ruta `implementacion` con `authGuard`) |

UI: `ToastService` + `ConfirmDialogService` (nunca alert/confirm nativos), clases `.ux-*`/`.table-italfoods`/`.badge-*`, tokens `primary`/`success`/`danger`. Referencias estables (sin getters que alocan por ciclo — NG0103). Íconos del menú: reutilizar nombres ya presentes en `ICON_MAP` de `menu.service.ts`.

## Casos de prueba (xUnit — `ImplementacionCalculosTests`)

1. Resumen: total 0 → 0 %; redondeo a 1 decimal; confirmadas cuentan como avance.
2. Estado plan: cancelado se respeta; 0 tareas → borrador; todas confirmadas → completado; parcial → en_progreso.
3. Vencida: solo si hay fecha programada pasada y estado pendiente.
4. PuedeConfirmar: exige estado completada + asignado == usuario actual (nulls → false).
5. Plantilla por defecto: no vacía, órdenes crecientes, categorías esperadas.

## Validación

- `dotnet build` (0 errores) + `dotnet test` (SDK 10 user-local `%USERPROFILE%\.dotnet\dotnet.exe`).
- `dotnet ef database update` contra BD local :5433 (aditivo; no rompe otras ramas).
- `cd frontend && yarn build` (Node portable 22.23.1; único warning aceptado: bundle budget preexistente).
- Nota post-deploy: asignar el menú "Implementación" a los roles por la UI de Roles (`role_menus` no se siembra).
