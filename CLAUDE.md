Markdown# CLAUDE.md

This file provides strict guidance and context to Claude Code (claude.ai/code) when working with code in this repository. 

## ⚠️ CRITICAL DEVELOPMENT WORKFLOW (MUST FOLLOW)

To prevent hallucinations, ensure architectural integrity, and maintain a clear context, you **MUST** follow this strict 2-step process for EVERY new requirement, feature, or bug fix BEFORE writing any functional code:

### STEP 1: Full Planning (`/fase_de_desarrollo/`)
1. Analyze the user's request thoroughly based on the existing Clean Architecture (Backend) and Angular Standalone architecture (Frontend).
2. Create a detailed Markdown planning document inside the `./fase_de_desarrollo/` directory (e.g., `./fase_de_desarrollo/feature_name_plan.md`). Absolute path reference: `C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino\fase_de_desarrollo\`.
3. This document must contain:
   - Architectural design and technical approach.
   - Specific files, components, and services to create or modify.
   - Database migrations or schema changes needed.
   - Business logic constraints.

### STEP 2: State Tracking (`tracker_estado.md`)
1. Open the `./tracker_estado.md` file (Absolute path: `C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino\tracker_estado.md`).
2. **CLEAR / WIPE** all its previous contents (remove the state of the previous task).
3. Add a clear title and a reference/link to the new planning document created in Step 1.
4. Break down the development plan into a **granular, step-by-step checklist** of small implementation tasks using Markdown checkboxes (`- [ ]`).
5. As you implement the code, continually update this file by checking off completed items (`- [x]`) to maintain a single source of truth for the development state.

---

## Project Overview

San Marino App is a full-stack poultry farm management system ("gestión avícola") for tracking farms, pens, batches, inventory, and production across broiler (engorde), layer (reproductoras), and growing-bird (levante) operations. Multi-company support is a core feature.

## Commands

### Local Development

```bash
# Start everything (Docker PostgreSQL + backend + Angular dev server)
make up

# Frontend only (from /frontend)
yarn start          # ng serve on localhost:4200
yarn start:hmr      # with hot module replacement

# Backend only (from /backend/src/ZooSanMarino.API)
dotnet run          # runs on port 5002

# Stop all containers
make down

# View logs
make logs
BuildingBash# Full production build
make build-angular  # builds Angular to dist/

# Frontend (from /frontend)
yarn build          # production build
yarn build:prod     # alias

# Backend (from /backend)
dotnet build
dotnet publish -c Release
Testing & MigrationsBash# Frontend unit tests (from /frontend)
yarn test           # Karma runner

# Backend tests (from /backend)
dotnet test

# Database Migrations (Run from /backend/src/ZooSanMarino.API/)
dotnet ef migrations add <MigrationName> --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext
dotnet ef database update
dotnet ef migrations list
AWS DeploymentBashmake deploy-backend   # build & push backend to ECS
make deploy-frontend  # build & push frontend to ECS
make deploy-all       # both
ArchitectureBackend (.NET 9 — /backend/src/)Clean Architecture with four layers:ZooSanMarino.API — Startup project (port 5002). 60+ REST controllers, JWT auth, Swagger, CORS. Program.cs wires all DI. Entry point for all HTTP traffic.ZooSanMarino.Application — DTOs, service interfaces, business validation (FluentValidation).ZooSanMarino.Infrastructure — EF Core 9 + Npgsql, ZooSanMarinoContext, repository implementations, email, Excel (ClosedXML/EPPlus) services.ZooSanMarino.Domain — Pure entity models and value objects with no external dependencies.Notes: ORM uses snake_case naming via EFCore.NamingConventions. Custom SQL scripts for data migrations live in /backend/sql/.Frontend (Angular 20 — /frontend/src/)Standalone components (no NgModules). Key structure:app/core/ — Auth service, JWT interceptor, encryption, token storage, active-company context.app/features/ — 33+ feature modules (auth, dashboard, farms, batches, inventory, reports, bird/egg transfers, configuration, db-studio, maps, etc.).app/shared/ — Reusable components and utilities.app/services/ — Domain HTTP services.app/app.config.ts — Routes, HTTP client, interceptor registration (the main app configuration file; app.routes.ts is legacy/unused).Database & AuthProduction: AWS RDS PostgreSQL (us-east-1).Local dev: Docker container zoo_sanmarino_db on port 5432, DB zoo_sanmarino_dev, creds postgres/postgres.Auth: JWT Bearer tokens (60-min expiry). The AuthInterceptor automatically attaches tokens. Data at rest uses EncryptionService (crypto-js, AES). reCAPTCHA enabled in prod.StylingTailwind CSS 3 with Italfoods brand palette defined in tailwind.config.js:ital-orange (#e85c25) — accentital-green (#2d7a3e) — primary actionital-cream (#faf8f5) — backgroundInfrastructureAWS: ECS (cluster devSanmarinoZoo), ECR, ALB, CloudFront, S3 (frontend static).Docker: Multi-stage builds for both backend and frontend. Backend runs as non-root appuser, health-checked at /health.Key Configuration FilesFilePurposebackend/src/ZooSanMarino.API/Program.csDI setup, middleware pipelinebackend/src/ZooSanMarino.API/appsettings.Development.jsonLocal dev secrets (DB, JWT, SMTP, keys)frontend/src/app/app.config.tsRoutes, HTTP client, interceptorsfrontend/src/environments/environment.tsAPI base URL per environmentfrontend/tailwind.config.jsBrand colorsMakefileAll top-level dev/deploy shortcuts