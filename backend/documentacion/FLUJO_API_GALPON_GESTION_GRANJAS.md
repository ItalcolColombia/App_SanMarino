# Flujo GET /api/Galpon – Tab Galpones (Gestión de Granjas)

## Objetivo

El endpoint `GET /api/Galpon` debe devolver **solo los galpones** que:

1. Pertenecen a la **empresa activa** del usuario (header `X-Active-Company` / `X-Active-Company-Id`).
2. Están en **granjas a las que el usuario tiene permiso** (UserFarms o granjas de sus empresas).
3. Por tanto, son galpones de **núcleos** de esas granjas (doble filtro: empresa + granjas → núcleos implícitos).

Así se evita traer datos de otras empresas u otras granjas.

## Flujo validado

### 1. Request desde el frontend

- **URL:** `GET http://localhost:5002/api/Galpon`
- **Headers relevantes:**
  - `Authorization: Bearer <JWT>`
  - `x-active-company: Agricola sanmarino`
  - `x-active-company-id: 1`
  - `x-active-pais: 1`

El frontend (Gestión de Granjas → tab Galpones) usa el servicio de galpones que llama a este endpoint.

### 2. Middleware y empresa efectiva

- `ActiveCompanyMiddleware` valida la empresa del header y setea `EffectiveCompanyId` en `HttpContext.Items`.
- El servicio usa `GetEffectiveCompanyIdAsync()` (header o JWT) para filtrar por empresa.

### 3. GalponService (doble filtro)

Para usuarios **no** admin/administrador/super admin:

1. **Filtro por empresa:** `g.CompanyId == effectiveCompanyId`.
2. **Filtro por granjas con permiso:** se obtienen las granjas accesibles con `GetUserAccessibleFarmsAsync` (UserFarms + granjas de empresas del usuario) y se filtra `g.GranjaId` en esa lista. Si no hay granjas permitidas, se devuelve lista vacía.

Los galpones tienen `NucleoId` y `GranjaId`; al filtrar por granjas permitidas, solo quedan galpones de núcleos de esas granjas.

### 4. Métodos afectados

- `GetAllAsync()` – listado usado por el tab Galpones.
- `GetAllDetailAsync()` – listado detalle.
- `SearchAsync()` – búsqueda paginada.
- `GetByGranjaAsync(int granjaId)` – por granja (además se valida que la granja esté en las permitidas).
- `GetByGranjaAndNucleoAsync(int granjaId, string nucleoId)` – por granja y núcleo.
- `GetDetailByGranjaAndNucleoAsync(int granjaId, string nucleoId)` – detalle por granja y núcleo.

## Resumen

| Quién               | Empresa           | Granjas / Núcleos                    |
|---------------------|-------------------|----------------------------------------|
| Admin/Administrador | Sin filtro (todos)| Sin filtro                             |
| Usuario normal      | Solo empresa activa | Solo granjas (y por tanto núcleos) con permiso |

## Archivos implicados

- **API:** `ZooSanMarino.API/Controllers/GalponController.cs` (GetAll, GetAllDetail, Search, GetByGranja, GetByGranjaAndNucleo).
- **Servicio:** `ZooSanMarino.Infrastructure/Services/GalponService.cs` (GetEffectiveCompanyIdAsync, GetAllowedFarmIdsForCurrentUserAsync, y todos los métodos de listado anteriores).
- **Permisos:** `IUserFarmService.GetUserAccessibleFarmsAsync` (granjas directas + por empresa).
