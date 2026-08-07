# Plan — Tickets como CASOS tipo Jira: tareas, tablero, tiempos y solicitante delegado

**Fecha:** 2026-08-06
**Módulo:** `tickets` (backend `ZooSanMarino.*` + frontend `features/tickets`)
**Pedido del usuario (literal, resumido):**
1. En *Mis solicitudes* poder indicar **de qué usuario del sistema viene la solicitud** (el admin
   resuelve casos que ningún usuario montó en la aplicación).
2. Un **módulo tipo Jira** que tome los tickets como **casos**, permita crear **tareas/historias**,
   moverlas como en Jira, con **tiempos de solución** y **fases de desarrollo** (análisis,
   documentación, en revisión, solucionado, cerrado). Solo para perfil **admin**
   (`tickets.admin` — usuario `moiesbbuga@gmail.com`).
3. *Mis solicitudes* más **profesional**, con **línea de tiempo** por caso y mejor UX.

**Decisiones tomadas con el usuario (2026-08-06):**
- **Fases:** *las dos cosas* → se amplía la máquina de estados del caso **y** se agrega el tablero de tareas.
- **Solicitante delegado:** **solo `tickets.admin`** puede crear a nombre de otro usuario.
- **Entrega:** **todo de una**.

---

## 1. Enfoque arquitectónico

**Regla rectora: aditivo, nunca destructivo.** El módulo ya está en producción con correos, doble
cierre y perfiles de resolutor. Todo lo nuevo entra como columnas *nullable* / con default neutro y
tablas nuevas; ningún flujo existente cambia de comportamiento cuando los campos nuevos están vacíos.

| Capa | Qué se hace |
|---|---|
| **Domain** | `Ticket` gana campos de gestión (prioridad, planificación, solicitante delegado). `TicketEstados` suma `EN_DOCUMENTACION` y `EN_REVISION` **sin quitar ninguna transición previa**. Entidades nuevas `TicketTarea` y `TicketTiempo`. |
| **Application** | DTOs nuevos + **cálculo puro** en `Calculos/` (métricas/SLA, línea de tiempo, reordenamiento kanban) con tests xUnit. |
| **Infrastructure** | Configurations, `TicketTareaService` (partial en `Funciones/`), extensiones puntuales de `TicketService`. |
| **API** | `TicketTareasController` nuevo + endpoints de gestión en `TicketsController`. **Ninguna ruta con `admin`** (WAF `AdminProtection` devuelve 403 → se usa `global`/`tablero`). |
| **Frontend** | Páginas nuevas `tablero` (kanban CDK) y `roadmap` (gantt), rediseño de `mis-tickets` y `ticket-detalle`, selector de solicitante en `ticket-create`. |

**Por qué la línea de tiempo se DERIVA y no se persiste:** una tabla de eventos obligaría a un
backfill de los tickets históricos (o a mostrarlos vacíos). En cambio se calcula fusionando lo que ya
existe —`created_at`, `fecha_primera_apertura`, notas con `estado_resultante`, `fecha_solucion`,
`fecha_cierre_solicitante`, adjuntos, tareas y worklogs—. Cero backfill, los casos viejos se ven
completos desde el primer deploy. El armado es **función pura** → `TicketTimelineCalculos` + tests.

---

## 2. Base de datos — 1 migración EF idempotente

`20260806XXXXXX_AddTicketsJiraCasosTareas`

### 2.1 `tickets` — columnas nuevas (`ADD COLUMN IF NOT EXISTS`)

