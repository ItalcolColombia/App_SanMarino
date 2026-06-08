# Tracker: Separación logo → `logo_companias` + Clean Code company-management

**Plan:** [28_logo_companias_separacion_tabla_plan.md](fase_de_desarrollo/28_logo_companias_separacion_tabla_plan.md)
**Inicio:** 2026-06-07

---

## Backend

### Domain
- [x] Crear `CompanyLogo.cs` (entidad nueva)
- [x] Modificar `Company.cs` — quitar `LogoBytes`/`LogoContentType`, agregar nav `Logo`

### Application
- [x] Crear `Application/Calculos/CompanyCalculos.cs` — `BuildLogoDataUrl` + `TryExtractLogo`

### Infrastructure — EF
- [x] Crear `CompanyLogoConfiguration.cs` (tabla `logo_companias`)
- [x] Modificar `CompanyConfiguration.cs` — quitar mapeos de logo
- [x] Agregar `DbSet<CompanyLogo>` al contexto
- [x] Crear migración `ExtractLogoToLogoCompanias` (idempotente + migración de datos)

### Infrastructure — Services (Clean Code)
- [x] Crear `Services/CompanyService/CompanyService.cs` (ancla partial)
- [x] Crear `Funciones/CompanyService.Crud.cs`
- [x] Crear `Funciones/CompanyService.Logo.cs`
- [x] Crear `Funciones/CompanyService.Permisos.cs`
- [x] Eliminar `Services/CompanyService.cs` original
- [x] Actualizar `CompanyPaisService.cs` — join con `logo_companias` (ThenInclude)
- [x] Actualizar `AuthService.cs` — ThenInclude Logo para CompanyPaisDto

### Validación
- [x] `dotnet build` → 0 errores
- [x] `dotnet test` → 58 tests ✅

---

## Frontend

### Clean Code
- [x] Crear `models/company-management.model.ts`
- [x] Crear `funciones/logo.funcion.ts`
- [x] Crear `funciones/geo.funcion.ts`
- [x] Crear `funciones/roles.funcion.ts`
- [x] Crear `funciones/paises.funcion.ts`
- [x] Crear `funciones/README.md`
- [x] Refactorizar `company-management.component.ts` (orquestador delgado)

### Validación
- [x] `yarn build` → 0 errores ✅ (warning de budget es preexistente)
