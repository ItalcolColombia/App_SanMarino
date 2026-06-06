# 16 — Tickets: Perfiles de atención, asignación por país/tipo y niveles de solicitante

**Fecha:** 2026-06-04
**Estado:** 🟡 PLAN (sin código aún — pendiente de aprobación)
**Base:** continúa el módulo de tickets ([14](14_modulo_tickets_soporte_plan.md) + [15](15_tickets_ux_redesign_y_features.md)).
**Arquitectura:** Hexagonal / Ports & Adapters (misma del proyecto).

---

## 1. Objetivo

Enrutar y asignar tickets según **quién atiende qué tipo y en qué país**, con:
- **Perfil de atención (resolutor)** por usuario: qué tipo(s) atiende (`SOPORTE` / `DESARROLLO` / `REQUERIMIENTO` / `DUDAS`) y en qué país (o **todos los países / global**).
- **Filtrado al crear**: el solicitante solo ve los tipos que **tienen resolutor en su país** (o global), y un **select de asignado** con los usuarios resolutores de ese tipo+país.
- **Nivel de solicitante**: qué tipos puede **crear** cada usuario.
- **Bandeja "Asignados a mí"** por usuario + gestión.
- **Transferencia** (Requerimiento → Desarrollo) reasignando a otro resolutor.
- **Super Admin**: ve todo (ya existe).

---

## 2. Conceptos clave

| Concepto | Definición |
|---|---|
| **Resolutor** | Usuario que atiende un `tipo` en un `país` (o global). Una persona puede tener varios: ej. `(SOPORTE, Ecuador)`, `(DESARROLLO, global)`. |
| **Global** | `pais_id = NULL` → el resolutor aparece para **todos** los países. |
| **Nivel de solicitante** | Qué tipos puede **crear** el usuario. `NORMAL` → SOPORTE, DUDAS. `IMPLEMENTADOR` → + DESARROLLO, REQUERIMIENTO. |
| **Asignación** | Al crear (o luego), se elige un resolutor del select filtrado por `(tipo, país)`. Reusa `tickets.assigned_to_*`. |
| **Transferencia** | Un resolutor de `REQUERIMIENTO` puede transferir a `DESARROLLO` y asignar a un resolutor de desarrollo. |

Composición con los permisos existentes (no se reemplazan):
- `tickets.crear` + **nivel** → tipos creables.
- `tickets.gestionar` + **perfil de resolutor (tipo, país)** → qué cae en su bandeja.
- `tickets.admin` → ve todo.

---

## 3. ⚠️ Nota técnica crítica — identidad de usuario (Guid vs int)

**Problema detectado:** el módulo de tickets hoy guarda `created_by_user_id` / `assigned_to_user_id` como **int**, que en este entorno es un **hash del Guid** (cuando el JWT no trae `user_id` numérico). Por eso el detalle muestra "Usuario #1068987241" — un número **no joinable** con la tabla `users` (cuya PK es `Guid`).

Para esta feature **necesitamos referencias reales a usuarios** (mostrar nombres, filtrar por país del usuario, poblar selects). Por lo tanto:

- Las tablas nuevas (`ticket_resolutor`, `ticket_perfil_usuario`) referencian al usuario por **`Guid`** (`users.id`), usando `ICurrentUser.UserGuid` (ya disponible).
- Para la **asignación** del ticket se agrega `assigned_to_user_guid (uuid NULL)` y `created_by_user_guid (uuid NULL)` a `tickets`, en paralelo a los int actuales (no se rompen). Los nombres se resuelven con `JOIN users`.
- **Decisión D1 (abajo):** migrar a Guid como identidad canónica del módulo, o mantener ambos.

> Sin resolver esto, el "select de usuarios" no puede mostrar personas reales. Es el primer paso del plan.

---

## 4. Modelo de datos (PostgreSQL, snake_case, idempotente)

### 4.1 `ticket_resolutor` — quién atiende qué y dónde
| Columna | Tipo | Notas |
|---|---|---|
| `id` | bigint identity PK | |
| `user_id` | uuid NOT NULL | FK lógica → `users.id` |
| `tipo` | varchar(20) NOT NULL | SOPORTE / DESARROLLO / REQUERIMIENTO / DUDAS |
| `pais_id` | int NULL | **NULL = global (todos los países)** |
| `company_id` | int NOT NULL | multi-tenant |
| `activo` | boolean NOT NULL default true | |
| auditoría | | `created_*`, `updated_*` |

