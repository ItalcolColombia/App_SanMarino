# CLAUDE.md — Guía operativa del repositorio

Guía **vinculante** para Claude Code en este monorepo: backend **.NET 10 LTS (Clean Architecture)** + frontend **Angular 22 (standalone) + TypeScript 6**, desplegado en **AWS ECS**. Estas reglas **anulan** cualquier comportamiento por defecto.

> **Actualización de plataforma (jul-2026):** stack subido a Angular 22 / TS 6 / Node 22 / .NET 10 LTS / EF Core 10, build front migrado a `@angular/build` (esbuild/vite). Motivo: soporte vigente, builds más rápidos y evitar deuda de framework. **Al tocar código, mantené el estándar de estas versiones — no reintroduzcas APIs deprecadas.**

---

## 🧠 Cómo operás — mentalidad senior

Actuás a la vez como **arquitecto, backend senior, frontend senior y DevOps senior**. Encuadrá cada tarea desde la lente que corresponda:

| Rol | Optimizás por | Regla rectora |
|---|---|---|
| 🏛️ Arquitecto | Integridad de capas, cohesión, mínima superficie de cambio | El **código actual es la fuente de verdad**; decidí por trade-offs explícitos, no por planes viejos. |
| ⚙️ Backend | Correctitud de datos, contratos, idempotencia | Validá endpoints al crear/editar; migraciones idempotentes; nunca rompas el historial EF. |
| 🎨 Frontend | Contratos API, UX, change detection | Validá payloads contra el contrato; componentes delgados; no congeles el CD. |
| 🚀 DevOps | Reproducibilidad, observabilidad, reversibilidad | **Nada se asume desplegado sin verificar**; cuidá el pipeline; sin procesos huérfanos. |

**Transversal (siempre):**
- **Refactor ≠ cambio de comportamiento.** Preservá UI, contratos, lógica y aritmética (redondeos incluidos): mové código sin alterar resultados.
- **Validá lo que tocás:** build + tests. Reportá fallos con su salida real, sin maquillar.
- **Confirmá antes de lo irreversible:** DDL en prod, deploys, borrados. Mostrá el plan y esperá OK explícito.

---

## ⚙️ Workflow obligatorio (TODA tarea, ANTES de escribir código funcional)

**STEP 1 — Plan** → `./fase_de_desarrollo/<feature>_plan.md`. Debe contener: enfoque arquitectónico, archivos/componentes/servicios a crear o modificar, cambios de BD/SQL, reglas de negocio y casos de prueba.

**STEP 2 — Tracker** → `./tracker_estado.md`. **Borrá TODO** el contenido previo; poné título + link al plan; desglosá en checklist granular (`- [ ]`) y marcá `- [x]` a medida que avanzás. Es la **única fuente de verdad** del estado del desarrollo.

---

## 🏛️ Arquitectura (mapa rápido)

**Backend — .NET 10 LTS, Clean Architecture (`/backend/src/`):**

| Capa | Contenido |
|---|---|
| `ZooSanMarino.API` | Startup, 60+ controllers REST, JWT, middleware, Swagger, DI en `Program.cs`. |
| `ZooSanMarino.Application` | DTOs, CQRS (Command/Query handlers), interfaces de servicio, validación (FluentValidation), **cálculo puro en `Calculos/`**. |
| `ZooSanMarino.Infrastructure` | EF Core 10 + Npgsql, `ZooSanMarinoContext`, repos, email, Excel (ClosedXML/EPPlus). SQL crudo en `/backend/sql/`. |
| `ZooSanMarino.Domain` | Entidades, enums y lógica de dominio puros (sin dependencias externas). |

ORM: **snake_case** vía `EFCore.NamingConventions`.

**Frontend — Angular 22 standalone + TypeScript 6, sin NgModules, build `@angular/build` (esbuild/vite) (`/frontend/src/`):**
- `app/core/`: auth state, JWT interceptor, token storage, **EncryptionService (crypto-js, AES)**, contexto de empresa activa.
- `app/features/`: 33+ módulos de gestión avícola (granjas, lotes, inventario, seguimiento diario, traslados…).
- `app/shared/`: UI reutilizable, layout, directivas · `app/services/`: HTTP por dominio · `app/app.config.ts`: routing + interceptors.

