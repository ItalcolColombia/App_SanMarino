# Permisos por empresa (`company_permissions`) — plan

**Fecha:** 2026-08-11 · **Tracker:** [tracker_estado.md](../tracker_estado.md)

---

## 1. Problema

El catálogo de permisos es **global y plano**: 31 filas en `permissions`, sin ninguna noción de
empresa. `GET /api/Permission` ([RoleCompositeService.cs:502](../backend/src/ZooSanMarino.Infrastructure/Services/RoleCompositeService.cs))
devuelve las 31 a todo el mundo, así que al crear un rol de **ItalcolEcuador** se ofrecen permisos
que solo existen para Panamá o para Sanmarino Colombia:

| Permiso | Empresa real |
|---|---|
| `sincronizacion_panama.ver` / `.ejecutar` | Panamá |
| `lote_base_pollo_engorde.*` (4) | Ecuador / Panamá |
| `carga_masiva_postura` / `carga_masiva_pollo_engorde` | Sanmarino Colombia |
| `tickets.*`, `vacunacion.*`, `editar_registro`… | transversales |

Uso real hoy (unión de `role_companies` + `user_roles` → `role_permissions`):

```
Agroavicola Sanmarino 28 · Santa Reyes 31 · Demo 24 · ItalcolPanama 22 · ItalcolEcuador 18
```

Ya existe el eje gemelo para navegación (`company_menus`), pero **no tiene fuerza**: la visibilidad
real del sidebar sale de `role_menus`, y por eso `RestringirMigracionesMasivasASanmarino` tuvo que
limpiar las dos tablas a mano. Este plan no repite ese error: `company_permissions` **manda** en los
dos puntos donde el permiso se usa (asignación y runtime).

## 2. Enfoque arquitectónico

Tabla **`company_permissions`** gemela de `company_menus` (`company_id`, `permission_id`,
`is_enabled`), poblada por **dato** y localizada por `permissions.key` / `companies.name` — nunca por
id ni con `if (empresa == 'X')` (§ Features por EMPRESA del CLAUDE.md).

Dos compuertas:

1. **Asignación** (UI de Roles y Permisos): el tab *Permisos* del modal de rol solo ofrece los
   permisos habilitados en la(s) empresa(s) del rol. Mismo patrón que ya usan los menús
   (`loadRoleModalMenusByCompany`).
2. **Runtime** (login): los permisos efectivos del usuario se intersectan con los de la empresa del
   rol que se los da. Desmarcar un permiso **apaga la función de verdad**, incluso en roles que ya lo
   tenían asignado.

La decisión es **lógica pura** en `Application/Calculos/CompanyPermissionCalculos.cs` con tests
xUnit; los services solo resuelven datos y delegan.

## 3. Reglas de negocio

| # | Regla |
|---|---|
| **R1** | **Fail-closed.** Un permiso es asignable a un rol solo si está habilitado en la empresa del rol. |
| **R2** | **Rol multi-empresa ⇒ intersección.** Un rol compartido entre empresas solo puede llevar permisos que **todas** sus empresas tengan habilitados. La UI avisa cuando la intersección recorta. |
| **R3** | **Runtime por par (rol, empresa).** Permisos efectivos = `⋃` sobre cada `user_roles(rol, empresa)` de `perms(rol) ∩ habilitados(empresa)`. No depende de "empresa activa": un permiso sobrevive solo si viene de un rol cuya empresa lo tiene prendido. |
| **R4** | **Ninguna empresa queda en cero.** El seed cubre todas las empresas existentes; una empresa sin permisos en uso recibe el catálogo completo. Y una empresa **nueva** nace con el catálogo completo habilitado (`CompanyService.CreateAsync`), para que fail-closed nunca bloquee la creación del primer rol. |
| **R5** | **No destructivo.** Deshabilitar un permiso en la empresa **no borra** `role_permissions`. La fila queda huérfana: no se puede re-seleccionar y no viaja en el login, y la UI la marca como *"asignada pero deshabilitada en la empresa"* para que el admin la limpie a conciencia. |
| **R6** | **Comparación case-insensitive** de keys (el front las baja a minúscula en `loadPermissions`). |

