# Plan — ItalJira: historias, tareas y tiempos fuera del módulo de Tickets

**Fecha:** 2026-08-07
**Pedido del usuario (literal, resumido):**

> «En el módulo de ticket, donde recibo el ticket y gestiono la aplicación, necesito que este módulo
> esté bien acomodado: que gestione los tiempos, tareas, historias de casos, etc. Cuando lo crea un
> usuario es una TAREA SIN HISTORIA; pero si es una HISTORIA es un proceso que realizo manual desde
> el área de desarrollo. Puedo crear una historia que se llama "Módulo de ticket" y dentro de ella
> tendré muchas tareas: creo la tarea "Gestión de usuario" y dentro de ella tendré muchas subtareas,
> bugs o fix o lo que se encuentre en el desarrollo de esa tarea — ese es el flujo completo.
> Este módulo de administrador será por fuera, se llamará **ItalJira**: es un centralizador de
> gestión. En Tickets dejamos solo **Mis solicitudes** y **Gestionar**; lo demás se saca a ItalJira.
> También quiero **otra migración** con las historias y tareas que YA he desarrollado en la
> aplicación (las que nunca pasaron por un ticket), con el flujo completo hasta CERRADO, creadas y
> asignadas a `moiesbbuga@gmail.com`, para que producción tenga información real de lo que se hizo.»

**Decisiones confirmadas por el usuario (07-ago-2026):**

| # | Decisión | Elegida |
|---|---|---|
| **D1** | Modelo de la historia | **Tabla nueva `historias`** — 3 niveles reales (historia → tarea → subtarea/bug). `ticket_tareas.ticket_id` pasa a NULLABLE y suma `historia_id`. |
| **D2** | Rutas | **Mover a `/italjira/*`** con redirect desde las `/tickets/*` viejas. |
| **D3** | Alcance del histórico sembrado | **Mixta**: ~20 historias por módulo + una tarea por cada trabajo con plan propio en `fase_de_desarrollo/` (~150), con fechas reales de git. |

---

## 1. Estado actual (auditado, no supuesto)

**Backend**
- `Ticket` (caso) — `tickets`: código `TK-YYYY-NNNNNN`, tipo, estado (máquina de 9 estados con
  transiciones válidas en `TicketEstados`), prioridad, orden de tablero, horas estimadas, fecha
  límite (SLA) y fechas plan de roadmap. Solicitante delegado.
- `TicketTarea` — `ticket_tareas`: **`ticket_id NOT NULL`** (toda tarea vive dentro de un caso),
  `parent_tarea_id` para subtareas, tipo `TAREA|HISTORIA|BUG|SUBTAREA|DOCUMENTACION|MEJORA`, estado
  de 7 columnas (`TicketTareaEstados`), prioridad, asignado, orden, estimación, fechas plan y reales.
- `TicketTiempo` — `ticket_tiempos`: worklog por caso o por tarea, borrado lógico.
- Servicios: `TicketService` (partials `Gestion`/`Indicadores`) y `TicketTareaService` (partial
  ancla + `Tiempos`), cálculo puro en `Application/Calculos/Ticket*Calculos.cs`.
- Controllers: `TicketsController`, `TicketTareasController`, `TicketPerfilesController`.

**Frontend** — `features/tickets/`: páginas `mis-tickets`, `ticket-create`, `mis-asignados`,
`gestion-tickets`, `admin-tickets`, `tablero`, `roadmap`, `panel`, `ticket-detalle`; componentes
`tareas-panel`, `tarea-modal`, `worklog-panel`, `ticket-filtros`, badges y stepper.

**Menús en BD** (`menus`, grupo `tickets` id 55 en local):

