# Módulo «Gerencia»: Panel de control en modo solo-lectura global

**Fecha:** 2026-08-13
**Objetivo:** que un rol de gerencia vea **únicamente** el Panel de control de ItalJira, con los
indicadores de **todos** los casos, sin heredar ninguna de las facultades de `tickets.admin`.

---

## 1. Diagnóstico — por qué no alcanza con asignar el menú

El acceso al panel está gobernado por **tres capas independientes**, y solo dos son configurables
por datos:

| # | Capa | Dónde | Hoy exige |
|---|---|---|---|
| 1 | Sidebar | `RoleCompositeService.Menus_GetForUserAsync` (`RoleCompositeService.cs:631`) — arma el árbol con los `role_menus` del rol **+ sus ancestros**, y después filtra por `menu_permissions` | `tickets.gestionar` **o** `tickets.admin` |
| 2 | Ruta del front | `italjira.routes.ts:44` (`permissionGuard`) | `tickets.gestionar` **o** `tickets.admin` |
| 3 | **Alcance de datos** | `TicketService.Gestion.cs:326` `AplicarFiltroTablero` | **solo** `tickets.admin` |

```csharp
if (!EsSuperAdmin())                                   // EsSuperAdmin() == tiene 'tickets.admin'
    query = query.Where(x => x.AssignedToUserGuid == miGuid);   // fail-closed sin Guid
```

⇒ La capa 3 es la que rompe el caso de uso: un rol de gerencia con `tickets.gestionar` **ve el
panel pero con los casos asignados a esa persona** (para un gerente que no resuelve tickets, todo en
cero). Y darle `tickets.admin` para arreglarlo le concede además: crear casos a nombre de otro
(`TicketService.cs:110`), gestionar/mover/cerrar cualquier caso, el buscador de solicitantes, el
tablero y el roadmap globales, y la Configuración de ItalJira.

### Hallazgos que condicionan el diseño

1. **`menus.parent_id` es único** ⇒ la fila `italjira.panel` **no puede** colgar a la vez de ItalJira
   y de Gerencia. Hace falta una **fila nueva**.
2. **Ruta propia obligatoria.** Si la fila nueva reusara `/italjira/panel`, toda migración que
   localiza menús por `route` (convención del repo, porque los ids difieren local↔prod) pasaría a
   matchear **dos** filas. La nueva usa `/gerencia/panel`.
3. **`company_permissions` es fail-closed por empresa** (`CompanyPermissionCalculos.cs:152-154`,
   regla R1: *empresa sin configurar no habilita nada*). Un permiso nuevo que no se siembre ahí
   **no viaja en el JWT** aunque el rol lo tenga ⇒ el rol quedaría sin nada y parecería un bug.
4. **La barra de filtros del panel NO necesita `tickets.admin`**: `TicketFiltrosComponent` llama
   `/pais`, `/Company/global` y `/api/tickets/global/resolutores`; este último
   (`TicketService.cs:414`) **no tiene gate**. La barra funciona completa para el rol nuevo.
5. `AplicarFiltroTablero` la comparten **4 vistas**: tablero (`Gestion.cs:252`), roadmap
   (`Gestion.cs:414`), indicadores (`Indicadores.cs:27`) y reporte (`Indicadores.cs:110`). Abrir el
   alcance ahí sin distinguir abriría también tablero y roadmap por URL directa.

---

## 2. Enfoque arquitectónico

Permiso nuevo **`tickets.indicadores`** = *lectura global del Panel de control y su reporte, sin
capacidad de gestión*.

**La decisión de alcance se parametriza por vista**, no por endpoint suelto: `AplicarFiltroTablero`
recibe un flag `vistaSoloLectura` que **solo** activan `GetIndicadoresAsync` y `GetReporteAsync`.
Tablero y roadmap siguen exigiendo `tickets.admin` ⇒ el permiso nuevo **no** los abre ni por URL.

La regla es **lógica pura** en `Application/Calculos/` (regla del repo: math/decisión sin EF va a
Calculos, con tests xUnit), y el service solo la consulta.

```
TieneAlcanceGlobal(permisos, vistaSoloLectura) =
      permisos ∋ 'tickets.admin'
   || (vistaSoloLectura && permisos ∋ 'tickets.indicadores')
```

Con el permiso **ausente** el resultado es idéntico al de hoy en las 4 vistas (equivalencia byte a
byte exigida por CLAUDE.md).

### Por qué NO se hizo de otra forma

- **No** se creó un endpoint nuevo para el panel: duplicaría la proyección y el Excel dejaría de
  coincidir con el tablero (el mismo motivo por el que la barra de filtros es compartida).
- **No** se reusó `tickets.gestionar`: es el permiso del **resolutor** y su semántica actual
  («solo mis casos») es correcta; cambiarla movería el número de gente que hoy sí resuelve.

---

## 3. Archivos a crear / modificar

### Backend

| Archivo | Acción |
|---|---|
| `Application/Calculos/TicketAlcancePanelCalculos.cs` | **NUEVO** — `static class` con las keys y `TieneAlcanceGlobal` |
| `tests/ZooSanMarino.Application.Tests/TicketAlcancePanelCalculosTests.cs` | **NUEVO** — xUnit (gate CI) |
| `Infrastructure/Services/Tickets/Funciones/TicketService.Gestion.cs` | `AplicarFiltroTablero(filtro, bool vistaSoloLectura = false)` + delegar en Calculos |
| `Infrastructure/Services/Tickets/Funciones/TicketService.Indicadores.cs` | pasar `vistaSoloLectura: true` en las 2 llamadas |
| `Infrastructure/Migrations/<ts>_MenuGerenciaPanelControl.cs` | **NUEVO** — data-only, idempotente |

