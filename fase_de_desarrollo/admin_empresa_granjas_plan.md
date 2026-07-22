# Plan — Administrador de Empresa: visibilidad global de granjas en asignación de usuarios

> Requerimiento: agregar un flag de **"Es Administrador de Empresa/País"** en el módulo de Roles (visible solo para Super Admin) que otorga a los usuarios con ese rol la posibilidad de ver **el 100% de las granjas activas y no eliminadas de su empresa** al momento de **asignar granjas a otros usuarios**, sin importar si esas granjas están asignadas a su propio perfil.

## Decisiones confirmadas por el usuario (2026-07-21)
- **Alcance de país:** *solo empresa, ignorar país.* → NO se agrega columna de país al rol; el filtro de granjas scopa por `company_id` + `status='A'` + `deleted_at IS NULL`. Sin filtro por país.
- **Dónde aplica la visibilidad global:** *solo en "Asignar Granjas".* → Endpoint dedicado nuevo consumido únicamente por el modal de asignación. NO se modifica el endpoint general `GET /api/Farm` ni ningún otro listado.

## Reconciliación requerimiento ↔ esquema real
| Requerimiento | Realidad en BD (código manda) |
|---|---|
| `status = active` | `farms.status = 'A'` (char(1); 'A'/'I'). No existe "active"/"Activa". |
| `is_deleted = false` | `farms.deleted_at IS NULL` (soft-delete por timestamp; NO existe columna `is_deleted`). |
| `empresa_id` | `farms.company_id`. |
| País | Derivado por `departamentos.pais_id` (NO se usa en este feature por decisión del usuario). |
| `Es_Admin = TRUE` | Nuevo booleano `roles.is_company_admin` (default false). |

## Enfoque arquitectónico
1. **Rol** gana un booleano `is_company_admin`. Solo un **Super Admin** (email hardcodeado `moiesbbuga@gmail.com`, ya existente en backend) puede activarlo/desactivarlo. Guarda autoritativa en el **backend**; la UI solo lo muestra a Super Admin.
2. **Detección en runtime:** el usuario logueado es "admin de empresa" si tiene algún `user_roles` para la **empresa activa** cuyo `role.is_company_admin = true`.
3. **Asignar Granjas:** endpoint dedicado `GET /api/Farm/assignable`. Si el usuario actual es admin de empresa (flag) **o** super admin → devuelve **todas** las granjas de la empresa activa con `status='A'` y `deleted_at IS NULL`, **omitiendo** el filtro de `user_farms`. Si no → comportamiento actual (granjas asignadas al usuario actual dentro de su empresa). Filtrado 100% en la consulta (BD filtra, backend orquesta).
4. **Super Admin al front:** se agrega `isSuperAdmin` (bool) al `AuthResponseDto` / sesión, calculado con `IsSuperAdminAsync`, para gatear el checkbox sin hardcodear el email en el bundle JS.

> **Nota "granjas y galpones":** el modal de asignación es a **nivel de granja**; los galpones se heredan al asignar la granja (no hay selector de galpón en ese modal). No se agrega selector de galpón.

---

## Backend — archivos a crear/modificar

### Dominio + EF
- `Domain/Entities/Role.cs` → agregar `public bool IsCompanyAdmin { get; set; }`.
- `Infrastructure/Persistence/Configurations/RoleConfiguration.cs` → mapear `is_company_admin` con `HasDefaultValue(false)`.
- **Migración** `AddIsCompanyAdminToRole` (idempotente): `Up()` → `ALTER TABLE roles ADD COLUMN IF NOT EXISTS is_company_admin boolean NOT NULL DEFAULT false;`. `Down()` → drop `IF EXISTS`.

### DTOs
- `Application/DTOs/RoleDto.cs`:
  - `RoleDto` → + `bool IsCompanyAdmin`.
  - `CreateRoleDto` → + `bool IsCompanyAdmin = false` (default en record posicional).
  - `UpdateRoleDto` → + `bool? IsCompanyAdmin = null` (null = no tocar).

### Cálculo puro + tests
- `Application/Calculos/RoleAdminCalculos.cs` (static):
  - `ResolverIsCompanyAdminEnCreacion(bool esSuperAdmin, bool solicitado) => esSuperAdmin && solicitado;`
  - `ResolverIsCompanyAdminEnEdicion(bool esSuperAdmin, bool? solicitado, bool actual) => (esSuperAdmin && solicitado.HasValue) ? solicitado.Value : actual;`
  - `PuedeVerTodasLasGranjas(bool esCompanyAdmin, bool esSuperAdmin) => esCompanyAdmin || esSuperAdmin;`
- `tests/ZooSanMarino.Application.Tests/RoleAdminCalculosTests.cs` (xUnit `[Theory]`): super-admin sí setea; no-super-admin nunca cambia el flag; global = flag OR super.