| Columna | Tipo | Default | Para qué |
|---|---|---|---|
| `solicitante_user_guid` | `uuid` NULL | NULL | Usuario del sistema a nombre de quien va el caso |
| `solicitante_user_id` | `int` NULL | NULL | Cédula espejo — permite que el solicitante lo vea en *Mis solicitudes* |
| `prioridad` | `varchar(20)` NOT NULL | `'MEDIA'` | BAJA / MEDIA / ALTA / CRITICA |
| `orden_tablero` | `int` NOT NULL | `0` | Posición de la tarjeta dentro de su columna |
| `horas_estimadas` | `numeric(8,2)` NULL | NULL | Estimación del caso |
| `fecha_limite` | `timestamptz` NULL | NULL | Compromiso de solución (base del SLA) |
| `fecha_inicio_plan` | `date` NULL | NULL | Barra del roadmap |
| `fecha_fin_plan` | `date` NULL | NULL | Barra del roadmap |

> `estado` es `varchar(20)`: `EN_DOCUMENTACION` (16) y `EN_REVISION` (11) entran sin tocar el tipo.

### 2.2 `ticket_notas` — columna nueva
`tipo_evento varchar(30) NULL` — clasifica las notas de sistema (`SISTEMA_ASIGNACION`,
`SISTEMA_PRIORIDAD`, `SISTEMA_TAREA`, `SISTEMA_PLANIFICACION`, `SISTEMA_SOLICITANTE`).
**NULL = comentario humano** ⇒ todas las notas existentes conservan su significado.

### 2.3 `ticket_tareas` (`CREATE TABLE IF NOT EXISTS`)
`id`, `ticket_id` FK→tickets ON DELETE CASCADE, `codigo varchar(40)`, `tipo varchar(20)`
(TAREA/HISTORIA/BUG/SUBTAREA/DOCUMENTACION/MEJORA), `titulo varchar(200)`, `descripcion text`,
`estado varchar(20)` (BACKLOG/ANALISIS/DOCUMENTACION/EN_CURSO/EN_REVISION/LISTO/BLOQUEADA),
`prioridad varchar(20)`, `asignado_user_guid uuid`, `parent_tarea_id bigint`, `orden int`,
`horas_estimadas numeric(8,2)`, `fecha_inicio_plan date`, `fecha_fin_plan date`,
`fecha_inicio_real timestamptz`, `fecha_fin_real timestamptz`, `etiquetas varchar(300)`,
`company_id`, `created_by_user_id`, `created_at`, `updated_by_user_id`, `updated_at`, `deleted_at`.

### 2.4 `ticket_tiempos` (worklog)
`id`, `ticket_id` FK, `tarea_id` FK NULL, `user_guid uuid`, `user_id int`, `fecha date`,
`horas numeric(6,2)`, `descripcion varchar(500)`, `created_at`, `deleted_at`.

### 2.5 Índices (`CREATE INDEX IF NOT EXISTS`)
`ix_tickets_solicitante_user_id`, `ix_tickets_prioridad`,
`ix_ticket_tareas_ticket_id`, `ix_ticket_tareas_estado`, `ix_ticket_tareas_asignado`,
`ix_ticket_tiempos_ticket_id`, `ix_ticket_tiempos_tarea_id`.

### 2.6 Seed de menú (idempotente, `WHERE NOT EXISTS`)
`tickets.tablero` → `/tickets/tablero` y `tickets.roadmap` → `/tickets/roadmap`, ambos con
`menu_permissions` contra `tickets.admin`. Localización por `route`/`key`, nunca por id fijo.

---

## 3. Máquina de estados ampliada (Domain — una sola fuente de verdad)

Estados: `ABIERTO` · `EN_ANALISIS` · **`EN_DOCUMENTACION`** · `EN_IMPLEMENTACION` ·
**`EN_REVISION`** · `SOLUCIONADO` · `CERRADO` · `TRANSFERIDO` · `SUSPENDIDO`.

Flujo lineal del stepper:
`ABIERTO → EN_ANALISIS → EN_DOCUMENTACION → EN_IMPLEMENTACION → EN_REVISION → SOLUCIONADO → CERRADO`

