# CLAUDE.md

This file provides strict guidance, environment context, and testing workflows to Claude Code (`claude.ai/code`) when working with this repository.

---

## ⚠️ CRITICAL DEVELOPMENT WORKFLOW (MUST FOLLOW)

To prevent hallucinations, ensure architectural integrity, and maintain a clear context, you **MUST** follow this strict 2-step process for EVERY new requirement, feature, or bug fix BEFORE writing any functional code:

### STEP 1: Full Planning (`/fase_de_desarrollo/`)
1. Analyze the user's request thoroughly based on the existing Clean Architecture (Backend) and Angular Standalone architecture (Frontend).
2. Create a detailed Markdown planning document inside the `./fase_de_desarrollo/` directory (e.g., `./fase_de_desarrollo/feature_name_plan.md`). 
   * **Absolute Local Path Reference:** `/Users/chelsycardona/Desktop/App_SanMarino/fase_de_desarrollo/`
3. This document must contain:
   * Architectural design and technical approach.
   * Specific files, components, and services to create or modify.
   * Database changes or raw SQL scripts needed.
   * Business logic constraints and test cases.

### STEP 2: State Tracking (`tracker_estado.md`)
1. Open the `./tracker_estado.md` file.
   * **Absolute Local Path Reference:** `/Users/chelsycardona/Desktop/App_SanMarino/tracker_estado.md`
2. **CLEAR / WIPE** all its previous contents completely (remove the state of the previous task).
3. Add a clear title and a reference/link to the new planning document created in Step 1.
4. Break down the development plan into a **granular, step-by-step checklist** of small implementation tasks using Markdown checkboxes (`- [ ]`).
5. As you implement the code, continually update this file by checking off completed items (`- [x]`) to maintain a single source of truth for the development state.

---

## 🗄️ DATABASE & MIGRATION WORKFLOW

> 📌 **ESTADO ACTUAL (2026-05-26):** El historial de EF Core (`__EFMigrationsHistory`) en RDS prod fue saneado y está 100% alineado con las migraciones del código (48 = 48, 0 pendientes). `Database__RunMigrations=true` está activo en la TaskDef ECS prod (revisión `:94`), por lo que **las migraciones nuevas SÍ se aplican automáticamente al arrancar la app en cada deploy**.

### Flujo recomendado para nuevas migraciones

1. **Crear la migración EF normalmente** desde `/backend/src/ZooSanMarino.API/`:
   ```bash
   dotnet ef migrations add <MigrationName> \
     --project ../ZooSanMarino.Infrastructure \
     --startup-project . \
     --context ZooSanMarinoContext
   ```
2. **Hacer la migración idempotente** si toca schema (recomendado, no obligatorio):
   * En el `Up()` de la migración, reemplazar `migrationBuilder.AddColumn(...)` por `migrationBuilder.Sql("ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...")` cuando haya riesgo de que la columna ya exista por trabajo manual previo.
   * Lo mismo para `CreateIndex` → `CREATE INDEX IF NOT EXISTS`, `CreateTable` → `CREATE TABLE IF NOT EXISTS`.
   * Esto da un seguro extra contra re-runs accidentales o conflictos con scripts SQL aplicados manualmente.
3. **Probar localmente ANTES de mergear**:
   * Levantar la BD local (`make up` o `docker compose up zoo_sanmarino_db`).
   * Ejecutar `dotnet ef database update` y verificar que aplica sin error.
   * Esto detecta conflictos antes de que lleguen al deploy de prod.
4. **El deploy aplica la migración automáticamente** — al arrancar, EF compara el historial y ejecuta solo lo pendiente. No hace falta tocar `__EFMigrationsHistory` manualmente.

### Reglas críticas (NO violar)

* **NUNCA insertar registros en `__EFMigrationsHistory` "a la ligera"**. Solo registrá una migración como aplicada si confirmaste que su trabajo (columnas, tablas, índices) está efectivamente en la BD. La causa principal de los problemas históricos del proyecto fue [marcar_todas_migraciones_pendientes.sql](backend/sql/marcar_todas_migraciones_pendientes.sql), que marcó como aplicadas migraciones que nunca se ejecutaron → cuando el código nuevo dependió de ese estado, la app crasheó con SIGSEGV en cada arranque.
* **NO usar `dotnet ef database update` directamente contra RDS prod desde tu máquina** — usá el flujo de deploy (ECS aplica las migraciones al arrancar la app).
* **Si una migración nueva genera error al desplegar**, EF la marca como fallida pero deja `__EFMigrationsHistory` inconsistente. Hay que: (a) corregir el código del Up(), (b) limpiar manualmente el estado parcial, (c) re-intentar el deploy. NO insertar el registro a mano para "saltearla".

### Scripts SQL en `/backend/sql/` (caso especial)

Mantener scripts SQL crudos solo para operaciones que EF Core no maneja bien:
* Funciones almacenadas, triggers, vistas materializadas (ej: `fn_seguimiento_diario_engorde.sql`).
* Backfills/migraciones de datos masivos donde DDL+DML mezclados son más claros como SQL puro.
* Seeds de catálogos.