| key | label | route | queda en |
|---|---|---|---|
| `tickets.mis` | Mis solicitudes | `/tickets` | **Tickets** |
| `tickets.gestion` | Bandeja de gestión | `/tickets/gestion` | **Tickets** |
| `tickets.admin` | Administración | `/tickets/admin` | → ItalJira |
| `tickets.tablero` | Tablero de casos | `/tickets/tablero` | → ItalJira |
| `tickets.roadmap` | Roadmap | `/tickets/roadmap` | → ItalJira |
| `tickets.panel` | Panel de control | `/tickets/panel` | → ItalJira |

**Lo que falta para el flujo pedido:** no existe el nivel HISTORIA, y **toda** tarea exige un ticket
⇒ hoy es imposible registrar trabajo nacido en el área de desarrollo.

---

## 2. Modelo objetivo

```
HISTORIA  «Módulo de ticket»                        historias
   ├── TAREA     «Gestión de usuario»               ticket_tareas (historia_id = H, ticket_id NULL)
   │      ├── SUBTAREA «Formulario de alta»         ticket_tareas (parent_tarea_id = T)
   │      └── BUG      «No refresca el listado»     ticket_tareas (parent_tarea_id = T)
   └── CASO      TK-2026-000123 (de un usuario)     tickets (historia_id = H)

SIN HISTORIA (bandeja de entrada de ItalJira)
   ├── CASO   TK-2026-000124  ← lo creó un usuario  tickets (historia_id NULL)
   └── TAREA  «Fix rápido»    ← nacida en desarrollo ticket_tareas (historia_id NULL, ticket_id NULL)
```

**Regla del pedido:** lo que crea un usuario entra **como tarea sin historia**; el área de
desarrollo lo arrastra dentro de una historia cuando corresponde. Una historia se crea **solo** desde
ItalJira (manual, área de desarrollo / requerimientos / administrador).

### 2.1 DDL

**Tabla nueva `historias`** (una fila = una épica):

| columna | tipo | nota |
|---|---|---|
| `id` | bigint identity | |
| `codigo` | varchar(40) | `HIS-YYYY-NNNN`, generado en backend |
| `pais_id` | integer | de `ICurrentUser`, nunca del body |
| `titulo` | varchar(200) NOT NULL | |
| `descripcion` | text | |
| `estado` | varchar(20) NOT NULL DEFAULT 'BACKLOG' | mismas 7 columnas que las tareas (`TicketTareaEstados`) — **un solo vocabulario** |
| `prioridad` | varchar(20) NOT NULL DEFAULT 'MEDIA' | `TicketPrioridades` |
| `responsable_user_guid` | uuid | |
| `orden` | integer NOT NULL DEFAULT 0 | posición en su columna del tablero |
| `horas_estimadas` | numeric(8,2) | |
| `fecha_inicio_plan` / `fecha_fin_plan` | date | barra del roadmap |
| `fecha_inicio_real` / `fecha_fin_real` | timestamptz | sellado por estado |
| `etiquetas` | varchar(300) | |
| auditoría | `company_id`, `created_by_user_id`, `created_at`, `updated_by_user_id`, `updated_at`, `deleted_at` | `AuditableEntity` |

**Cambios aditivos:**
- `ticket_tareas.ticket_id` → **DROP NOT NULL** (tarea nacida en desarrollo).
- `ticket_tareas.historia_id bigint NULL` + FK → `historias(id) ON DELETE SET NULL` + índice.
- `tickets.historia_id bigint NULL` + FK → `historias(id) ON DELETE SET NULL` + índice.
- `ticket_tiempos.ticket_id` → **DROP NOT NULL** (imputar horas a una tarea sin caso) + `historia_id`
  no hace falta: el tiempo se imputa a la tarea y la tarea conoce su historia.

**Invariante nuevo (CHECK, idempotente):** una fila de `ticket_tareas` tiene **al menos uno** de
`ticket_id` / `historia_id` / `parent_tarea_id` no nulo — nunca una tarea huérfana de los tres.

### 2.2 Compatibilidad — lo que NO cambia

- Las 10 tareas y los tickets existentes conservan su `ticket_id`: la columna solo deja de ser
  obligatoria. **Cero filas se tocan.**