Las **4 fases de trabajo** (`EN_ANALISIS`, `EN_DOCUMENTACION`, `EN_IMPLEMENTACION`, `EN_REVISION`) se
mueven **libremente entre sí** (es lo que hace un tablero con drag & drop) y todas pueden ir a
`SOLUCIONADO`, `SUSPENDIDO` y `TRANSFERIDO`. **Invariante de no-regresión:** toda transición válida
hoy sigue siendo válida (test dedicado que las recorre una por una).

`CERRADO` sigue siendo exclusivo de `ConfirmarCierre` (lo confirma el solicitante) y sigue siendo
terminal. La regla «el solicitante no gestiona su propio ticket» no cambia.

---

## 4. Solicitante delegado ("a nombre de")

- `CreateTicketRequest` gana `SolicitanteUserGuid` **al final y con default `null`** (record
  posicional: agregar al final es lo único compatible).
- **Gate:** si viene informado y el usuario actual **no** tiene `tickets.admin` ⇒
  `InvalidOperationException`. Sin el campo, el comportamiento es byte a byte el de hoy.
- Al persistir: `SolicitanteUserGuid` + `SolicitanteUserId` (cédula resuelta del usuario destino).
  `CreatedByUserGuid`/`CreatedByUserId` **siguen siendo quien registró** ⇒ la trazabilidad de "quién
  lo montó" no se pierde y la UI muestra *"Registrado por X en nombre de Y"*.
- **Visibilidad:** `SearchMisTicketsAsync` pasa a `created_by = yo OR solicitante = yo`, y
  `PuedeVerTicketAsync` suma al solicitante delegado. Así el usuario a nombre de quien se creó lo ve
  y puede confirmar el cierre.
- **Cierre y correos:** `EsCreador` pasa a considerar también al solicitante delegado (es quien debe
  confirmar el cierre) y `ResolveSolicitanteEmailAsync` resuelve primero el delegado ⇒ los correos de
  solución/cierre van a quien reportó, no a quien registró.

---

## 5. Cálculo puro (`Application/Calculos/`) + tests xUnit

| Archivo | Responsabilidad |
|---|---|
| `TicketMetricasCalculos.cs` | Tiempo de primera respuesta, tiempo de resolución, tiempo por estado (a partir de las notas con `estado_resultante`), estado del SLA vs `fecha_limite` (EN_TIEMPO / POR_VENCER / VENCIDO), % de avance por tareas, horas estimadas vs registradas. |
| `TicketTimelineCalculos.cs` | Fusiona ticket + notas + adjuntos + tareas + worklogs en una lista ordenada de eventos tipados para la línea de tiempo. |
| `TicketTareaCalculos.cs` | Reordenamiento del kanban al soltar una tarjeta (recalcula `orden` de la columna origen y destino), validación de estado/tipo/prioridad, generación del código `TK-...-T{n}`. |

Tests en `backend/tests/ZooSanMarino.Application.Tests/`:
`TicketEstadosTransicionesTests.cs` (no-regresión de la máquina), `TicketMetricasCalculosTests.cs`,
`TicketTimelineCalculosTests.cs`, `TicketTareaCalculosTests.cs`.

---

## 6. API — endpoints nuevos (ninguno con `admin` en la ruta)

**`TicketsController`**
- `GET  /api/tickets/tablero` — casos agrupados por estado (kanban del admin).
- `GET  /api/tickets/roadmap` — casos con fechas planificadas + tareas (gantt).
- `GET  /api/tickets/{id}/timeline` — línea de tiempo unificada.
- `GET  /api/tickets/{id}/metricas` — SLA y tiempos del caso.
- `GET  /api/tickets/solicitantes` — usuarios candidatos a solicitante (solo `tickets.admin`).
- `PATCH /api/tickets/{id}/prioridad` · `PATCH /api/tickets/{id}/planificacion` ·
  `PATCH /api/tickets/{id}/asignado` · `POST /api/tickets/{id}/mover`.

