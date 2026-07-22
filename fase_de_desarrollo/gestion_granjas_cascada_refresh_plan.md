# Plan — Gestión de Granjas: cascada al eliminar + refresco entre tabs + scoping por granja asignada

## Contexto / módulo
`frontend/src/app/features/farm/pages/farm-management` (contenedor con tabs **Granjas / Núcleos / Galpones**, ruta `config/farm-management`). Los tres listados (`app-farm-list`, `app-nucleo-list`, `app-galpon-list`) se renderizan simultáneamente vía `[hidden]`.

## Problemas reportados y causa raíz

| # | Síntoma | Causa raíz |
|---|---------|-----------|
| 1 | Al eliminar una granja NO se deshabilitan sus núcleos/galpones | `FarmService.DeleteAsync` (backend) hace soft-delete **solo** de la granja; no hay cascada. |
| 2 | Al crear (p. ej. una granja) hay que recargar la app para verlo en otra tab | Cada listado carga una sola vez en `ngOnInit`; no hay comunicación entre tabs. |
| 3 | El listado de núcleos sigue mostrando datos viejos aun recargando | `NucleoService.getAll()` usa `shareReplay(1)` que **nunca se invalida**. |
| 4 | Trae núcleos/galpones que no pertenecen a las granjas asignadas al usuario | La tab Granjas se filtra por granjas asignadas (`id_user_session`), pero `NucleoService.GetAllAsync` y `GalponService.GetAllAsync` **omiten TODO filtro cuando el usuario es admin/super-admin** → devuelven todo. |

## Enfoque arquitectónico

### Backend (.NET) — `ZooSanMarino.Infrastructure` + `ZooSanMarino.Application`
1. **Cascada en `FarmService.DeleteAsync(id)`**: tras marcar la granja como eliminada, soft-delete (mismo `SaveChanges`, atómico) de **núcleos y galpones** de esa granja/empresa con `DeletedAt == null` (set `DeletedAt`, `UpdatedAt`, `UpdatedByUserId`). También en `HardDeleteAsync` (cascada dura por consistencia).
2. **Scoping consistente en listados de tabs** — `NucleoService.GetAllAsync` y `GalponService.GetAllAsync` dejan de tener el *bypass* de admin y pasan a filtrar **estrictamente por las granjas asignadas al usuario (UserFarms)** — igual que la tab Granjas — y excluyendo hijos de granjas eliminadas (`Farm.DeletedAt == null`). Fail-closed (sin `UserGuid` o sin granjas asignadas → vacío).
   - *No* se tocan `SearchAsync`, `GetByGranjaAsync`, `GetByGranjaAndNucleoAsync` (mantienen su comportamiento; siguen usados por otras pantallas).
3. **Cálculo puro** → `Application/Calculos/GestionGranjasCalculos.cs` (`static class`): regla de visibilidad por granja (`EsVisiblePorGranja`, `FiltrarVisiblesPorGranja`) y selección de hijos a inhabilitar (`RequiereInhabilitar`). Se testea con xUnit (gate CI).

### Frontend (Angular)
4. **Bus de refresco entre tabs** — nuevo `GestionGranjasRefreshService` (`providedIn: 'root'`) con `changes$` y `notify(source)`. Los listados emiten en su CRUD y reaccionan a cambios que los afectan.
   - `farm-list`: `notify('farm')` en create/update/delete.
   - `nucleo-list`: `notify('nucleo')` en su CRUD; reacciona a `'farm'` → invalida caché núcleo + resetea caché de granjas del modal + recarga.
   - `galpon-list`: `notify('galpon')` en su CRUD; reacciona a `'farm'` y `'nucleo'` → resetea cachés (farms/nucleos) del modal, invalida caché núcleo y recarga galpones.
5. **Invalidación de caché en `NucleoService`** — `getAll(force = false)` (reconstruye caché si `force`), `invalidate()`, y `tap(() => this.invalidate())` en `create/update/delete`. Sin quitar el `shareReplay` global (evita regresión de carga en las 23 pantallas consumidoras).

## Archivos a crear / modificar

**Backend**
- `backend/src/ZooSanMarino.Application/Calculos/GestionGranjasCalculos.cs` (NUEVO, puro).
- `backend/src/ZooSanMarino.Infrastructure/Services/FarmService.cs` (`DeleteAsync`, `HardDeleteAsync`: cascada).
- `backend/src/ZooSanMarino.Infrastructure/Services/NucleoService.cs` (`GetAllAsync`: scoping).
- `backend/src/ZooSanMarino.Infrastructure/Services/GalponService.cs` (`GetAllAsync`: scoping).
- `backend/tests/ZooSanMarino.Application.Tests/GestionGranjasCalculosTests.cs` (NUEVO).

**Frontend**
- `frontend/src/app/features/farm/services/gestion-granjas-refresh.service.ts` (NUEVO).
- `frontend/src/app/features/farm/services/gestion-granjas-refresh.service.spec.ts` (NUEVO).
- `frontend/src/app/features/nucleo/services/nucleo.service.ts` (caché invalidable).
- `frontend/src/app/features/farm/components/farm-list/farm-list.component.ts` (notify).
- `frontend/src/app/features/nucleo/components/nucleo-list/nucleo-list.component.ts` (notify + react).
- `frontend/src/app/features/galpon/components/galpon-list/galpon-list.component.ts` (notify + react).

## Reglas de negocio
- Eliminar granja = deshabilitar (soft-delete) granja + sus núcleos + sus galpones, atómico.
- Las tabs Núcleos/Galpones muestran **solo** lo de las granjas asignadas al usuario (mismo alcance que la tab Granjas), incluso para super-admin.
- Refactor sin cambiar aritmética ni contratos de otras pantallas (Search/GetByGranja intactos).

## Casos de prueba
- **Backend (xUnit puro):**
  - `EsVisiblePorGranja`: granja asignada + activa → visible; no asignada → no; asignada pero eliminada → no.
  - `FiltrarVisiblesPorGranja`: filtra lista mixta correctamente; sin asignadas → vacío.
  - `RequiereInhabilitar`: `DeletedAt == null` → true; ya eliminado → false.
- **Frontend (Karma):**
  - `GestionGranjasRefreshService`: `notify(x)` emite en `changes$`.
  - `NucleoService`: `invalidate()` fuerza nuevo GET; `create/update/delete` invalidan.
- **Manual (preview):**
  - Crear granja → aparece en dropdowns de Núcleos/Galpones sin recargar.
  - Crear núcleo → aparece en Galpones sin recargar.
  - Eliminar granja → sus núcleos y galpones desaparecen de sus tabs.
  - Usuario ve en Núcleos/Galpones solo lo de sus granjas asignadas.