Para columnas / tablas / índices / constraints simples: **preferí la migración EF idempotente**, no el script SQL manual.

### Local Connection Configuration

Toda configuración de BD local debe leer desde:
* `/Users/chelsycardona/Desktop/App_SanMarino/backend/src/ZooSanMarino.API/appsettings.Development.json`

---

## 🔍 SCHEMA AUDIT RULE — EL CÓDIGO ES LA FUENTE DE VERDAD

Cuando detectes una desalineación entre el código y el schema de la BD (local o prod), **el código actual del backend manda**, NO el historial de migraciones ni planes anteriores.

### Aplicar siempre estos filtros antes de proponer un cambio de schema

1. **Prioridad absoluta del código actual**:
   * Validá lo que las entidades (`/backend/src/ZooSanMarino.Domain/Entities/`) y sus configuraciones (`/backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/*.ToTable(...)`) esperan **HOY**.
   * Si una migración planeada renombró tabla `X` → `Y`, pero la entidad sigue mapeando a `X`, ese rename fue descartado: **NO aplicar en BD**.
   * Si el código no toca un cambio, NO modificar el código para forzar un plan viejo. El código manda.

2. **Auditoría de historial (Git + migraciones + SQL del último mes)**:
   * Revisar commits de Git recientes, archivos `.cs` de migraciones y scripts `.sql` para distinguir qué se consolidó vs qué quedó huérfano.
   * Cruzar la información con las entidades actuales antes de generar cualquier ALTER/CREATE.

3. **Diagnóstico de desalineación**:
   * Si código espera `Y` y prod tiene `X` → generar SQL/migración para llevar prod a `Y`.
   * Si código sigue usando `X` (aunque exista una migración pendiente que iba a llevarlo a `Y`) → BD se queda en `X`, descartar la migración hacia `Y` (eliminarla del repo o marcarla como aplicada sin ejecutar si ya está referenciada en otros lugares).

4. **NO proceder sin confirmación**: antes de ejecutar cualquier DDL contra prod, presentá el plan al usuario con la auditoría visible. Esperá aprobación explícita.

---

## 🚀 CI/CD & DEPLOYMENT GOTCHAS

Lecciones grabadas de incidentes pasados — leer antes de tocar el workflow `/Users/chelsycardona/Desktop/App_SanMarino/.github/workflows/deploy-production.yml` o de ejecutar un deploy manual.

### Reglas del workflow

* **NUNCA volver a meter `dorny/paths-filter`** en el workflow de deploy. Comparaba `main...main-produccion` y tras un merge ambas ramas quedan en el mismo SHA → `diff = 0` → jobs de deploy saltados silenciosamente. Los pushes a `main-produccion` deben disparar backend y frontend siempre.
* **Trigger debe ser `push` a `main-produccion`**, no `pull_request: closed`. El subject claim OIDC de `pull_request` (`repo:.../pull_request`) no coincide con la trust policy del rol IAM `github-actions-deploy`. Documentado en el commit `fed6120`.
* **`wait-for-minutes` en ECS deploy: 25 minutos mínimo**. Con 15 minutos el deploy reportaba "failed" prematuramente y ECS hacía rollback aunque la app eventualmente arrancaba bien.

### Verificación post-deploy (obligatoria)

Cuando se hace un deploy (vía GitHub Actions o `make deploy-backend`), **NUNCA asumir éxito por el output del CLI**. Ejecutar siempre:

```bash
# 1) ¿Qué TaskDef está realmente corriendo?
aws ecs describe-services --cluster devSanmarinoZoo \
  --services sanmarino-back-task-service-75khncfa \
  --region us-east-2 \
  --query 'services[0].{TaskDef:taskDefinition,Running:runningCount,Deployments:deployments[].{Status:status,Rollout:rolloutState,TaskDef:taskDefinition}}'

# 2) ¿Qué imagen tiene esa TaskDef?
aws ecs describe-task-definition --task-definition <arn-de-arriba> \
  --region us-east-2 --query 'taskDefinition.containerDefinitions[0].image'

# 3) Comparar contra la imagen que pretendías desplegar
```

**Por qué importa**: ECS hace rollback silencioso. Si la nueva tarea no pasa el health check 3 veces (típicamente exit code 139/SIGSEGV o `EssentialContainerExited`), ECS marca el deployment como `failed: tasks failed to start` y revierte a la TaskDef anterior. El output de `aws ecs update-service` y el de `make deploy-backend` no reflejan este rollback — dicen "completado" porque la versión vieja sigue corriendo. El `Waiter ServicesStable failed: Max attempts exceeded` es la señal.

### Si una tarea ECS crashea al arrancar

