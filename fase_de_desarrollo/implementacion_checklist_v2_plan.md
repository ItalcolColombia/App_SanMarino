# Plan — Módulo Implementación (checklists) v2: rediseño + firmas de participantes

**Fecha:** 2026-07-20 · **Módulo:** `implementacion` (cronogramas de entrega/capacitación por empresa)

## Contexto y problemas reportados

1. **"Los filtros nunca cargan / se quedan pensando aunque la petición retornó":** el módulo hoy **no tiene filtros** y sus páginas solo tienen 2 estados visibles (spinner / datos). Si una petición falla (JWT vencido → 401, 500, red), el error se ve solo en un toast fugaz y la página queda vacía o aparenta "seguir pensando" (sin estado de error visible, sin reintento, sin timeout). El backend local responde en milisegundos (verificado: `GET /api/Implementacion/planes` → 401 en 0.07 s sin token), o sea el cuelgue percibido es de **UX de estados**, no del API.
2. **"Muy fuera del diseño / estética del módulo":** las páginas usan tabla plana con acciones como links de texto (`btn-ghost` repetidos), sin card de filtros estándar (`filters-row`/`filter-group` como `movimientos-pollo-engorde`), sin KPIs consistentes ni chips.
3. **Funcionalidad pedida (flujo completo):**
   - Crear **cronograma** con descripción + fechas de implementación, que sirva para **entregas y capacitaciones** (tipo).
   - Ver **creador** del cronograma y campo **encargado/implementador**: por defecto el mismo creador; si es otro, se elige en el campo "implementador diferente".
   - Luego crear sus **ítems de validación** (tareas) con fechas y descripción (ej. "Integrar Italgranja en todo Panamá", "Capacitación módulo X fecha Y").
   - **Asignar los usuarios que estuvieron** en la capacitación/entrega (participantes, N por tarea): cada uno **ve el detalle y firma** (firma digitada en un campo) que estuvo/recibió cada punto, con **nota u observación**.
   - Si **no quieren recibir/firmar** → se registra una **novedad** (motivo obligatorio) y se lo **guía a crear un ticket** explicando por qué no firma.
   - Al ver el check se ve el **detalle**: qué se realizó, fecha, la **firma digitada**, el usuario de la aplicación **con su correo** (sacado de la app: `user_logins → logins.email`) y **quién fue el encargado** de la implementación.

## Enfoque arquitectónico

- **BD (aditivo, idempotente):** 3 columnas nuevas en `implementacion_planes` (`tipo`, `implementador_user_id`, `creado_por_user_guid`) + tabla nueva `implementacion_tarea_firmas` (participantes/firmas por tarea). Sin tocar lo existente; el estado del plan sigue derivándose de tareas (misma aritmética).
- **Backend:** mismo patrón del módulo (partial classes en `Services/Implementacion/Funciones/`, cálculo puro en `ImplementacionCalculos`, controller REST existente ampliado). Email de usuario vía join `user_logins → logins.email` (primer login no borrado).
- **Frontend:** rediseño de las 3 páginas + modales con **tokens/clases globales del tema** (`ux-card`, `filters-row` local, `input-italfoods`, `btn-italfoods-*`, `table-italfoods`, `icon-btn`, `spinner`, `empty-state`) + **filtros client-side instantáneos** (funciones puras) + **estados robustos de carga** (cargando/error con Reintentar/vacío; `timeout` rxjs 30 s para no dejar spinners infinitos; 401 → mensaje de sesión vencida).
- **Novedad → ticket:** al rechazar se guarda la novedad y se ofrece navegar a `/tickets/nuevo` (módulo tickets existente; sin prefill porque ese form no lee query params — fuera de alcance tocarlo).

## Cambios de BD (migración EF idempotente `AddImplementacionFirmasYTipo`)

```sql
ALTER TABLE implementacion_planes
  ADD COLUMN IF NOT EXISTS tipo varchar(20) NOT NULL DEFAULT 'implementacion',      -- CHECK ('implementacion','capacitacion','mixto')
  ADD COLUMN IF NOT EXISTS implementador_user_id uuid NULL,                          -- FK users (encargado; default = creador)
  ADD COLUMN IF NOT EXISTS creado_por_user_guid uuid NULL;                           -- FK users (guid real del creador)

CREATE TABLE IF NOT EXISTS implementacion_tarea_firmas (
  id identity PK, tarea_id int NOT NULL FK→implementacion_tareas ON DELETE CASCADE,
  user_id uuid NOT NULL FK→users, estado varchar(20) NOT NULL DEFAULT 'pendiente',  -- CHECK ('pendiente','firmada','rechazada')
  firma_texto varchar(300) NULL, nota varchar(2000) NULL, fecha_respuesta timestamptz NULL,
  company_id int NOT NULL + auditoría estándar (created_by_user_id int, created_at, updated_*, deleted_at));
-- Índices: tarea_id, user_id, company_id + UNIQUE parcial (tarea_id, user_id) WHERE deleted_at IS NULL
```

FKs guardadas con `DO $$ ... pg_constraint` (patrón de `20260720135027_AddImplementacionModule`). CHECKs con `DO $$` + `IF NOT EXISTS`.

## Reglas de negocio