### Frontend

| Archivo | Acción |
|---|---|
| `features/gerencia/gerencia.routes.ts` | **NUEVO** — `/gerencia/panel` reutilizando `PanelComponent` |
| `app.config.ts` | **NUEVO** bloque lazy `path: 'gerencia'` con `authGuard` |
| `features/tickets/models/ticket.models.ts` | agregar `indicadores: 'tickets.indicadores'` a `TICKET_PERMS` |
| `features/italjira/pages/panel/panel.component.html` | revisar los `RouterLink` — si apuntan a vistas que el gerente no puede abrir, ocultarlos por permiso |

---

## 4. Cambios de BD (migración data-only, Designer clonado, ModelSnapshot intacto)

Todo idempotente (`WHERE NOT EXISTS`), localizando por `permissions.key` / `menus.key` /
`companies.name` — **nunca por id** (difieren local↔prod).

1. **Permiso**
   `INSERT INTO permissions (key, description)` → `tickets.indicadores`,
   *«ItalJira: ver el Panel de control (indicadores y reporte) de TODOS los casos, sin gestionarlos»*.
2. **Grupo `gerencia`** — `is_group=true`, `route=NULL`, `order=902` (justo después de ItalJira).
3. **Ítem `gerencia.panel`** — label «Panel de control», `route='/gerencia/panel'`, hijo del grupo.
4. **`menu_permissions`** de `gerencia.panel` → `tickets.indicadores` **y** `tickets.admin`
   (el admin lo sigue viendo si se le asigna; el gate es OR).
5. **`company_permissions`** — habilita `tickets.indicadores` en **toda empresa que ya tenga
   habilitado `tickets.admin` o `tickets.gestionar`** (o sea, donde el módulo ya existe). Sin esto el
   permiso no llega al JWT (hallazgo 3).

**Lo que la migración NO hace, a propósito:** no crea el rol «Gerencia», no inserta en
`role_permissions` ni en `role_menus`. Esa asignación se hace desde la pantalla de **Roles y
Permisos** (convención del repo — ver `24_permisos_botones_movimientos_pollo_engorde_plan.md:44`), y
así el usuario elige el rol y la empresa sin que la migración adivine nombres.

`Down()`: borra `role_menus`/`company_menus`/`menu_permissions` de los dos menús, los menús, las
filas de `role_permissions`/`company_permissions` del permiso y el permiso.

---

## 5. Reglas de negocio

- `tickets.admin` conserva **exactamente** lo que puede hoy (superset).
- `tickets.indicadores` concede alcance global **solo** en indicadores y reporte. En tablero,
  roadmap y en cualquier acción de escritura **no cambia nada**: sigue cayendo en «solo mis casos».
- Fail-closed intacto: sin `UserGuid` y sin permiso ⇒ `Where(_ => false)`.
- El permiso **no** habilita el menú de ItalJira ni ninguna de sus otras vistas.

---

## 6. Casos de prueba

### xUnit — `TicketAlcancePanelCalculosTests` (gate CI)

| # | Permisos | Vista | Esperado |
|---|---|---|---|
| 1 | *(ninguno)* | solo-lectura | `false` |
| 2 | *(ninguno)* | tablero | `false` |
| 3 | `tickets.gestionar` | solo-lectura | `false` ← **regresión**: hoy es así y debe seguir |
| 4 | `tickets.admin` | tablero | `true` |
| 5 | `tickets.admin` | solo-lectura | `true` |
| 6 | `tickets.indicadores` | **solo-lectura** | `true` ← comportamiento nuevo |
| 7 | `tickets.indicadores` | **tablero** | `false` ← el permiso NO abre el tablero |
| 8 | `TICKETS.ADMIN` (mayúsculas) | tablero | `true` (comparación case-insensitive, como hoy) |
| 9 | `null` / lista vacía | ambas | `false` (sin NRE) |

### Smoke manual (doble, según CLAUDE.md)

- **Rol sin el permiso (regresión):** admin actual → panel idéntico, mismos totales que antes.
  Resolutor con `tickets.gestionar` → sigue viendo solo lo suyo.
- **Rol con el permiso:** menú **Gerencia › Panel de control** visible y **nada más**; los totales
  coinciden con los que ve el admin; `/italjira/tablero` y `/italjira/backlog` por URL directa →
  redirigen a `/home`; `GET /api/tickets/tablero` → responde con alcance recortado (no global).

---

## 7. Validación

```bash
cd backend && dotnet build && dotnet test
cd frontend && yarn build
```

Backend local **apagado antes de empezar y apagado al terminar** (regla dura del ciclo de vida).
Migración probada con `dotnet ef database update` en la BD local antes de mergear.

---

## 8. Post-deploy (manual, no va en la migración)

1. Roles y Permisos → crear/elegir el rol de gerencia y asignarle **solo** `tickets.indicadores`.
2. Asignarle el menú **Gerencia › Panel de control** (`role_menus`).
3. Verificar en `company_permissions` que la empresa del rol tenga `tickets.indicadores` habilitado.
4. Login con un usuario de ese rol y confirmar el sidebar.