* Exit code 139 = SIGSEGV. Usualmente significa que EF Core intentó correr una migración que falla (tabla inexistente, columna duplicada, FK rota) y el proceso muere antes del primer log de aplicación.
* Verificá `__EFMigrationsHistory` en RDS y crucealo con las migraciones del código antes de re-deployar.
* Si no tenés permisos para CloudWatch logs, los events del servicio (`aws ecs describe-services ... events`) muestran el patrón task started → registered → deregistered en pocos segundos.

---

## 🧪 TESTING, VALIDATION & SERVICE LIFECYCLE DISCIPLINE

You must always guarantee code quality through immediate backend validation, frontend contract alignment, and clean process handling:

### 1. API & Integration Validation
* **Backend Validation:** As soon as an API endpoint, controller, or application command/query handler is created or modified, you **MUST** perform integration tests or execute requests against the API to validate inputs, business constraints, outputs, and status codes.
* **Frontend Validation:** Before attempting to send or map data from Angular, perform explicit validation of the data payload structure against the API contracts to prevent transmission mismatches.

### 2. Service Lifecycle (No Orphan Processes)
* When spinning up any local service, test runner, mock server, database docker container, or compiler to validate code, **you must explicitly terminate, kill, or stop the service (`make down` or appropriate termination command) as soon as you finish using it.**
* Do **NOT** leave active, hanging background services or container processes alive on the host machine when tests/tasks are completed.

### 3. Dedicated Testing Folders
If a feature requires new unit, integration, or structural tests, isolate them inside the designated testing directories:
* **Backend Tests Location:** `/Users/chelsycardona/Desktop/App_SanMarino/backend/tests/` (or structured project-level equivalent).
* **Frontend Tests Location:** `/Users/chelsycardona/Desktop/App_SanMarino/frontend/src/tests/`.

---

## 🛠️ Project Commands Reference

### Local Development & Lifecycle
```bash
# Start everything (Docker PostgreSQL DB + backend + Angular dev server)
make up

# Backend only (from /backend/src/ZooSanMarino.API)
dotnet run --launch-profile "Development"    # runs on port 5002

# Frontend only (from /frontend)
yarn start                                    # ng serve on localhost:4200
yarn start:hmr                                # with hot module replacement

# STOP ALL SERVICES IMMEDIATELY AFTER TESTING (Avoid living background processes)
make down
Build & Deployments
Bash
# Full production build
make build-angular                            # builds Angular to dist/

# Frontend (from /frontend)
yarn build                                    # production build

# Backend (from /backend)
dotnet build
dotnet publish -c Release

# AWS Deployment
make deploy-backend                           # build & push backend to ECS
make deploy-frontend                          # build & push frontend to ECS
make deploy-all                               # both
Testing & Temporary Migrations Setup
Bash
# Frontend unit tests (from /frontend)
yarn test                                     # Karma runner

# Backend tests (from /backend)
dotnet test

# Database Migrations alignment (Run from /backend/src/ZooSanMarino.API/)
dotnet ef migrations add <MigrationName> --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext
dotnet ef migrations list
🏛️ Architecture Blueprint
Backend (.NET 9 — /backend/src/)
Clean Architecture with four layers:

ZooSanMarino.API: Startup project, REST controllers (60+), JWT auth configuration, Middleware pipeline, Swagger, and DI wiring via Program.cs.

ZooSanMarino.Application: DTOs, CQRS Command/Query handlers, service interfaces, and business validation using FluentValidation.

ZooSanMarino.Infrastructure: EF Core 9 + Npgsql, ZooSanMarinoContext, repository implementations, email, and Excel generation (ClosedXML/EPPlus). Custom raw SQL scripts live in /backend/sql/.

ZooSanMarino.Domain: Pure entity models, Enums, and core domain domain logic with no external dependencies.

Note: ORM uses snake_case naming via EFCore.NamingConventions.

Frontend (Angular 20 — /frontend/src/)
Modern Angular Standalone architecture (No NgModules):

app/core/: Authentication state, JWT interceptor, token storage, encryption utilities, and active-company context management.

app/features/: 33+ feature modules handling poultry management metrics (farms, batches/lotes, inventory, daily tracking, transfers, etc.).

app/shared/: Reusable UI components, layout elements, and directives.

app/services/: Domain-specific HTTP communication services.

app/app.config.ts: Main application configuration routing, client setup, and interceptor registration.

Database, Styling & Infrastructure
Production Environment: AWS RDS PostgreSQL (us-east-1), ECS Clusters (devSanmarinoZoo), ECR, ALB, CloudFront, and S3.

Local Dev Environment: Docker container zoo_sanmarino_db on port 5432, DB zoo_sanmarino_dev, credentials postgres/postgres.

Security: JWT Bearer tokens (60-min expiry) attached automatically via AuthInterceptor. Storage data uses EncryptionService (crypto-js, AES).

Styling: Tailwind CSS 3 using the Italfoods corporate brand palette:

ital-orange (#e85c25) — Accent

ital-green (#2d7a3e) — Primary actions

ital-cream (#faf8f5) — Main Background