Único: `(user_id, tipo, pais_id, company_id)`. Índices: `tipo`, `pais_id`, `company_id`, `user_id`.

### 4.2 `ticket_perfil_usuario` — nivel de solicitante (1 por usuario/empresa)
| Columna | Tipo | Notas |
|---|---|---|
| `id` | bigint identity PK | |
| `user_id` | uuid NOT NULL | |
| `company_id` | int NOT NULL | |
| `nivel` | varchar(20) NOT NULL | `NORMAL` \| `IMPLEMENTADOR` |
| `activo` | boolean default true | |
| auditoría | | |

Único: `(user_id, company_id)`.

### 4.3 `tickets` (alter, idempotente)
- `+ assigned_to_user_guid uuid NULL`
- `+ created_by_user_guid uuid NULL`
- (mantener los int existentes por compatibilidad)

### 4.4 (Opcional, Fase 2) `ticket_resolutor_rol(role_id, tipo, pais_id, company_id)`
Defaults por rol que siembran el perfil de un usuario al asignarle el rol. **Decisión D2.**

---

## 5. Backend (Clean/Hexagonal — calcar patrón `TicketService`)

### Entidades / configs / migración
- `TicketResolutor`, `TicketPerfilUsuario` (Domain) + configs EF + DbSets + migración **idempotente** (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`).

### Endpoints (en `TicketsController` o nuevo `TicketResolutoresController`)
| Verbo | Ruta | Para |
|---|---|---|
| `GET` | `/api/tickets/resolutores?userId=` | Listar perfiles de resolutor de un usuario |
| `PUT` | `/api/tickets/resolutores/{userGuid}` | Setear perfiles (lista de `{tipo, paisId?}`) + nivel de un usuario |
| `GET` | `/api/tickets/tipos-permitidos` | Tipos que el **usuario actual** puede crear (según su nivel) |
| `GET` | `/api/tickets/asignables?tipo=&paisId=` | Usuarios resolutores para `(tipo, país\|global)` → `[{userGuid, nombre}]` para el select |
| `POST` | `/api/tickets` (modificar) | aceptar `assignedToUserGuid?`; validar tipo∈nivel y resolutor∈(tipo,país) |
| `POST` | `/api/tickets/{id}/transferir` | `{ tipoDestino, assignedToUserGuid }` → reasigna + estado TRANSFERIDO + nota |
| `GET` | `/api/tickets/asignados` | Bandeja "asignados a mí" (`assigned_to_user_guid == currentUser.UserGuid`) |

### Reglas de servicio
- **Tipos creables** = `NivelTipos[nivel]` ∩ (tipos con al menos un resolutor en el país del usuario o global). Si un tipo no tiene resolutor en ese país → no se ofrece.
- **Asignables** = `ticket_resolutor` where `tipo=X and (pais_id = P or pais_id is null) and activo`.
- **Crear**: si `assignedToUserGuid` viene, validar que sea resolutor válido de `(tipo, país)`. Estado inicial `ABIERTO` (o `EN_ANALISIS` si se asigna directo — **D3**).
- **Transferir**: solo resolutor de `REQUERIMIENTO`; destino `DESARROLLO`; nuevo asignado debe ser resolutor de desarrollo; estado `TRANSFERIDO`; registra nota; **¿cambia `tipo` a DESARROLLO?** — **D4**.

### Mapa de niveles (configurable)
```
NORMAL        → [SOPORTE, DUDAS]
IMPLEMENTADOR → [SOPORTE, DUDAS, DESARROLLO, REQUERIMIENTO]
```

---

## 6. Frontend (Angular standalone)

### 6.1 Módulo Usuarios (crear/editar)
- Sección **"Perfiles de atención de tickets"**:
  - **Nivel** (radio): Normal / Implementador.
  - **Resolutor**: por cada tipo (Soporte/Desarrollo/Requerimiento/Dudas) un toggle + selector de **país** (multi: específicos o "Todos los países / Global").
  - Guarda contra `PUT /resolutores/{userGuid}`.

### 6.2 Módulo Roles (opcional, Fase 2 — **D2**)
- Mismos perfiles como **default** del rol (siembran el perfil del usuario al asignarlo).

### 6.3 Crear ticket
- **Tipo**: solo los de `GET /tipos-permitidos` (según nivel + disponibilidad en el país).
- **Asignar a** (select): tras elegir tipo, llama `GET /asignables?tipo=&paisId=auto` y muestra usuarios resolutores (nombre). Opcional u obligatorio — **D3**.

### 6.4 Bandejas
- Nueva **"Asignados a mí"** (`/tickets/asignados`) — reusa `ticket-list`.
- Gestión/Admin existentes se mantienen; "Asignados a mí" es la bandeja personal del resolutor.

### 6.5 Detalle
- Acción **Transferir** (visible para resolutor de Requerimiento): elegir resolutor de Desarrollo → `POST /transferir`.
- Mostrar **asignado** (nombre real, ya con Guid+join).

---

## 7. Flujos con ejemplos

**Ej. 1 — Ecuador (país 1) solo tiene resolutores de Soporte y Requerimiento:**
- Un solicitante de Ecuador al crear ve **solo** "Soporte" y "Requerimiento" (los demás no tienen resolutor en Ecuador). El select de asignado muestra los usuarios resolutores de Ecuador **+ los globales** de ese tipo.

**Ej. 2 — Resolutor global de Desarrollo:**
- `(DESARROLLO, pais_id=NULL)` → aparece como asignable en **todos** los países cuando el tipo es Desarrollo.

**Ej. 3 — Transferencia:**
- Resolutor de Requerimiento abre el ticket, decide que es desarrollo → "Transferir a Desarrollo" → elige un resolutor de Desarrollo (de su país o global) → el ticket pasa a TRANSFERIDO, queda asignado al de desarrollo, y aparece en la bandeja de éste.

**Ej. 4 — Niveles:**
- Usuario Normal: al crear solo puede elegir Soporte/Dudas. Implementador: además Desarrollo/Requerimiento.

---

## 8. Criterios de aceptación
1. Crear muestra solo tipos del nivel **y** con resolutor en el país (o global).
2. El select de asignado solo lista resolutores válidos de `(tipo, país|global)`.
3. Usuario global aparece en todos los países para su(s) tipo(s).
4. "Asignados a mí" muestra solo lo asignado al usuario actual; puede gestionarlo.
5. Transferencia reasigna correctamente y respeta perfiles.
6. Super admin ve todo (sin cambios).
7. Multi-tenant: todo scoping por `company_id`.

---

## 9. Decisiones CONFIRMADAS (2026-06-04)
- **D1 — Identidad:** ✅ usar **Guid** para referencias de usuario del módulo; agregar `assigned_to_user_guid`/`created_by_user_guid` en paralelo a los int (no se rompen) y usar Guid para la feature.
- **D2 — Perfiles en Rol:** ✅ **Usuario + Rol**. El rol lleva perfiles de atención como *default*; al asignar el rol a un usuario, siembran/complementan su perfil. Editable también a nivel usuario. → **`ticket_resolutor_rol` pasa a Fase 1.**
- **D3 — Asignación al crear:** ✅ **Obligatoria**. Hay que elegir un resolutor válido `(tipo, país|global)` al crear. Estado inicial `ABIERTO` (asignado); el resolutor lo abre → `EN_ANALISIS`.
- **D4 — Transferir cambia el `tipo`:** ✅ **Sí**: pasa a `tipo=DESARROLLO`, se reasigna al resolutor de desarrollo, estado `TRANSFERIDO`, registra nota.
- **D5 — DUDAS:** las crea `NORMAL`; las atiende quien tenga resolutor de `DUDAS` (default).

> Efecto de D2/D3 en el alcance: la tabla y UI **de rol** entran en Fase 1, y el select de asignado del **crear** es obligatorio (validado en backend).

---

## 10. Fases de implementación
1. **Identidad + datos:** alter `tickets` (+`assigned_to_user_guid`,`created_by_user_guid`), tablas `ticket_resolutor`, `ticket_perfil_usuario` y **`ticket_resolutor_rol`** (D2), migración idempotente.
2. **Backend perfiles:** entidades/servicios/endpoints de resolutores (usuario **y rol**), nivel, `asignables`, `tipos-permitidos`. Resolución de nombres con `JOIN users`.
3. **Backend tickets:** crear con **asignación obligatoria** (valida resolutor), `transferir` (cambia tipo a DESARROLLO), bandeja `asignados`.
4. **Frontend Usuarios + Roles:** sección "perfiles de atención" (nivel + resolutor por tipo/país) en ambos módulos.
5. **Frontend tickets:** crear (tipos filtrados + **select asignado obligatorio**), bandeja "Asignados a mí", transferir en detalle, mostrar nombre real del asignado.
6. **Validación e2e** (navegador con HMR) + `make down`.

> Pendiente previo (iteración 2, no bloquea): código `AAAA00000`, adjuntos PDF/Excel, links.
