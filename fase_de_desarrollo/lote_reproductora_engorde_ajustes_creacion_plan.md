# Plan — Ajustes de creación/edición en Lote Reproductora Aves de Engorde

> Módulo: `lote-reproductora-ave-engorde` (front) + `LoteReproductoraAveEngorde*` (back).
> Sesión paralela — NO tocar archivos de las otras sesiones activas.

## Contexto (código actual = fuente de verdad)

- **Front creación**: modal "Registrar lote reproductora" es *bulk* (FormArray `incubadoras`) en
  `lote-reproductora-ave-engorde-list.component.ts` (`createBulkRow()`, `saveBulk()`) +
  `...component.html` (filas del FormArray).
- **Front edición**: modal "Editar" usa el `form` raíz + `save()`.
- **Back**: `LoteReproductoraAveEngordeService` (`CreateAsync`/`CreateBulkAsync`/`UpdateAsync`/`DeleteAsync`),
  controller `LoteReproductoraAveEngordeController`. `edadDias` se calcula en `Map()`.
- **Permisos**: patrón `UserPermissionService` + `*appHasPermission` (ver módulo hermano
  `seguimiento-diario-lote-reproductora`, keys `seguimiento_reproductora_engorde.confirmar/.eliminar`).
- **Registros del lote reproductora** = filas de `SeguimientoDiarioLoteReproductoraAvesEngorde`
  (`LoteReproductoraAveEngordeId`), contadas hoy en `numRegistros` (stat `Num`, tope 7 = "Cerrado").

## Enfoque arquitectónico

Refactor ≠ cambio de comportamiento salvo en los 5 puntos pedidos. Front delgado; backend autoritativo
para el bloqueo de borrado (defense-in-depth). Sin romper el contrato del DTO ni la aritmética existente
de aves/disponibles.

---

## Requisitos

### R1 — Quitar "Código reproductora" al **crear**
- **Front**: quitar el campo `codigoReproductora` del modal bulk:
  - `createBulkRow()`: eliminar el control `codigoReproductora`.
  - HTML: eliminar el `<div class="lrae-field">` de "Código reproductora" en la fila del FormArray.
  - `saveBulk()`: no enviar `codigoReproductora` (queda `null` → el back ya lo guarda `null`).
- **Decisión**: se quita SOLO de creación. El modal **Editar** conserva el campo (sigue editable) y la
  tabla/detalle lo siguen mostrando. La columna/dato en BD no se toca.

### R2 — "Nombre del lote": obligatorio, pero **vacío** por defecto (no prellenar con el lote principal)
- `createBulkRow()`: `nombreLote: ['', [Validators.required, Validators.maxLength(200)]]` (quitar `baseNombre`).
- `save()` (rama crear): quitar el fallback `|| (this.loteSeleccionado?.loteNombre ?? '')`
  (línea ~461) → el `required` bloquea el submit vacío en vez de rellenar con el nombre del lote padre.
- Backend ya exige `NombreLote` no vacío (400) → sin cambios.

### R3 — "Edad (días)": congelar al cerrar (DECIDIDO por el usuario) — SIN migración
- Hoy: `Map()` → `edadDias = max(0, (UtcNow.Date − FechaEncasetamiento.Date).days)`. Crece con el reloj del
  sistema, puede dar 14 aunque la recogida sea de 1..7 días.
- **Regla nueva**: mientras el lote está **Vigente**, edad = `hoy − fechaEncaset` (igual que hoy). Cuando el lote
  pasa a **Cerrado** (7 días confirmados), la edad se **congela** en la fecha de cierre = `MAX(Fecha)` de los
  registros `SeguimientoDiarioLoteReproductoraAvesEngorde` → deja de crecer con el reloj.