- `TicketEstados` (máquina del caso) intacta; el ticket sigue cerrándose por su propio flujo.
- `ITicketTareaService` conserva sus firmas actuales ⇒ el detalle del caso, el panel de tareas y el
  worklog siguen funcionando byte a byte igual.

---

## 3. Backend — archivos

| Acción | Archivo | Contenido |
|---|---|---|
| NUEVO | `Domain/Entities/Historia.cs` | Entidad + `HistoriaEstados` (alias de las 7 columnas) |
| EDITA | `Domain/Entities/TicketTarea.cs` | `TicketId` → `long?`, `+ HistoriaId` |
| EDITA | `Domain/Entities/Ticket.cs` | `+ HistoriaId` |
| EDITA | `Domain/Entities/TicketTiempo.cs` | `TicketId` → `long?` |
| NUEVO | `Infrastructure/Persistence/Configurations/HistoriaConfiguration.cs` | mapeo snake_case |
| EDITA | `…/TicketTareaConfiguration.cs`, `TicketConfiguration.cs`, `TicketTiempoConfiguration.cs` | columnas nuevas / nullability |
| EDITA | `Infrastructure/Persistence/ZooSanMarinoContext.cs` | `DbSet<Historia> Historias` |
| NUEVO | `Application/Calculos/HistoriaCalculos.cs` | **puro**: `GenerarCodigo`, `SiguienteConsecutivo`, `NormalizarEstado/Prioridad`, `SellarFechasReales`, `AvancePorTareas`, `RangoPlanDerivado` |
| NUEVO | `Application/DTOs/Tickets/HistoriaDtos.cs` | `CreateHistoriaRequest`, `UpdateHistoriaRequest`, `MoverHistoriaRequest`, `HistoriaDto`, `HistoriaDetalleDto`, `ItalJiraBacklogDto`, `ItalJiraRoadmapDto`, `AsignarAHistoriaRequest` |
| NUEVO | `Application/Interfaces/IHistoriaService.cs` | puerto de `historias` |
| NUEVO | `Infrastructure/Services/ItalJira/HistoriaService.cs` (ancla) | CRUD + visibilidad + identidad |
| NUEVO | `…/ItalJira/Funciones/HistoriaService.Backlog.cs` | árbol backlog, tablero, roadmap, indicadores |
| NUEVO | `…/Tickets/Funciones/TicketTareaService.Historias.cs` | **partial del mismo servicio**: tareas por historia y tareas sueltas (único escritor de `ticket_tareas`) |
| EDITA | `Application/Interfaces/ITicketTareaService.cs` | métodos historia-scoped (aditivos) |
| NUEVO | `API/Controllers/HistoriasController.cs` | `/api/Historias` + `/api/Historias/{id}/tareas` + `backlog` / `roadmap` / `tablero` |
| EDITA | `API/Program.cs` | DI de `IHistoriaService` |

**Permisos:** ItalJira es el área de desarrollo ⇒ gate `tickets.gestionar` + `tickets.admin` (los
mismos que hoy protegen tablero/roadmap/panel). No se inventan permisos nuevos: se reutilizan para
que los roles ya configurados no queden fuera tras el deploy.

⚠️ **WAF**: ninguna ruta nueva contiene `admin` (AWS WAF devuelve 403 a cualquier path de API con esa
palabra — incidente documentado). La administración de resolutores se expone como
`/italjira/configuracion` en el front; su endpoint sigue siendo el actual de perfiles.

---

## 4. Frontend — archivos

```
features/italjira/
├── italjira.routes.ts
├── models/         historia.models.ts  (+ re-export de los tipos de tarea que ya existen)
├── services/       historia.service.ts
├── funciones/      exportar-italjira-excel.funcion.ts, agrupar-backlog.funcion.ts   (puras)
├── components/     historia-modal/, historia-card/, backlog-arbol/
└── pages/          backlog/  tablero/  roadmap/  panel/  configuracion/  mis-asignados/
```