## 4. Archivos

### Backend

| Archivo | Acción |
|---|---|
| `Domain/Entities/CompanyPermission.cs` | **nuevo** — gemelo de `CompanyMenu` |
| `Domain/Entities/Company.cs` | nav `CompanyPermissions` |
| `Domain/Entities/Permission.cs` | nav `CompanyPermissions` |
| `Infrastructure/Persistence/Configurations/CompanyPermissionConfiguration.cs` | **nuevo** — `ToTable("company_permissions")`, PK compuesta, FKs cascade |
| `Infrastructure/Persistence/ZooSanMarinoContext.cs` | `DbSet<CompanyPermission>` |
| `Application/Calculos/CompanyPermissionCalculos.cs` | **nuevo** — lógica pura (R1, R2, R3, R5, R6) |
| `Application/DTOs/CompanyPermissionDtos.cs` | **nuevo** — `CompanyPermissionItemDto`, `SetCompanyPermissionsRequest` |
| `Application/Interfaces/ICompanyPermissionService.cs` | **nuevo** |
| `Infrastructure/Services/CompanyPermissionService.cs` | **nuevo** |
| `API/Controllers/CompanyController.cs` | `GET`/`PUT /api/Company/{id}/permissions` |
| `API/Program.cs` | DI |
| `Infrastructure/Services/AuthService.cs` | gate runtime (R3) en login y en `GetUserWithMenuAsync` |
| `Infrastructure/Services/CompanyService*.cs` | siembra del catálogo al crear empresa (R4) |
| `tests/ZooSanMarino.Application.Tests/CompanyPermissionCalculosTests.cs` | **nuevo** |

### Migraciones

1. **`AddCompanyPermissions`** — schema, idempotente (`CREATE TABLE IF NOT EXISTS`).
2. **`SeedCompanyPermissionsDesdeRolesActuales`** — data-only idempotente: por cada empresa, los
   permisos que hoy usan sus roles (`role_companies ∪ user_roles` → `role_permissions`); empresa sin
   ninguno ⇒ catálogo completo. Con esto **nadie pierde acceso el día del deploy**.

### Frontend

| Archivo | Acción |
|---|---|
| `core/services/company-permission/company-permission.service.ts` | **nuevo** |
| `features/config/company-management/*` | modal *Permisos* junto a la de menús (ver + editar) |
| `features/config/role-management/funciones/filtrar-permisos-empresa.funcion.ts` | **nuevo** — función pura |
| `features/config/role-management/role-management.component.{ts,html}` | tab *Permisos* filtrado por empresa + aviso de huérfanos |

## 5. Casos de prueba (xUnit sobre el cálculo puro)

| # | Caso | Esperado |
|---|---|---|
| T1 | Empresa con config | solo los habilitados |
| T2 | Empresa sin filas | vacío + bandera `SinConfigurar` (la UI avisa, no miente) |
| T3 | Rol en 2 empresas | intersección |
| T4 | Runtime: permiso `X` en rol de empresa A (no habilitado en A) y en rol de empresa B (habilitado) | `X` presente una sola vez |
| T5 | Runtime: mismo permiso solo por empresa que no lo habilita | ausente |
| T6 | Keys en mayúscula/minúscula mezcladas | matchean igual |
| T7 | Permisos asignados al rol que la empresa deshabilitó | salen listados como huérfanos, no se pierden silenciosamente |
| T8 | Catálogo vacío | vacío, sin excepción |

## 6. Validación

- `cd backend && dotnet build` (0 errores) + `dotnet test` (suite completa verde).
- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
- **Smoke doble en local:** empresa con config recortada (ItalcolEcuador, 18 permisos) ⇒ el selector
  ofrece 18; empresa con catálogo completo (Santa Reyes, 31) ⇒ cero cambios visibles.
- **Antes/después del seed:** los permisos efectivos de cada usuario (login) deben ser **idénticos**
  a los de hoy. Es el invariante que prueba que el seed no dejó a nadie afuera.
