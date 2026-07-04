# Tracker — Upgrade Angular 20 → 22

> Plan: [`fase_de_desarrollo/upgrade_angular_20_a_22_plan.md`](fase_de_desarrollo/upgrade_angular_20_a_22_plan.md)
> Sesión previa commiteada (6495db7 back · 52bf914 front · 1b9a1bf docs) → upgrade aislado.

## Salto 1 — Angular 20 → 21 ✅ COMPLETO (commit 430e4d0)
- [x] `ng update` core/cli/cdk@21 (+ TS 5.9) → migración control-flow `*ngIf/*ngFor`→`@if/@for`
- [x] Fixes: fontawesome `[spin]`→`[animation]`; trackBy 1-arg→`track $index` (13); Uint8Array→BufferSource
- [x] Peer-deps: fontawesome 0.14→4; ng-recaptcha (abandonado)→ng-recaptcha-2@21
- [x] `yarn build` 0 err (19 warnings NG8107 cosméticos pendientes)

## Salto 2 — Angular 21 → 22 ✅ COMPLETO (commit b704757) — vía Node PORTABLE
- **Solución al bloqueo de Node (sin admin/IT):** Node 22.23.1 **portable** descomprimido en `C:\Users\SAN MARINO\node-portable\node-v22.23.1-win-x64\` (el Node del sistema 22.15 queda intacto). Cumple Angular 22 (≥22.22.3).
- `yarn add fontawesome@5.1 + ng-recaptcha-2@22`; `ng update` core/cli/cdk@22 (con node portable) → **TypeScript 6**
- Migración automática 22: `changeDetection: ChangeDetectionStrategy.Eager` (preserva comportamiento)
- `yarn build` **0 errores** (node portable)
- **Para el dev server Angular 22:** config `frontend-node22` en `.claude/launch.json` (usa el node portable). Para builds/serve manuales: `export PATH="/c/Users/SAN MARINO/node-portable/node-v22.23.1-win-x64:$PATH"` antes de `yarn`.
- ⚠️ Nota: hasta que IT instale Node ≥22.22.3 a nivel sistema, hay que usar el node portable para build/serve. (`OpenJS.NodeJS.22`=22.23.1 vía winget, o instalador oficial.)

## Dependencias / librerías (peer-deps al día para Angular 22)
- [x] `@fortawesome/angular-fontawesome` `0.14`→`4` (21) → luego `5.1` (22)
- [x] `ng-recaptcha` (abandonado en Angular 17) → **`ng-recaptcha-2`** `@21` → luego `@22` (+ import en login)
- [ ] Auditar resto de deps desactualizadas/deprecadas (`yarn outdated`) y subir las seguras
- [ ] `ng-recaptcha-2`/otros → versión Angular 22 en el salto 2

## Node
- [ ] Verificar Node vs requerimiento de Angular 22 (^20.19 || ^22.12 || ^24). Actual 22.15 ✓; evaluar LTS más nuevo (24) + pin en `package.json engines` / `.nvmrc`

## NG8107 ✅ RESUELTO (0 warnings)
- Angular 22 **ya no emite** NG8107 → 0 warnings en build 22 (no requirió fix manual). Verificado.

## Migración build-system ✅ COMPLETO (commit 44edc13)
- `angular.json`: `@angular-devkit/build-angular:*` → `@angular/build:*` (application/dev-server/extract-i18n/karma)
- Eliminado target `server` (SSR muerto: sin main.server.ts/@angular/ssr; Docker = SPA nginx) + `tsconfig.server.json`
- Removida dep huérfana `@angular-devkit/build-angular` (webpack). Dev server ahora sobre **vite**.
- `yarn build` 0 errores/0 deprecaciones; login validado en runtime (vite), consola limpia.

## Backend .NET 9 → 10 (LTS) ✅ COMPLETO (commit ad26c95)
- **SDK 10 portable** (sin admin) en `C:\Users\SAN MARINO\dotnet-portable\` (SDK 10.0.301). Sistema sigue con 9.0.301/8.0.408.
- `net9.0`→`net10.0` (6 proyectos) + paquetes 9.x→10.x (EF Core 10.0.9, Npgsql 10.0.2, NamingConventions 10.0.1)
- Swashbuckle 8.1.2→10.2.3; fixes breaking **Microsoft.OpenApi v2**: namespace `.Models`→raíz; `Type` string→`JsonSchemaType`; security por `OpenApiSecuritySchemeReference` + `AddSecurityRequirement` con factory; `OpenApiJsonWriter`
- SYSLIB0060: `Rfc2898DeriveBytes` ctor → `Pbkdf2` (hash idéntico) · removido `Microsoft.Extensions.Configuration.Json`
- **`dotnet build` 0/0 · `dotnet test` 122 verdes · app arranca (EF 10 migraciones OK) · E2E login Angular22+.NET10 → /home**
- Build/run con SDK portable: `export PATH="/c/Users/SAN MARINO/dotnet-portable:$PATH"`. IT: instalar .NET 10 SDK a nivel sistema.

## Validación final (flujo completo)
- [ ] `yarn build` 0 err + `yarn test`
- [ ] preview: login → home → módulos clave (inventario, lotes, seguimientos, reportes) sin errores de consola · rebrand intacto · funcionalidad preservada
- [ ] arreglar cualquier error/deprecación que aparezca en el camino

## Evidencia
- Estado inicial: Angular 20.3.x, TS 5.8, RxJS 7.8, zone.js 0.15, Node 22.15.
