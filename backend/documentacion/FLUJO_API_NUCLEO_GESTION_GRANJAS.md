# Flujo GET /api/Nucleo – Tab Núcleo (Gestión de Granjas)

## Objetivo

El endpoint `GET /api/Nucleo` debe devolver **solo los núcleos** que:

1. Pertenecen a la **empresa activa** del usuario logueado (header `X-Active-Company` / `X-Active-Company-Id`).
2. Están en **granjas a las que el usuario tiene permiso** (UserFarms o granjas de sus empresas).

## Flujo validado

### 1. Request desde el frontend

- **URL:** `GET http://localhost:5002/api/Nucleo`
- **Headers relevantes:**
  - `Authorization: Bearer <JWT>` (usuario autenticado)
  - `x-active-company: Agricola sanmarino`
  - `x-active-company-id: 1`
  - `x-active-pais: 1`

El frontend (Gestión de Granjas → tab Núcleos) usa `NucleoService.getAll()` que llama a este endpoint.

### 2. Middleware (ActiveCompanyMiddleware)

- Lee `X-Active-Company` y/o `X-Active-Company-Id`.
- Comprueba que el usuario tenga acceso a esa empresa (`UserCompanies` o super admin).
- Si es válido, guarda en `HttpContext.Items`:
  - `EffectiveCompanyId`
  - `EffectiveCompanyName`

Si el usuario no tiene permiso para esa empresa, no se setean estos valores y se usa el `company_id` del JWT.

### 3. Controller

- `NucleoController.GetAll()` → `INucleoService.GetAllAsync()`.

### 4. NucleoService.GetAllAsync()

- **Empresa:** Si el usuario **no** es admin/administrador/super admin:
  - Se obtiene la empresa efectiva con `GetEffectiveCompanyIdAsync()` (header activo o JWT).
  - Se filtran núcleos por `n.CompanyId == effectiveCompanyId`.
- **Granjas:** Para usuarios no admin se obtienen las granjas permitidas con `GetUserAccessibleFarmsAsync` (UserFarms + granjas de empresas del usuario) y se filtran núcleos por `n.GranjaId` en esa lista. Si no hay granjas permitidas, se devuelve lista vacía.
- **Admin:** Si el usuario es admin (por rol o por tener todos los países asignados), se devuelven todos los núcleos sin filtrar por empresa ni por granja.

Con esto se cumple: *“núcleos de las granjas que tiene permisos el usuario y que pertenecen a la empresa del usuario logueado”*.

## Resumen

| Quién              | Empresa                    | Granjas                          |
|--------------------|----------------------------|----------------------------------|
| Admin/Administrador| Sin filtro (todos)          | Sin filtro                       |
| Usuario normal     | Solo empresa activa        | Solo granjas a las que tiene acceso |

## Archivos implicados

- **API:** `ZooSanMarino.API/Controllers/NucleoController.cs` (GetAll).
- **Middleware:** `ZooSanMarino.API/Infrastructure/ActiveCompanyMiddleware.cs`.
- **Usuario actual:** `ZooSanMarino.API/Infrastructure/HttpCurrentUser.cs` (CompanyId, ActiveCompanyName).
- **Servicio:** `ZooSanMarino.Infrastructure/Services/NucleoService.cs` (GetAllAsync, GetEffectiveCompanyIdAsync, GetAllowedFarmIdsForCurrentUserAsync).
- **Frontend tab Núcleos:** `farm-management.component` → `app-nucleo-list` → `NucleoService.getAll()`.