- **Plan:** `tipo ∈ {implementacion, capacitacion, mixto}` (default `implementacion`). `ImplementadorUserId` nulo en el request → se usa el guid del creador. `creado_por_user_guid` se setea al crear (planes viejos: null → UI muestra "—").
- **Participantes (por tarea):** solo usuarios activos de la empresa activa. `PUT participantes` sincroniza: agrega faltantes (estado `pendiente`), quita solo los que siguen `pendiente` (soft delete); si se intenta quitar uno `firmada`/`rechazada` → 400 con mensaje claro (auditoría no se borra). Reactivar un participante previamente quitado revive su fila (soft-undelete) conservando id.
- **Firmar:** solo el propio participante (por `UserGuid`, fail-closed). Requiere `firma_texto` (3–300 chars). Nota opcional. `pendiente|rechazada → firmada` (permite arrepentirse de una novedad). Plan cancelado → 400.
- **Rechazar (novedad):** solo el propio participante; motivo obligatorio (se guarda en `nota`); `pendiente → rechazada`. Una firma `firmada` no se puede rechazar. Plan cancelado → 400.
- **Estados del plan/tarea:** sin cambios (misma derivación y porcentajes).

## Endpoints (controller `api/Implementacion`)

| Método | Ruta | Descripción |
|---|---|---|
| PUT | `tareas/{id}/participantes` | Sincroniza lista de userIds participantes → TareaDto con firmas |
| POST | `tareas/{id}/firmar` | `{firmaTexto, nota}` firma del usuario actual |
| POST | `tareas/{id}/rechazar` | `{motivo}` novedad del usuario actual |
| GET | `mis-firmas` | Firmas del usuario actual (pendientes + historial) |

Ampliados: `GET planes` y `GET planes/{id}` (tipo, encargado, creador+emails, firmas por tarea), `usuarios-asignables` (+email), create/update plan (+tipo, +implementadorUserId).

## Archivos a crear/modificar

**Backend**
- `Domain/Entities/Implementacion/ImplementacionPlan.cs` (+3 props +navs) · `ImplementacionTarea.cs` (+nav Firmas) · **nuevo** `ImplementacionTareaFirma.cs`
- `Infrastructure/Persistence/Configurations/Implementacion/` → PlanConfiguration (columnas/FK nuevas), **nuevo** `ImplementacionTareaFirmaConfiguration.cs`
- `Infrastructure/Persistence/ZooSanMarinoContext.cs` (+DbSet ImplementacionTareaFirmas)
- `Application/DTOs/Implementacion/ImplementacionDtos.cs` (ampliar + nuevos records)
- `Application/Calculos/ImplementacionCalculos.cs` (+ tipos plan, estados firma, `ResumenFirmas`, `PuedeResponderFirma`, `ValidarFirmaTexto`)
- `Application/Interfaces/IImplementacionService.cs` (+4 métodos)
- `Infrastructure/Services/Implementacion/` → ancla (helpers email/nombres, MapTarea con firmas), Planes (tipo/implementador/creador), **nuevo** `Funciones/ImplementacionService.Firmas.cs`, Consultas (+email en usuarios asignables, mis-firmas)
- `API/Controllers/ImplementacionController.cs` (+endpoints)
- `Infrastructure/Migrations/` **nueva** `AddImplementacionFirmasYTipo` (SQL crudo idempotente)
- `tests/ZooSanMarino.Application.Tests/ImplementacionCalculosTests.cs` (+casos nuevos)

**Frontend (`features/implementacion/`)**
- `models/implementacion.models.ts` (ampliar) · `services/implementacion.service.ts` (+métodos)
- `funciones/` → **nuevos** `filtrar-planes.funcion.ts`, `filtrar-tareas.funcion.ts`, `resumen-firmas.funcion.ts` (puras)
- **nuevo** `styles/implementacion-shared.scss` (filters-row/filter-group/chips/estados, tokens del tema)
- `pages/planes-list/` rediseño (header, card filtros búsqueda+tipo+estado, tabla, error+Reintentar, timeout)
- `pages/plan-detail/` rediseño (cabecera con tipo/encargado/creador, KPIs+firmas, filtros, firmas por tarea, modal participantes, modal firmas)
- `pages/mis-tareas/` + sección "Por firmar" (mis-firmas) + modal firmar/novedad con guía a `/tickets/nuevo`
- `components/modal-plan/` (+tipo, +encargado con "implementador diferente", muestra creador) · `components/modal-tarea/` (estilo) · **nuevos** `modal-participantes/`, `modal-firmas/`, `modal-firmar/`

## Casos de prueba

**xUnit (cálculo puro):** tipos de plan válidos/incorrectos · `ResumenFirmas` (0 participantes, mixto, redondeo 1 decimal AwayFromZero) · `PuedeResponderFirma` (pendiente→firmar ok, rechazada→firmar ok, firmada→rechazar no, plan cancelado no, no-participante no) · `ValidarFirmaTexto` (vacío/corto/largo/trim).
**Manuales/smoke:** crear cronograma tipo capacitación con encargado distinto → detalle muestra creador y encargado con correos · asignar 2 participantes → firmar uno (firma+nota) y rechazar otro (novedad) → detalle muestra firmas/novedad · mis-firmas muestra pendiente e historial · quitar participante firmado → 400 · filtros de lista/detalle filtran al instante · 401 muestra "sesión vencida" (no spinner infinito).

## Validación

`dotnet build` (Domain/Application/Infrastructure por proyecto para no chocar con el backend corriendo del usuario; API con `-o` scratch si hay lock) + `dotnet test` · migración probada local (`dotnet ef database update` con startup Infrastructure, BD compartida :5433 — cambios solo aditivos) · `yarn build` con Node portable · sin procesos huérfanos.