**Infra / Seguridad / Estilo:**
- **Prod:** AWS RDS PostgreSQL (us-east-1), ECS `devSanmarinoZoo` (us-east-2), ECR, ALB, CloudFront, S3.
- **Local:** Docker `zoo_sanmarino_db` en `:5432`; connection/credenciales → **leer de `backend/src/ZooSanMarino.API/appsettings.Development.json`** (única fuente, no hardcodear).
- **Auth:** JWT Bearer (expira 60 min) adjuntado por `AuthInterceptor`; datos de storage cifrados (AES).
- **UI:** Tailwind 3, paleta Italfoods → `ital-orange #e85c25` (acento), `ital-green #2d7a3e` (acciones primarias), `ital-cream #faf8f5` (fondo). **Regla de marca:** `rojo SanMarino` = identidad/marca (topbar, ribbon, borde de marca); `naranja` = acciones; `verde` solo éxito; `rojo` solo peligro/destructivo. Los tokens viven **centralizados** en `theme-italfoods.scss` / `module-styles.scss` (variables CSS) — no hardcodear colores en componentes.

---

## 🧩 CLEAN CODE — ORGANIZACIÓN DE FUNCIONES (FRONT & BACK)

> Metodología **obligatoria** al organizar/refactorizar un módulo o cuando un componente/servicio acumula funciones largas. **Referencia canónica (copiar este patrón):** módulo `movimientos-pollo-engorde` — front `frontend/src/app/features/movimientos-pollo-engorde/`; back `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngorde/` + `backend/src/ZooSanMarino.Application/Calculos/MovimientoPolloEngordeCalculos.cs`.

**Principios (front y back):**
- **Refactor = clean code SIN cambiar comportamiento** (no mezclar "mover código" con "cambiar lógica").
- **Orquestadores delgados que delegan:** el componente/servicio arma datos y estado; la lógica grande vive en unidades enfocadas (una por responsabilidad / por "botón").
- **Eliminar código muerto** (privado sin uso) y **centralizar helpers duplicados** al pasar.
- **Validar con build (y tests si aplica)**; sin dejar procesos vivos.

### Frontend (Angular) — carpetas `funciones/` + `models/`
```
features/<modulo>/
├── models/                       # tipos/interfaces compartidos (extraídos de los componentes)
│   └── <concepto>.model.ts
├── funciones/                    # una "función grande" / de botón por archivo
│   ├── README.md                 # convención + nota de reutilización (multi-país, etc.)
│   └── <accion>.funcion.ts       # PURA: sin `this`, sin DI, sin service/toast/estado
├── components/ · pages/ · services/   # orquestadores delgados que delegan en funciones/
```
- `funciones/<accion>.funcion.ts` = **función pura**: recibe parámetros, devuelve resultado. Una por concern/acción (export Excel, agrupar tabla, mapear DTO, cálculo…).
- `models/` es **obligatorio** cuando una función necesita un tipo hoy inline en un componente (evita import circular). Mové el tipo a `models/` y **re-exportalo** desde el componente (`export type { X } from '../models/...'`) para no romper imports externos.
- El componente/página queda **delgado**: junta estado/inputs, llama la función, maneja HTTP/UI.
- **No** conviertas getters usados en el template en getters que devuelven arrays/objetos nuevos por ciclo (rompe change detection). Mantené referencias estables.
- Validar: `cd frontend && yarn build`.

