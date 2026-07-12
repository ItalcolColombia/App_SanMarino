# Plan — Fix migración masiva de Granjas (regionales + departamentos)

Sub-tarea correctiva de la **Fase 1 (Estructura)** del módulo Migraciones Masivas.
Plan padre: [migraciones_masivas_plan.md](migraciones_masivas_plan.md).

## Problema (diagnóstico verificado contra BD local + módulo real de granjas)

La plantilla `.xlsx` de Granjas y su importación fallan porque la migración maneja **regionales** y
**departamentos** distinto a como lo hace el módulo real de granjas (fuente de verdad = `farm-list.component.ts`).

### 🔴 Bug A — Regionales (rompe la carga)
- **Realidad**: las regionales **no están en la tabla `regionales`** (0 filas). Se crean dinámicamente como
  **opciones de lista maestra** `key = 'region_option_key'` por empresa. El form las carga con
  `masterListSvc.getByKey('region_option_key', companyId)` y al guardar manda el **`id` de la opción**
  como `regionalId`. Verificado: `farms.regional_id ∈ {57,59,60,61,62}` = ids de opciones (Centro/Occidente/…).
- **Migración (mal)**: `CargarRegionalesAsync` lee la tabla `regionales` vacía → dropdown vacío; al importar
  resuelve el nombre contra esa tabla → nunca matchea → pasa `regionalId = null`.
- **Golpe final**: `farms.regional_id` es **`NOT NULL`** en BD → insertar null explota = "regional obligatoria".

### 🟠 Bug B — Departamentos incompletos
- El endpoint normal (`DepartamentoService.GetByPaisIdAsync`) devuelve **todos** los departamentos del país,
  sin filtrar por activo. La migración agrega `.Where(d => d.Active)` → recorta. Hay que quitar ese filtro.
- Nota: el catálogo `departamentos`/`municipios` es chico (Colombia ~10 en BD). Si en prod está incompleto,
  es tema de **datos semilla**, idéntico al form normal — fuera de alcance de este fix de código.

## Enfoque (refactor = cambio de comportamiento SOLO en lo diagnosticado; sin tocar aritmética/UI)

Backend, 2 archivos partial de `MigracionService` (namespace plano `ZooSanMarino.Infrastructure.Services`):

### `Infrastructure/Services/Migracion/Funciones/MigracionService.Estructura.cs`
1. `CargarDepartamentosAsync`: **quitar** `.Where(d => d.Active)` (igualar al endpoint normal).
2. Reemplazar `CargarRegionalesAsync` (hoy lee `_ctx.Regionales`) por lectura de opciones de la lista maestra
   `region_option_key` de la empresa (mismo scoping que `MasterListService.GetByKeyAsync`:
   `ml.CompanyId == companyId || ml.CompanyId == null`). Devuelve `List<MasterListOption>` ordenado por `Order`.
   Constante `RegionOptionKey = "region_option_key"`.
3. `ProcesarGranjasAsync`:
   - `regionalPorNombre`: mapear `NormalizarClave(option.Value) → option.Id`.
   - Regional **obligatoria**: si viene vacía → error "La regional es obligatoria."; si no matchea → "La regional no existe en la empresa.".
   - Pasar `RegionalId: regionalId` (ya no null) en `CreateFarmDto` (se guarda directo en `farms.regional_id`).

### `Infrastructure/Services/Migracion/Funciones/MigracionService.Plantillas.cs`
4. `GenerarPlantillaGranjasAsync`: la columna/dropdown "Regionales" usa `option.Value` (filtrando vacíos).
   Actualizar la línea de Instrucciones: Regional pasa de "opcional" a "obligatoria".

## Reglas de negocio
- `farms.regional_id` = id de `master_list_options` (contrato idéntico al form de granjas).
- Regional obligatoria (BD `NOT NULL` + form normal la exige con `Validators.required`).
- Departamento y Ciudad ya eran obligatorios (se conserva).

## Casos de prueba
- **Query regionales** (validada por psql): empresa 1 → 6 opciones (57 Centro … 62 División Pollita).
- **Build**: `cd backend && dotnet build` 0 errores / 0 nuevas advertencias.
- **Tests**: `dotnet test` sigue 267/267 (el fix no toca `MigracionCalculos`, que es lo cubierto por unit tests).
- **E2E** (aceptación usuario): descargar plantilla Granjas → dropdown Regional con las 6 opciones →
  llenar 1 fila con regional válida → Importar → 1 granja creada con `regional_id` = id de la opción.
  Fila sin regional → error "La regional es obligatoria." (dry-run, no inserta).

## Fuera de alcance
- Completar el catálogo oficial de departamentos/municipios (datos semilla).
- Cambiar la nulabilidad de `farms.regional_id` (se respeta el `NOT NULL` existente).
