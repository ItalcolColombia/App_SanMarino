# Plan — Lote base de pollo engorde: creación simple + asignación de granjas + visibilidad por granja

## Objetivo (pedido del usuario)
Rediseñar el módulo **Lote base** de pollo engorde:
1. **Crear** solo pide **nombre**. La **fecha de activación** se toma automática (hoy) y se **captura el usuario** que lo creó.
2. Una vez creado el lote base, tener una opción **"Asignar granjas"** — la **misma** que en Usuarios (modal + `GET /api/Farm/assignable`): si el usuario es **Administrador de Empresa/Super Admin** trae **todas** las granjas de la empresa; si no, solo las asignadas.
3. Esa asignación es un **filtro de visibilidad**: al **crear un lote de engorde**, el selector de "Nombre del lote / Lote base" solo muestra los lotes base **asignados a la granja seleccionada** (hoy salen para todas).
4. **Quitar la vigencia por año** (el lote base ya no "se vence"; el mismo nombre se puede repetir año tras año).

## Decisiones cerradas con el usuario
- **Campos extra** (Código ERP, Línea genética, Descripción): se **quitan del módulo** (form y lista). Las **columnas se conservan en BD** (no se borran) para no romper el Reporte Diario Costos ni los datos existentes; simplemente no se piden ni se muestran.
- **Toggle Activo/Desactivar**: se **mantiene** como apagado global (un lote base `Inactivo` no aparece en ningún crear-lote, además del filtro por granja).

## Enfoque arquitectónico
Reusar el modelo M:N usuario↔granja pero para lote base↔granja. Nueva tabla puente `lote_base_engorde_granja`. El filtro al crear lote se resuelve en el front con los `granjaIds` que el backend adjunta a cada lote base (catálogo chico por empresa; no es agregación pesada multipaís). El modal de asignar granjas reutiliza `FarmService.getAssignableFarms()` (scoping admin/asignadas ya resuelto en backend).

## Cambios BD / SQL
- **Nueva tabla** `public.lote_base_engorde_granja`:
  - `id` PK serial, `lote_base_engorde_id` int NOT NULL, `farm_id` int NOT NULL, `company_id` int NOT NULL,
    `created_by_user_id` int NOT NULL, `created_at` timestamptz NOT NULL default now.
  - UNIQUE (`lote_base_engorde_id`, `farm_id`); índices por `farm_id` y por `lote_base_engorde_id`.
- Migración EF **idempotente** (`CREATE TABLE IF NOT EXISTS` + `CREATE INDEX IF NOT EXISTS`). No se toca `lote_base_engorde` (las columnas erp/línea/descripción/fecha/activo quedan como están).

## Backend (archivos)
- **Crear** `Domain/Entities/LoteBaseEngordeGranja.cs` (entidad puente).
- **Crear** `Infrastructure/Persistence/Configurations/LoteBaseEngordeGranjaConfiguration.cs` (mapeo snake_case + índices).
- **Editar** `Infrastructure/Persistence/ZooSanMarinoContext.cs` → `DbSet<LoteBaseEngordeGranja>`.
- **Crear** migración `AddLoteBaseEngordeGranja` (idempotente) + snapshot.
- **Editar** `Application/DTOs/LoteBaseEngorde/LoteBaseEngordeDtos.cs`:
  - `LoteBaseEngordeDto`: **agregar** `IReadOnlyList<int> GranjaIds` y `string? CreatedByNombre` (se conservan los campos de lectura existentes → compat con el reporte).
  - Simplificar `CreateLoteBaseEngordeDto` a `(string Nombre)` y `UpdateLoteBaseEngordeDto` a `(int Id, string Nombre)`.
  - **Nuevos**: `LoteBaseEngordeGranjaDto(int FarmId, string FarmName)`, `AssignGranjaLoteBaseDto(int FarmId)`.