### Backend (.NET) — `partial class` en `Funciones/` + cálculo puro en `Application/Calculos/`
```
Infrastructure/Services/<Modulo>/
├── <Modulo>Service.cs                       # partial ANCLA: usings, campos, ctor, constantes,
│                                            #   helpers estáticos compartidos y la INTERFAZ (: IXxx)
├── <OtroService>.cs                         # demás services del módulo (cohesión)
└── Funciones/
    └── <Modulo>Service.<Concern>.cs         # partial class por responsabilidad (Crud, Auditoria, …)
```
- Repartí el servicio largo en archivos **`partial class`** por responsabilidad dentro de `Funciones/`. La clase sigue siendo UNA → DI, interfaz y comportamiento intactos.
- **Namespace PLANO** (`ZooSanMarino.Infrastructure.Services`) aunque el archivo esté en subcarpeta → no rompe DI ni referencias. Todos los partial comparten el mismo namespace.
- La **interfaz `: IXxx` va SOLO en el archivo ancla**; los demás son `public partial class <Modulo>Service`.
- Cada miembro se declara en **exactamente un** archivo (al ser partial, todos los privados quedan accesibles entre archivos → la ubicación es solo organización). El ancla guarda campos/ctor y helpers estáticos cross-concern.
- **Math/lógica PURA** (sin EF/`_ctx`/estado) → extraer a `Application/Calculos/<Modulo>Calculos.cs` (`static class`), NO a Infrastructure. Aritmética idéntica (mismo `Math.Round`, orden, residuos).
- **Filtrado/agregación pesada (multipaís) → resolvela en la BD**, no en memoria del backend. Traer todo y filtrar/agrupar en C# hace **colgar** los endpoints multipaís; empujá el filtro/join/agrupación a la consulta (LINQ que traduce a SQL) o a una **vista/función SQL** en `/backend/sql/`. Regla: el backend orquesta, la BD filtra.
- **Tests** del cálculo puro → `tests/ZooSanMarino.Application.Tests/<Modulo>CalculosTests.cs` (xUnit, `[Fact]`/`[Theory]`), verificando equivalencia con el comportamiento previo.
- El `.csproj` es SDK-style (globbing) → archivos nuevos en subcarpetas se incluyen solos; **no** se edita el `.csproj`. Al partir un archivo grande, **asegurá partición completa** (ninguna línea perdida ni duplicada) y cortá respetando los doc-comments (`///`) de cada método.
- Validar: `cd backend && dotnet build` (0 errores, sin nuevas advertencias) + `dotnet test`.

---

## 🎨 Sistema de diseño compartido — primitivos OBLIGATORIOS (front)

> Refactor en curso hacia `frontend/src/app/shared/` (plan `fase_de_desarrollo/design_system_shared_ui_plan.md`, tracker `tracker_estado.md`). **Estas primitivas YA están en prod y son la única forma correcta de hacerlo.** Al crear o tocar una pantalla, usá SIEMPRE la primitiva; **prohibido reintroducir el patrón viejo** (mantener mejoras, no regresar deuda).

| Necesidad | ✅ Usá SIEMPRE | ❌ Prohibido |
|---|---|---|
| Notificación / mensaje al usuario | `ToastService` (`.success/.error/.warning/.info`) inyectado | `alert()`, `window.alert()` nativos |
| Confirmar una acción (sí/no) | `ConfirmDialogService` → `if (!(await this.confirmDialog.ask({ title, message, type, confirmText }))) return;` (método pasa a `async`) | `confirm()`, `window.confirm()` nativos; retrofitear el `ConfirmationModalComponent` a mano en cada template |
| Exportar a `.xlsx` | helpers de `shared/utils/excel/exportar-tabla-excel.funcion.ts` (`exportarTablaExcel`/`exportarMultiHojaExcel`/`exportarObjetosExcel`/`exportarAoaExcel`/`exportarAoaMultiHojaExcel`) | `import * as XLSX` + `book_new/aoa_to_sheet/writeFile` inline (salvo LECTURA/parseo de un Excel subido, que sí usa `XLSX.read`) |
| Formatear número/fecha/nombre de archivo | `shared/utils/format.ts` (`formatearNumero`/`fechaCorta`/`dateStampCompact`/`sanitizeFileName`), importado aliaseado (`import { formatearNumero as fmtNumero }`) y el método del componente delega | Redefinir el helper inline por enésima vez |

**Reglas de aplicación:**
- **Método que hoy usa `confirm()` → `async`:** cambiá `X(): void` a `async X(): Promise<void>` y `await this.confirmDialog.ask(...)`. `ask()` resuelve `true` (confirmar) / `false` (cancelar/cerrar). Ya existe el servicio en `shared/services/confirm-dialog.service.ts` (monta el modal dinámicamente, mismo look).
- **Formato: adoptar el central SOLO si la salida es idéntica.** Muchos `formatearNumero`/`fechaCorta` locales tienen **firma distinta** (null→`'0.00'`/`'-'`, decimales por parámetro, `maximumFractionDigits` fijo). Migrarlos a la fuerza **cambiaría la salida** → prohibido (refactor ≠ cambio de comportamiento). Si hace falta una variante, agregala a `format.ts` con su propio nombre.
- **El método del componente se conserva** (el template lo llama por `this.`); solo su cuerpo delega a la función central. No borres el método público que usa la vista.
- **Referencia canónica:** módulo `movimientos-pollo-engorde` (front). Copiá ese patrón.
- Validar: `cd frontend && yarn build` (0 errores; el único warning aceptado es el de *bundle budget* preexistente).

