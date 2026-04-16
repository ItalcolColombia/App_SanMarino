# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
```

### Building

```bash
# Full production build
make build-angular  # builds Angular to dist/

# Frontend (from /frontend)
yarn build          # production build
yarn build:prod     # alias

# Backend (from /backend)
dotnet build
dotnet publish -c Release
```

### Testing

```bash
# Frontend unit tests (from /frontend)
yarn test           # Karma runner

# Backend tests (from /backend)
dotnet test
```

### Database Migrations

Run from `/backend/src/ZooSanMarino.API/`:

```bash
# Add migration
dotnet ef migrations add <MigrationName> \
  --project ../ZooSanMarino.Infrastructure \
  --startup-project . \
  --context ZooSanMarinoContext

# Apply migrations
dotnet ef database update

# List migrations
dotnet ef migrations list
```

### AWS Deployment

```bash
make deploy-backend   # build & push backend to ECS
make deploy-frontend  # build & push frontend to ECS
make deploy-all       # both
```

## Architecture

### Backend (.NET 9 — `/backend/src/`)

Clean Architecture with four layers:

- **ZooSanMarino.API** — Startup project (port 5002). 60+ REST controllers, JWT auth, Swagger, CORS. `Program.cs` wires all DI. Entry point for all HTTP traffic.
- **ZooSanMarino.Application** — DTOs, service interfaces, business validation (FluentValidation).
- **ZooSanMarino.Infrastructure** — EF Core 9 + Npgsql, `ZooSanMarinoContext`, repository implementations, email, Excel (ClosedXML/EPPlus) services.
- **ZooSanMarino.Domain** — Pure entity models and value objects with no external dependencies.

ORM uses **snake_case naming** via `EFCore.NamingConventions`. Custom SQL scripts for data migrations live in `/backend/sql/`.

### Frontend (Angular 20 — `/frontend/src/`)

Standalone components (no NgModules). Key structure:

- `app/core/` — Auth service, JWT interceptor, encryption, token storage, active-company context.
- `app/features/` — 33+ feature modules (auth, dashboard, farms, batches, inventory, reports, bird/egg transfers, configuration, db-studio, maps, etc.).
- `app/shared/` — Reusable components and utilities.
- `app/services/` — Domain HTTP services.
- `app/app.config.ts` — Routes, HTTP client, interceptor registration (the main app configuration file; `app.routes.ts` is legacy/unused).

### Database

- **Production:** AWS RDS PostgreSQL (us-east-1).
- **Local dev:** Docker container `zoo_sanmarino_db` on port 5432, DB `zoo_sanmarino_dev`, creds `postgres/postgres`.

### Auth

JWT Bearer tokens with 60-minute expiry. The `AuthInterceptor` automatically attaches tokens to all outgoing API requests. Data at rest uses `EncryptionService` (crypto-js, AES). reCAPTCHA is enabled in production.

### Styling

Tailwind CSS 3 with Italfoods brand palette defined in `tailwind.config.js`:
- `ital-orange` (#e85c25) — accent
- `ital-green` (#2d7a3e) — primary action
- `ital-cream` (#faf8f5) — background

### Infrastructure

- **AWS:** ECS (cluster `devSanmarinoZoo`), ECR, ALB, CloudFront, S3 (frontend static).
- **Docker:** Multi-stage builds for both backend and frontend. Backend runs as non-root `appuser`, health-checked at `/health`.
- `docker-compose.yml` at repo root provides only the local PostgreSQL service; backend and frontend run natively in dev.

## Key Configuration Files

| File | Purpose |
|------|---------|
| `backend/src/ZooSanMarino.API/Program.cs` | DI setup, middleware pipeline |
| `backend/src/ZooSanMarino.API/appsettings.Development.json` | Local dev secrets (DB, JWT, SMTP, keys) |
| `frontend/src/app/app.config.ts` | Routes, HTTP client, interceptors |
| `frontend/src/environments/environment.ts` | API base URL per environment |
| `frontend/tailwind.config.js` | Brand colors |
| `Makefile` | All top-level dev/deploy shortcuts |