- **Editar** `Application/Interfaces/ILoteBaseEngordeService.cs`: quitar `soloVigentes`; agregar `GetGranjasAsync`, `AssignGranjaAsync`, `UnassignGranjaAsync`.
- **Editar** `Infrastructure/Services/LoteBaseEngordeService.cs`:
  - `CreateAsync`: fecha activación = hoy (UTC date) auto; usuario auto (ya se hacía). Solo nombre.
  - `UpdateAsync`: solo renombra.
  - `GetAllAsync(ct)`: sin vigencia; adjunta `GranjaIds` (consulta agrupada a la puente) + `CreatedByNombre` (join por `cedula` = `CreatedByUserId.ToString()`, patrón de `TicketService`) + `LotesAsignados` (conteo agrupado).
  - `DeleteAsync`: además borra filas de la puente del lote base.
  - `GetGranjasAsync/AssignGranjaAsync/UnassignGranjaAsync`: validan que el lote base y la granja pertenezcan a la empresa efectiva; assign idempotente.
- **Editar** `API/Controllers/LoteBaseEngordeController.cs`: quitar `soloVigentes`; agregar `GET {id}/granjas`, `POST {id}/granjas`, `DELETE {id}/granjas/{farmId}`. La asignación se gatea con permiso `editar` (nivel controller sigue `[Authorize]`; gate fino en front).

## Frontend (archivos)
- **Editar** `features/engorde-comun/services/lote-base-engorde.api.ts`:
  - `LoteBaseEngordeDto`: agregar `granjaIds: number[]`, `createdByNombre?`.
  - `create` payload = `{ nombre }`; `update` = `{ id, nombre }`. Quitar `soloVigentes` de `getAll`.
  - Agregar `getGranjas(id)`, `assignGranja(id, farmId)`, `unassignGranja(id, farmId)` + interface `LoteBaseEngordeGranjaDto`.
- **Crear** componente standalone `features/lote-engorde/components/asignar-granjas-lote-base/` (modal, espejo simplificado de `asignar-usuario-granja`): inputs `loteBaseId/loteBaseNombre/isOpen`, outputs `close/updated`. Lista asignadas (`getGranjas`) + disponibles (`FarmService.getAssignableFarms()`), asignar/quitar. Toast + confirm centralizados.
- **Editar** `features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.ts`:
  - Quitar `esVigente`/`lotesBaseVigentes`. Nuevo `lotesBaseParaGranja` (referencia estable) = `lotesBase.filter(activo && granjaIds.includes(granjaId))` + incluir el `loteBaseEngordeId` en edición aunque no esté asignado.
  - Recomputar `lotesBaseParaGranja` en `granjaId.valueChanges`, al abrir modal y tras cargar lotes base.
  - `lbGuardar`: solo nombre. Quitar estados `lbCodigoErp/lbLineaGenetica/lbDescripcion/lbFechaActivacion`.
  - `crearLoteBaseRapido`: tras crear, auto-asigna a la granja seleccionada (si hay) y recomputa.
  - Estado + handlers del modal de asignar granjas.
- **Editar** `...lote-engorde-list.component.html`:
  - Selects de crear-lote (Panamá obligatorio / Ecuador opcional) usan `lotesBaseParaGranja`; hint "seleccione granja primero" si no hay granja.
  - Tab/modal de gestión: form solo Nombre; lista con columnas Nombre · Activación · Creado por · Granjas (conteo) · Estado · Acciones (Asignar granjas, Activar/Desactivar, Editar, Eliminar). Ajustar textos (quitar "año en curso").
  - Montar `<app-asignar-granjas-lote-base>`.

## Reglas de negocio
- Visibilidad de un lote base al crear lote = `activo == true` **Y** granja seleccionada ∈ granjas asignadas del lote base.
- Panamá: nombre del lote = lote base (obligatorio) → el select solo lista los asignados a la granja.
- Nombre de lote base único por empresa (case-insensitive, entre vivos) — se conserva.
- Delete bloqueado si hay lotes de engorde vivos amarrados — se conserva.

## Casos de prueba
- Crear lote base solo con nombre → fecha activación = hoy, creado_por = usuario actual.
- Asignar 2 granjas a un lote base; al crear lote en granja A aparece, en granja C (no asignada) no aparece.
- Admin de Empresa ve todas las granjas en el modal; usuario normal solo las asignadas.
- Desactivar lote base → desaparece de todos los crear-lote aunque tenga granjas asignadas.
- Editar lote existente cuyo lote base ya no está asignado a su granja → el select conserva el valor actual.
- Reporte Diario Costos sigue funcionando (getAll compat).
- Backend: `dotnet build` 0 errores + `dotnet test`. Front: `yarn build` 0 errores.