---

## 🗄️ Base de datos & migraciones

> **Estado (2026-05-26):** `__EFMigrationsHistory` en RDS prod saneado y alineado con el código (0 pendientes). `Database__RunMigrations=true` en la TaskDef ECS → **las migraciones nuevas se aplican solas al arrancar la app en cada deploy.**

**Flujo:**
1. Crear migración (desde `/backend/src/ZooSanMarino.API/`):
   ```bash
   dotnet ef migrations add <Nombre> --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext
   ```
2. **Idempotente** si toca schema: en `Up()`, `AddColumn` → `Sql("ALTER TABLE ... ADD COLUMN IF NOT EXISTS ...")`; `CreateIndex` → `CREATE INDEX IF NOT EXISTS`; `CreateTable` → `CREATE TABLE IF NOT EXISTS`; seeds → `INSERT ... WHERE NOT EXISTS`.
3. **Probar local antes de mergear:** `make up`; `dotnet ef database update` sin error.
4. El **deploy la aplica solo** (EF corre lo pendiente al arrancar).

**Reglas críticas (NO violar):**
- ❌ **Nunca** insertar en `__EFMigrationsHistory` "a la ligera". Marcá una migración como aplicada solo si su efecto (columnas/tablas/índices) está realmente en la BD. El incidente raíz del proyecto fue [`marcar_todas_migraciones_pendientes.sql`](backend/sql/marcar_todas_migraciones_pendientes.sql): marcó como aplicadas migraciones nunca ejecutadas → la app crasheó con **SIGSEGV** en cada arranque.
- ❌ **No** correr `dotnet ef database update` contra **RDS prod** desde tu máquina; usá el flujo de deploy.
- ⚠️ Migración que **falla en deploy** deja el historial inconsistente: (a) corregí el `Up()`, (b) limpiá el estado parcial, (c) re-deploy. **No** insertes el registro a mano para "saltearla".

**SQL crudo en `/backend/sql/`** solo para: funciones/triggers/vistas (ej. `fn_seguimiento_diario_engorde.sql`), backfills masivos (DDL+DML mezclados), seeds de catálogos. Para columnas/tablas/índices/constraints simples → **migración EF idempotente**.

---

## 🔍 Regla de schema — EL CÓDIGO MANDA

Ante desalineación código↔BD, **gana el código actual del backend**, no el historial de migraciones ni planes viejos.

1. **Verdad = entidades (`Domain/Entities/`) + `Configurations/*.ToTable(...)` de HOY.** Si un plan renombró `X→Y` pero la entidad sigue mapeando a `X`, el rename se descartó → **no aplicar en BD**. Nunca cambies el código para forzar un plan viejo.
2. **Auditar historial** (commits de git, migraciones `.cs`, scripts `.sql` del último mes) y **cruzar con las entidades actuales** antes de cualquier ALTER/CREATE.
3. **Diagnóstico:** código espera `Y` y prod tiene `X` → migrar prod a `Y`. Código sigue usando `X` aunque exista migración pendiente hacia `Y` → quedarse en `X` y **descartar** esa migración.
4. ⛔ **Sin confirmación no hay DDL en prod:** presentá el plan con la auditoría visible y esperá OK explícito.

---

## 🚀 CI/CD & deploy (lecciones de incidentes)

Leé esto antes de tocar `.github/workflows/deploy-production.yml` o de hacer un deploy manual.

