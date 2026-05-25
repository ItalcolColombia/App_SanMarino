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

## 🗄️ DATABASE & MIGRATION WORKFLOW (LOCAL DEV)

> 🚨 **CRITICAL CONTEXT:** The Entity Framework Core migrations mechanism is currently broken/corrupted. Do **NOT** rely solely on `dotnet ef database update` for local sync. Follow this strict protocol to maintain persistence and alignment without errors:

1. **Local Connection Configuration:** All local development environment database configurations must read from and point to:
   * `/Users/chelsycardona/Desktop/App_SanMarino/backend/src/ZooSanMarino.API/appsettings.Development.json`
2. **SQL Script Generation:** Every time a schema change (table creation, new column, modified constraint, or seed data) is required:
   * Write the raw, pure SQL script executing the change.
   * Save the `.sql` script inside the folder: `/Users/chelsycardona/Desktop/App_SanMarino/backend/sql/`
   * Use a clear numbering/naming convention for execution order (e.g., `045_add_traslado_fields_to_seguimiento.sql`).
3. **Manual Execution:** Connect to your local database engine and execute the raw SQL script directly to test and update your local schema.
4. **Future-Proofing Migrations:** Even though the engine is currently broken, you **MUST** create the equivalent Entity Framework Core migration (`dotnet ef migrations add <Name>`) right after saving the SQL script. This ensures that once migrations are repaired, the C# codebase and snapshot remain aligned with the database schema for future migrations.

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