- Las páginas `tablero`, `roadmap`, `panel`, `admin-tickets`, `mis-asignados` **se mudan**
  físicamente desde `features/tickets/` (traslado literal + imports reajustados; refactor ≠ cambio de
  comportamiento).
- `features/tickets/` queda con: `mis-tickets`, `ticket-create`, `ticket-detalle`, `gestion-tickets`
  y los componentes compartidos. Los componentes que usan las dos features (`ticket-filtros`,
  badges, `tareas-panel`, `tarea-modal`, `worklog-panel`) **se quedan donde están** y ItalJira los
  importa (evita duplicar UI).
- `tickets.routes.ts`: `tablero|roadmap|panel|admin|asignados` → `redirectTo` a `/italjira/...`.
- `app.config.ts`: nueva ruta lazy `italjira`.
- **Todo componente nuevo lleva `changeDetection: ChangeDetectionStrategy.Eager` explícito**
  (regla #1 del repo: en Angular 22 omitirlo = OnPush ⇒ modal colgado en «Cargando…»).
- Notificaciones con `ToastService`, confirmaciones con `ConfirmDialogService`, Excel con
  `shared/utils/excel/exportar-tabla-excel.funcion.ts`. Prohibido `alert`/`confirm`/`XLSX` inline.

### Página nueva — Backlog (el corazón del módulo)
Árbol de 3 niveles con progreso por historia:

```
▾ HIS-2026-0001  Módulo de ticket            ██████░░░░ 60 %   12/20 tareas   48 h
   ▾ T  Gestión de usuario          EN_CURSO   Moises   8 h / 12 h
        ├ SUB  Formulario de alta            LISTO
        └ BUG  No refresca el listado        EN_CURSO
   └ CASO TK-2026-000123  Error al guardar   SOLUCIONADO
▾ SIN HISTORIA (bandeja de entrada)
   └ CASO TK-2026-000124  Duda de inventario  ABIERTO      [Mover a historia ▾]
```

---

## 5. Migraciones

| # | Nombre | Tipo | Contenido |
|---|---|---|---|
| M1 | `20260807190000_AddHistoriasItalJira` | DDL idempotente | `historias` + `historia_id` en tareas y tickets + `ticket_id`/`ticket_tiempos.ticket_id` nullable + índices + CHECK |
| M2 | `20260807191000_MenusItalJiraFueraDeTickets` | data-only | Grupo `italjira` + **UPDATE en sitio** de `tickets.tablero/roadmap/panel/admin` → `italjira.*` (conserva `role_menus`/`company_menus`/`menu_permissions` porque se identifican por `menu_id`) + menú nuevo `italjira.backlog` copiado a los roles/empresas que ya ven las vistas de gestión |
| M3 | `20260807192000_SeedHistorialDesarrolloItalJira` | data-only | El histórico real (§6) |

Todas: `Designer` clonado, **ModelSnapshot solo tocado por M1** (es la única con cambio de modelo),
`IF NOT EXISTS` / `WHERE NOT EXISTS`, `Down()` reversible.

⚠️ M2 usa `UPDATE ... WHERE key = 'tickets.tablero'` — si en prod el menú no existe, el UPDATE afecta
0 filas y no rompe nada; el `INSERT ... WHERE NOT EXISTS` de respaldo lo crea.

---

## 6. M3 — el histórico real (lo que se desarrolló sin ticket)

**Fuente de verdad (no inventada):** los 198 documentos de `fase_de_desarrollo/` y sus fechas reales
de git (`git log --diff-filter=A` para el alta, `git log -1` para el último toque), cruzados con
`tracker_estado.md`.

**Agrupación en historias** (≈20, por módulo funcional):

| Historia | Alcance |
|---|---|
| Postura — Levante | seguimiento, cierre, arrastre de huevos, curva, alimentos múltiples |
| Postura — Producción | fn canónica, indicadores, espejo de huevos, clasificación |
| Pollo de engorde | seguimiento, saldos de alimento, cuadres, mixto Panamá |
| Reproductoras | lote, cruce, confirmación, edades |
| Liquidación y cierre | engorde, producción, congelamiento |
| Inventario y gastos | unificación Colombia, scoping multiempresa, gastos |
| Carga masiva / Migraciones masivas | levante, producción, engorde, alimento |
| Movimientos de aves y traslados | traslados, cohortes, recepción de tránsito |
| Reportes e informes | RA pesadas, técnico semanal, contable, costos |
| Guías genéticas | Panamá Ross 308 AP, Colombia, uniformidad |
| Tickets y soporte | el módulo completo hasta el tablero tipo Jira |
| ItalJira | **esta** entrega |
| Implementación | checklist de entrega por empresa |
| Vacunación | cronograma y mejora integral |
| Seguridad y sesión | login, rate limiting, sesión deslizante, alcance por granja |
| Usuarios, roles y menús | plataforma, admin de empresa, alcance granular |
| Multi-empresa | Santa Reyes, flags por empresa, Panamá |
| Diseño y UX | design system, filtros unificados, paleta |
| Plataforma y upgrades | Angular 22, .NET 10, PWA, deploy/CI |
| Integraciones | Puente Panamá, correo, DB Studio |

**Cada tarea** lleva: título derivado del plan, `tipo` según su naturaleza (`fix_*` → **BUG**,
`refactor_*` → **MEJORA**, `CONTEXTO_*`/`diccionario_*`/`*_spec` → **DOCUMENTACION**, resto →
**TAREA**), `estado = LISTO`, `fecha_inicio_real` / `fecha_fin_real` de git, `fecha_inicio_plan` /
`fecha_fin_plan` iguales a las reales, `descripcion` con el enlace al archivo del plan y
`etiquetas` con el módulo.

**Identidad:** el usuario se resuelve **por email**
(`logins.email = 'moiesbbuga@gmail.com'` → `user_logins.user_id`), nunca por un guid fijo (los ids
difieren local ↔ prod). `created_by_user_id` = su cédula. Empresa/país = los del ticket más reciente,
o los primeros activos si no hay tickets.

**Idempotencia:** `WHERE NOT EXISTS` por `codigo` (`HIS-2025-0001…`) y por
`(historia_id, titulo)` en las tareas. Correrla dos veces no duplica nada.

**Estado final:** historias y tareas en `LISTO`, con `fecha_fin_real` — «el flujo completo hasta
cerrado» que pidió el usuario. Las historias todavía en curso (ItalJira, Postura Producción) quedan
en `EN_CURSO` para no mentir.

---

## 7. Reglas de negocio

1. Una **historia** solo se crea desde ItalJira (área de desarrollo). Los usuarios finales no la ven.
2. Un **ticket** de usuario nace SIN historia. Aparece en el backlog de ItalJira en la bandeja
   «Sin historia» y se puede mover a una historia; moverlo **no** altera el estado del caso.
3. Una **tarea** puede colgar de una historia, de un caso, o de las dos. Una **subtarea/bug** cuelga
   siempre de una tarea (`parent_tarea_id`), y hereda su historia para el árbol.
4. Mover tarjetas **no** cambia el estado del caso: el caso conserva su máquina de estados.
5. **Avance de la historia** = tareas vivas en `LISTO` ÷ total de tareas vivas (subtareas incluidas).
   Sin tareas ⇒ avance por su propio estado (0 % o 100 %).
6. **Horas**: la historia suma las horas de sus tareas y de los casos que agrupa; nunca se registran
   horas directamente sobre la historia (evita el doble conteo).
7. Borrar una historia es **lógico** y **no** borra sus tareas: las devuelve a «sin historia».
8. Fechas reales: `EN_CURSO` sella `fecha_inicio_real` (una sola vez), `LISTO` sella
   `fecha_fin_real`; salir de `LISTO` la limpia. Misma regla que las tareas hoy
   (`TicketTareaCalculos.SellarFechasReales`) — se reutiliza, no se reescribe.

---

## 8. Casos de prueba (xUnit — gate de CI)

`tests/ZooSanMarino.Application.Tests/HistoriaCalculosTests.cs`

1. `GenerarCodigo` — primera historia del año ⇒ `HIS-2026-0001`; con huecos toma el máximo + 1.
2. `SiguienteConsecutivo` ignora códigos de otro año y códigos corruptos.
3. `NormalizarEstado/Prioridad` — nulo/vacío/desconocido ⇒ default; case-insensitive.
4. `SellarFechasReales` — a `EN_CURSO` sella inicio y no lo pisa al re-entrar; a `LISTO` sella fin;
   salir de `LISTO` lo limpia.
5. `AvancePorTareas` — 0 tareas ⇒ por estado propio; 3/5 ⇒ 60 %; todas listas ⇒ 100 %; las
   eliminadas no cuentan.
6. `RangoPlanDerivado` — mínimo de inicios y máximo de fines de las tareas; sin fechas ⇒ null.
7. **Retrocompatibilidad**: una tarea con `ticket_id` y sin `historia_id` se proyecta igual que hoy.
8. `TicketTareaCalculos.Reordenar` sigue verde con tareas sin ticket (mismo cálculo, otro universo).

**Smoke HTTP** (backend propio en `PORT=5499`, JWT + `X-Secret-Up` minteados):
crear historia → crear tarea dentro → crear subtarea y bug → registrar horas → mover a `LISTO` →
verificar avance 100 % → mover un ticket existente a la historia → backlog y roadmap devuelven el
árbol correcto → borrar la historia y comprobar que las tareas quedan sin historia.

**Smoke UI** (front `:4200` + back `:5002`, sesión inyectada en `localStorage.auth_session`):
el menú ItalJira aparece con sus 5 items, Tickets queda con 2, el backlog pinta el árbol, los modales
**no se cuelgan en «Cargando…»** (abrir y cerrar dos veces) y las rutas viejas redirigen.

---

## 9. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| `ticket_id` nullable rompe consultas que lo asumen NOT NULL | Auditar todos los `t.TicketId ==` antes de compilar; EF avisa en build al cambiar a `long?`. Los tests de tareas existentes son la red. |
| Los menús mudados pierden permisos y nadie ve nada | **UPDATE en sitio** del `menus.id` (no delete+insert): `role_menus` y `company_menus` apuntan al mismo id ⇒ conservan la asignación. Verificado con la migración `SeedMenuPanelIndicadoresTickets` como referencia. |
| El seed histórico corre antes de que exista el usuario en prod | El INSERT es `SELECT … WHERE EXISTS (usuario)`: si no está, siembra 0 filas y no rompe el arranque; se re-corre después. Idempotente. |
| Doble conteo de horas historia/caso | Regla 6: la historia agrega, nunca registra. |
| Sesiones paralelas | Bloque propio al FINAL de `tracker_estado.md`; migraciones con timestamp nuevo; no se toca ningún archivo de otro bloque abierto. |

---

## 10. Orden de ejecución

1. **F1 Backend datos** — entidad, configurations, context, migración M1, aplicar en local.
2. **F2 Backend lógica** — `HistoriaCalculos` + tests, DTOs, interfaz, servicios, controller, DI.
3. **F3 Menús** — migración M2 + verificación en BD local.
4. **F4 Frontend** — feature `italjira`, mudanza de páginas, redirects, backlog nuevo.
5. **F5 Histórico** — extracción de fechas de git, curado y migración M3.
6. **F6 Validación** — `dotnet build` + `dotnet test` + `yarn build` + smoke HTTP + smoke UI +
   apagar todo lo que se levante.