**`TicketTareasController`** (`/api/tickets/{ticketId}/tareas`)
- `GET` listar · `POST` crear · `PUT /{tareaId}` editar · `DELETE /{tareaId}` (lógico)
- `POST /{tareaId}/mover` (estado + orden, drag & drop)
- `GET|POST /{tareaId}/tiempos` y `POST /api/tickets/{ticketId}/tiempos` (worklog del caso)

---

## 7. Frontend

**Nuevo**
- `pages/tablero/` — Kanban con `@angular/cdk/drag-drop` (ya está `@angular/cdk` 22 en el package).
  Columnas = fases del caso; tarjeta = caso con prioridad, asignado, avance de tareas, chip de SLA.
  Filtros: tipo, prioridad, resolutor, país, empresa, año. Solo `tickets.admin`.
- `pages/roadmap/` — timeline tipo el screenshot de Jira: filas = casos, barras por fechas
  planificadas, tareas anidadas colapsables, marcador de "hoy".
- `components/ticket-timeline/` — línea de tiempo vertical con iconos por tipo de evento.
- `components/ticket-prioridad-badge/`, `components/ticket-sla-chip/`,
  `components/tarea-card/`, `components/tarea-modal/`, `components/worklog-panel/`.

**Rediseño**
- `mis-tickets` — tarjetas ricas (estado, progreso, SLA, última actividad) + línea de tiempo
  desplegable por caso + resumen superior por estado.
- `ticket-detalle` — layout tipo Jira: columna principal (descripción, tareas, adjuntos, actividad)
  + sidebar de detalles (estado, prioridad, solicitante, asignado, fechas, tiempos, SLA).
- `ticket-create` — selector de solicitante visible **solo** con `tickets.admin`.

**Obligatorio en todo componente/modal nuevo:** `changeDetection: ChangeDetectionStrategy.Eager`
(en Angular 22 omitirlo = OnPush ⇒ modal colgado en "Cargando…").
Toasts con `ToastService`, confirmaciones con `ConfirmDialogService`, formatos desde
`shared/utils/format.ts`. Prohibido `alert()`/`confirm()`/`XLSX` inline.

---

## 8. Casos de prueba

**Backend (xUnit)**
1. Toda transición válida antes del cambio sigue siendo válida (no-regresión).
2. Nuevas transiciones: las 4 fases de trabajo se mueven entre sí; `CERRADO` sigue terminal.
3. `SolucionadoRequiereDescripcion` intacto.
4. Métricas: SLA EN_TIEMPO / POR_VENCER / VENCIDO; sin `fecha_limite` ⇒ SIN_SLA.
5. Tiempo por estado a partir de notas desordenadas.
6. Timeline: orden cronológico, tipos correctos, caso viejo sin tareas ni worklogs se arma completo.
7. Kanban: reordenar dentro de la misma columna y mover entre columnas deja `orden` 0..n-1 sin huecos.
8. Código de tarea correlativo por caso.

**Smoke funcional**
9. Admin crea caso a nombre de otro usuario → ese usuario lo ve en *Mis solicitudes* y puede
   confirmar el cierre; el detalle muestra "registrado por".
10. Usuario **sin** `tickets.admin` que manda `solicitanteUserGuid` → rechazado.
11. Caso existente (creado antes del cambio) abre sin errores, muestra timeline y prioridad MEDIA.
12. Drag & drop en el tablero persiste estado y orden; recargar mantiene la posición.
13. Worklog suma horas y el avance del caso refleja las tareas LISTO.

---

## 9. Validación

- `cd backend && dotnet build` — 0 errores, sin advertencias nuevas.
- `cd backend && dotnet test` — todo verde (los tests nuevos incluidos).
- `dotnet ef database update` contra la BD local (`sanmarinoapplocal` en **:5433**) sin error.
- `cd frontend && yarn build` — 0 errores (único warning aceptado: bundle budget preexistente).
- Smoke UI con sesión en **localStorage** (no sessionStorage).
- Sin procesos huérfanos: backend/front de smoke detenidos al terminar.
