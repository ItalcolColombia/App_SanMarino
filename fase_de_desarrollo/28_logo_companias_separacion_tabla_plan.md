# Plan: Separación logo → tabla `logo_companias` + Clean Code company-management

## Objetivo
Extraer el almacenamiento del logo de `companies` a una nueva tabla `logo_companias`, actualizar todos
los puntos de lectura/escritura y refactorizar el módulo `company-management` (back y front) siguiendo
la metodología Clean Code del proyecto.

---

## Enfoque arquitectónico
- **Relación 1:0..1** entre `Company` y `CompanyLogo` (una empresa puede tener 0 ó 1 logo).
- El contrato de la API **no cambia**: `CompanyDto.LogoDataUrl` y `CompanyPaisDto.CompanyLogoDataUrl`
  siguen existiendo; la diferencia es interna (JOIN a `logo_companias` en lugar de columnas en `companies`).
- Migración **idempotente**: crea la tabla con `CREATE TABLE IF NOT EXISTS`, migra datos existentes
  con `INSERT … WHERE NOT EXISTS`, y luego elimina columnas `logo_bytes`/`logo_content_type` de `companies`
  con `ALTER … DROP COLUMN IF EXISTS`.
- La lógica matemática pura (`BuildLogoDataUrl`, `TryExtractLogo`) se extrae a
  `Application/Calculos/CompanyCalculos.cs` (sin EF, sin DI).

---

## Archivos a crear / modificar

### Backend — Domain
| Acción | Archivo |
|--------|---------|
| CREAR  | `Domain/Entities/CompanyLogo.cs` |
| MODIFICAR | `Domain/Entities/Company.cs` — quitar `LogoBytes`/`LogoContentType`; agregar nav `Logo` |

### Backend — Infrastructure (EF)
| Acción | Archivo |
|--------|---------|
| CREAR  | `Persistence/Configurations/CompanyLogoConfiguration.cs` |
| MODIFICAR | `Persistence/Configurations/CompanyConfiguration.cs` — quitar mapeos de logo |
| MODIFICAR | `Persistence/ZooSanMarinoContext.cs` — agregar `DbSet<CompanyLogo>` |
| CREAR  | `Migrations/<timestamp>_ExtractLogoToLogoCompanias.cs` |

### Backend — Application
| Acción | Archivo |
|--------|---------|
| CREAR  | `Application/Calculos/CompanyCalculos.cs` — `BuildLogoDataUrl` + `TryExtractLogo` |

### Backend — Infrastructure (Services, Clean Code)
Reestructurar en `Services/CompanyService/` (namespace plano `ZooSanMarino.Infrastructure.Services`):

| Acción | Archivo |
|--------|---------|
| REEMPLAZA | `Services/CompanyService.cs` → pasa a ser ancla en `Services/CompanyService/CompanyService.cs` |
| CREAR  | `Services/CompanyService/Funciones/CompanyService.Crud.cs` — GetAll, GetById, Create, Update, Delete |
| CREAR  | `Services/CompanyService/Funciones/CompanyService.Logo.cs` — UpsertLogo, DeleteLogo |
| CREAR  | `Services/CompanyService/Funciones/CompanyService.Permisos.cs` — IsAdmin, IsSuperAdmin |
| MODIFICAR | `Services/CompanyPaisService.cs` — join con `logo_companias` al construir `CompanyPaisDto` |

### Frontend — company-management (Clean Code)
```
features/config/company-management/
├── models/
│   └── company-management.model.ts       ← tipos extraídos del componente
├── funciones/
│   ├── README.md
│   ├── logo.funcion.ts                   ← validación + lectura de archivo
│   ├── geo.funcion.ts                    ← resolución código←→nombre geográfico
│   ├── roles.funcion.ts                  ← filtro roles, permisos combinados
│   └── paises.funcion.ts                 ← diff para add/remove países
├── company-management.component.ts       ← orquestador delgado
├── company-management.component.html
└── company-management.component.scss
```

---

## Reglas de negocio preservadas
- Logo máximo 512 KB, tipo `image/*`
- null en DTO → no cambia; "" → borrar; dataUrl → actualizar
- Si se edita la empresa activa, actualizar `tokenStorage.updateActiveCompanyLogo()`
- Al crear empresa: INSERT en `logo_companias` si viene logo
- Al actualizar empresa: UPSERT en `logo_companias` (insert or update); si "" → DELETE

---

## Casos de prueba
- Crear empresa sin logo → `logo_companias` sin registro
- Crear empresa con logo → registro en `logo_companias`
- Actualizar empresa cambiando logo → registro actualizado
- Borrar logo → registro eliminado de `logo_companias`
- `GET /api/Company/{id}` devuelve `logoDataUrl` correcto
- `GET /api/CompanyPais/user/current` devuelve `companyLogoDataUrl` correcto
- Sidebar muestra logo de la empresa activa