### Servicios
- `Infrastructure/Services/RoleCompositeService.cs`:
  - Proyección de lectura (`Roles_GetByIdAsync`/`GetAll`) → incluir `IsCompanyAdmin`.
  - `Roles_CreateAsync` → setear flag con `RoleAdminCalculos.ResolverIsCompanyAdminEnCreacion(esSuperAdmin, dto.IsCompanyAdmin)`.
  - `Roles_UpdateAsync` → `ResolverIsCompanyAdminEnEdicion(esSuperAdmin, dto.IsCompanyAdmin, actual)`.
  - Necesita `IsCurrentUserSuperAdminAsync()` → reusar `IUserPermissionService.IsSuperAdminAsync` (inyectar) o helper por `UserLogins → Login.email`.
- `Infrastructure/Services/FarmService.cs` (+ interfaz `IFarmService`):
  - `Task<bool> IsCurrentUserCompanyAdminAsync()` → `UserRoles.Any(ur => ur.UserId == UserGuid && ur.CompanyId == effectiveCompanyId && ur.Role.IsCompanyAdmin)`.
  - `Task<IEnumerable<FarmDto>> GetAssignableFarmsAsync()` → global (todas de la empresa, `status='A'`, `deleted_at IS NULL`) si `PuedeVerTodasLasGranjas(...)`; si no, granjas del usuario actual (comportamiento actual) filtradas por empresa + activas + no borradas. Reusa la proyección `FarmDto` existente.

### API
- `API/Controllers/FarmController.cs` → `GET api/Farm/assignable` (+ alias `GET /Farm/assignable` si aplica el patrón), `[Authorize]`, delega en `_svc.GetAssignableFarmsAsync()`.
- `Application/DTOs/AuthResponseDto.cs` → + `bool IsSuperAdmin`.
- `Infrastructure/Services/AuthService.cs` (`login`) → poblar `IsSuperAdmin` vía `IsSuperAdminAsync`.
- `API/Controllers/AuthController.cs` (`GET api/Auth/profile`) → + `isSuperAdmin` en el objeto de respuesta.

---

## Frontend — archivos a crear/modificar

### Auth / sesión
- `core/auth/auth.models.ts` → `AuthSession.user` + `isSuperAdmin: boolean`.
- `core/auth/auth.service.ts` (`login`) → mapear `isSuperAdmin` desde el login result.

### Roles
- `core/services/role/role.service.ts` (interfaces DTO) → `isCompanyAdmin?: boolean` en Create/Update/Role.
- `features/config/role-management/role-management.component.ts`:
  - FormGroup → `isCompanyAdmin: [false]`.
  - `isSuperAdminUser` desde `session.user.isSuperAdmin`.
  - `openModal(r)` → `patchValue({ isCompanyAdmin: r?.isCompanyAdmin ?? false })`.
  - `save()` → incluir `isCompanyAdmin` en `payloadBase` (create + update).
- `features/config/role-management/role-management.component.html`:
  - En paso "General", checkbox/switch **"Es Administrador de Empresa/País"** dentro de `@if (isSuperAdminUser)`.
  - (Opcional, solo si `companies` ya trae país) dropdown País como filtro visual de la lista de empresas — NO se persiste. Si no es trivial, se omite.

### Asignar Granjas
- `core/services/farm/farm.service.ts` → `getAssignableFarms()` → `GET /Farm/assignable`.
- `features/config/user-management/components/asignar-usuario-granja/asignar-usuario-granja.component.ts` → `loadData()` usa `getAssignableFarms()` en vez de `getAll()`. Se conserva el filtro cliente `status==='A'` (redundante, inofensivo) y el "menos las ya asignadas".

---

## Reglas de negocio
1. Solo Super Admin puede activar/desactivar `is_company_admin` (backend autoritativo; UI gateada).
2. Admin de empresa = tiene un `user_roles` para la **empresa activa** con `role.is_company_admin = true`.
3. En "Asignar Granjas", si es admin de empresa o super admin → todas las granjas de la empresa activa con `status='A'` y `deleted_at IS NULL`. Si no → solo las asignadas al usuario actual (sin regresión).
4. Refactor ≠ cambio de comportamiento: el endpoint general de granjas y todos los demás listados quedan intactos.

## Casos de prueba
- **Pure (xUnit):** creación/edición del flag según super-admin; `PuedeVerTodasLasGranjas`.
- **Backend build + dotnet test** verdes.
- **Front build** verde (solo warning de bundle budget preexistente).
- **Smoke local:** login super-admin → crear rol con flag → `GET /api/Farm/assignable` devuelve todas las granjas activas de la empresa; usuario no-admin → solo sus granjas. (Gotchas: login cifrado + `X-Secret-Up` + rate-limit 429; BD local :5433.)

## Validación / gotchas
- Migración **idempotente**; se prueba en BD local (`sanmarinoapplocal` :5433). BD local compartida entre worktrees → migración aditiva, sin riesgo.
- `dotnet ef` con el backend del usuario corriendo puede fallar → usar `DesignTimeDbContextFactory` de Infrastructure.
- **DDL en prod:** la migración es aditiva y se aplica sola en el deploy; NO se toca prod manualmente. Confirmar antes de desplegar.