- **Sin campo nuevo, sin migración.** La fecha de cierre se deriva del último registro de recogida.
- Implementación:
  - `ReproStats` + `GetReproStatsAsync`: agregar `MaxFecha = g.Max(s => (DateTime?)s.Fecha)`.
  - Cálculo PURO en `ReproductoraEngordeCalculos.CalcularEdadDias(fechaEncaset, hoyUtc, cerrado, fechaCierre)`
    + test xUnit (equivalencia Vigente = comportamiento previo; Cerrado = congela).
  - `Map()`: `edadDias = ReproductoraEngordeCalculos.CalcularEdadDias(x.FechaEncasetamiento, DateTime.UtcNow, estado == "Cerrado", stats?.MaxFecha)`.

### R4 — Bloquear eliminar reproductora que ya tiene registros cargados
- **Backend (autoritativo)** `DeleteAsync`: antes de `Remove`, contar
  `SeguimientoDiarioLoteReproductoraAvesEngorde` con `LoteReproductoraAveEngordeId == id`.
  Si `> 0` → `throw new InvalidOperationException("Elimine primero los N registros de seguimiento de esta reproductora.")`.
- **Controller** `Delete`: envolver en try/catch → `InvalidOperationException` ⇒ `400` (BadRequest/ValidationProblem).
  Hoy solo devuelve 204/404.
- **Front**: `deleteRegistro()` — si `r.numRegistros > 0`, avisar con Toast y no abrir el confirm (UX inmediata).
  Igual el back queda como red de seguridad; el error 400 ya se muestra por Toast.

### R5 — Permiso de **editar** y **eliminar** la reproductora
- Keys (patrón `modulo.accion`): `lote_reproductora_engorde.editar`, `lote_reproductora_engorde.eliminar`.
- **Front**:
  - Importar `HasPermissionDirective` + inyectar `UserPermissionService`.
  - Getters `canEditarPerm` / `canEliminarPerm` (`permSvc.has(KEY)`).
  - Botones Editar/Eliminar de la tabla: ocultar con `*appHasPermission` (o `[disabled]`); guardas en
    `edit()` y `deleteRegistro()` (return si no tiene permiso).
- **Permisos/menú**: alta de las 2 keys + asignación a roles (seed/role_menus manual post-deploy, como otros
  módulos). Confirmar si ya existen o se crean.
- Alineado con el módulo hermano (front-gated). Enforcement en backend: opcional, se evaluará para no
  divergir del patrón actual del controller (hoy sin `[Authorize(Policy)]`).

---

## Archivos a crear/modificar (previsto)

**Frontend**
- `features/lote-reproductora-ave-engorde/pages/.../lote-reproductora-ave-engorde-list.component.ts`
- `features/lote-reproductora-ave-engorde/pages/.../lote-reproductora-ave-engorde-list.component.html`
- (según R3) service/DTO si "Edad" pasa a input.

**Backend**
- `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` (`DeleteAsync`, y R3 si aplica).
- `API/Controllers/LoteReproductoraAveEngordeController.cs` (`Delete` → 400).
- (según R3) `Domain/Entities/LoteReproductoraAveEngorde.cs` + `Configurations/...` + migración EF idempotente.
- (según R5) seed de permisos / SQL de `role_menus` si hace falta.

## Casos de prueba
- R1: crear sin ver "Código reproductora"; se guarda `codigoReproductora = null`.
- R2: submit con "Nombre del lote" vacío → bloqueado (required); con valor → se guarda tal cual (no el del padre).
- R4: reproductora con ≥1 registro → DELETE devuelve 400 y no borra; sin registros → 204 borra.
- R5: sin `.editar` → botón/acción editar oculto/bloqueado; sin `.eliminar` → idem eliminar.
- R3: definir tras aclaración.

## Validación
- Front: `cd frontend && yarn build` (0 err; warning bundle budget preexistente OK).
- Back: `cd backend && dotnet build` + `dotnet test`. Si R3 agrega cálculo puro → test xUnit en
  `tests/ZooSanMarino.Application.Tests/`.