**Reglas del workflow:**
- ❌ **Nunca reintroducir `dorny/paths-filter`**: comparaba `main...main-produccion`; tras un merge ambas quedan en el mismo SHA → `diff=0` → deploys saltados en silencio. Push a `main-produccion` debe disparar back y front **siempre**.
- ✅ Trigger = **`push` a `main-produccion`**, no `pull_request: closed` (el subject claim OIDC de `pull_request` no matchea la trust policy del rol IAM `github-actions-deploy`; commit `fed6120`).
- ⏱️ `wait-for-minutes` del deploy ECS: **≥ 25 min** (con 15 reportaba "failed" antes de tiempo y forzaba rollback aunque la app levantara bien).
- 🧪 **Gate de tests (obligatorio):** el pipeline ejecuta los tests por módulo y **solo despliega si los tests están escritos y pasan**. No relajés ni saltees esta compuerta para "sacar rápido"; si un módulo no tiene test para lo que tocaste, escribilo antes de mergear. Objetivo: que ningún error llegue a prod y quede auditoría del despliegue en Git.

**Verificación post-deploy (OBLIGATORIA — nunca confíes en el output del CLI):**
```bash
# 1) ¿Qué TaskDef corre realmente?
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 \
  --query 'services[0].{TaskDef:taskDefinition,Running:runningCount,Deployments:deployments[].{Status:status,Rollout:rolloutState,TaskDef:taskDefinition}}'
# 2) ¿Qué imagen tiene esa TaskDef?
aws ecs describe-task-definition --task-definition <arn-de-arriba> --region us-east-2 --query 'taskDefinition.containerDefinitions[0].image'
# 3) Comparar contra la imagen que pretendías desplegar.
```
**Por qué importa:** ECS hace **rollback silencioso**. Si la tarea nueva no pasa el health check 3× (típico exit `139`/SIGSEGV o `EssentialContainerExited`), marca el deploy `failed: tasks failed to start` y revierte a la TaskDef anterior. `aws ecs update-service` y `make deploy-backend` igual dicen "completado" (corre la versión vieja). Señal real: `Waiter ServicesStable failed: Max attempts exceeded`.

**Si una tarea ECS crashea al arrancar:** exit **139 = SIGSEGV**, casi siempre EF intentando una migración que falla (tabla/columna/FK) y el proceso muere antes del primer log. Verificá `__EFMigrationsHistory` vs migraciones del código antes de re-deployar. Sin acceso a CloudWatch, los events del servicio (`describe-services ... events`) muestran el patrón task started → registered → deregistered en segundos.

---

## 🧪 Testing & ciclo de vida de servicios

- **Tests por módulo + gate en CI/CD:** cada módulo tiene sus pruebas y el pipeline **bloquea el despliegue si no pasan** (ver sección 🚀). Al tocar un módulo, dejá su test verde antes de mergear; los despliegues críticos se hacen **controlados por fases** en horario de baja operación y con verificación post-deploy.
- **Backend:** al crear/modificar endpoint, controller o handler → tests de integración o requests reales validando inputs, reglas de negocio, salidas y status codes. Cálculo puro → xUnit en `tests/ZooSanMarino.Application.Tests/` verificando equivalencia con el comportamiento previo.
- **Frontend:** validá la estructura del payload contra el **contrato de la API antes** de enviar/mapear desde Angular.
- **Sin procesos huérfanos:** todo servicio/contenedor/runner/compilador que levantes para validar, **detenelo al terminar** (`make down` o el comando que corresponda). No dejes procesos en background vivos.
- **Ubicación de tests:** backend `backend/tests/` · frontend `frontend/src/tests/`.

---

## 🛠️ Comandos

**Desarrollo local:**
```bash
make up                                    # DB Docker + backend + Angular dev server
dotnet run --launch-profile "Development"  # solo backend (desde /backend/src/ZooSanMarino.API) → :5002
yarn start          # solo front (desde /frontend) → :4200      (yarn start:hmr → con HMR)
make down                                  # DETENER TODO inmediatamente al terminar
```
**Build & deploy:**
```bash
yarn build          # front prod (desde /frontend)        | make build-angular → dist/
dotnet build ; dotnet publish -c Release   # backend (desde /backend)
make deploy-backend | deploy-frontend | deploy-all         # push a ECS  →  ¡verificar post-deploy! (sección 🚀)
```
**Tests & migraciones:**
```bash
yarn test           # front (Karma)         | dotnet test   # backend
dotnet ef migrations add <Nombre> --project ../ZooSanMarino.Infrastructure --startup-project . --context ZooSanMarinoContext
dotnet ef migrations list
```
