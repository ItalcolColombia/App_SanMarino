# 🎫 MÓDULO DE TICKETS — Documentación Técnica

> **Última actualización:** 2026-06-22
> **Estado:** Producción · Backend (.NET 9, Clean Architecture) + Frontend (Angular 20 standalone)

## 📋 RESUMEN EJECUTIVO

Sistema centralizado de **tickets de soporte y requerimientos** multi-empresa / multi-país. Permite a cualquier usuario crear solicitudes (Soporte, Dudas, Desarrollo, Requerimiento), asignarlas a un **resolutor** del equipo central, dar seguimiento tipo chat, adjuntar evidencia (imágenes, Excel/PDF, links) y cerrar el caso con **doble confirmación** (resolutor soluciona + solicitante confirma).

Características clave:
- ✅ **Resolutores globales (cross-company):** el equipo de soporte/desarrollo vive en una empresa central y atiende tickets de todas las subsidiarias.
- ✅ **Niveles de solicitante** que limitan qué tipos puede crear cada usuario.
- ✅ **Máquina de estados** con cierre por doble confirmación (SOLUCIONADO → CERRADO).
- ✅ **Conversación tipo chat** (mis notas a la derecha, las ajenas a la izquierda) con nombre + rol + email.
- ✅ **Adjuntos**: imágenes, documentos Excel/PDF (Base64) y links externos.
- ✅ **Notificación automática por correo** al solicitante cuando se soluciona.
- ✅ **Segregación de funciones**: el solicitante no gestiona su propio ticket.

---

## 🏗️ ARQUITECTURA

### Backend (Clean Architecture — `/backend/src/`)

| Capa | Archivos del módulo |
|---|---|
| **Domain** | `Ticket.cs`, `TicketImagen.cs`, `TicketAdjunto.cs`, `TicketNota.cs`, `TicketResolutor.cs`, `TicketResolutorRol.cs`, `TicketPerfilUsuario.cs`, `TicketConstants.cs` (tipos + estados + máquina), `TicketNivelConstants.cs` |
| **Application** | `DTOs/Tickets/TicketDtos.cs`, `DTOs/Tickets/TicketPerfilDtos.cs`, `Interfaces/ITicketService.cs`, `Interfaces/ITicketPerfilService.cs` |
| **Infrastructure** | `Services/TicketService.cs`, `Services/TicketPerfilService.cs`, `Persistence/Configurations/Ticket*Configuration.cs` |
| **API** | `Controllers/TicketsController.cs`, `Controllers/TicketPerfilesController.cs` |

### Frontend (Angular standalone — `/frontend/src/app/features/tickets/`)

```
tickets/
├── models/ticket.models.ts          # Tipos + catálogos de estado/transiciones/permisos
├── services/
│   ├── ticket.service.ts            # HTTP a TicketsController
│   └── ticket-perfil.service.ts     # HTTP a TicketPerfilesController
├── pages/
│   ├── mis-tickets/                 # Bandeja del solicitante ("Mis solicitudes")
│   ├── ticket-create/               # Crear ticket
│   ├── ticket-detalle/              # Detalle: chat, solución, cierre, adjuntos, gestión
│   ├── mis-asignados/               # Bandeja "Asignados a mí"
│   ├── gestion-tickets/             # Bandeja de gestión (por perfil de resolutor)
│   └── admin-tickets/               # Bandeja global del super admin
├── components/
│   ├── ticket-list/                 # Lista presentacional (cards)
│   ├── ticket-estado-badge/         # Badge de estado
│   ├── ticket-stepper/              # Stepper de progreso
│   ├── image-dropzone/ image-lightbox/
│   └── ticket-perfil-editor/        # Editor de perfiles (en Usuarios/Roles)
└── tickets.routes.ts                # Rutas lazy con permissionGuard
```

---

## 🗄️ MODELO DE DATOS

### Tablas

| Tabla | Propósito |
|---|---|
| `tickets` | Ticket. Incluye empresa/país, tipo, estado, autor/asignado (int + Guid), solución y cierre. |
| `ticket_notas` | Bitácora / conversación (chat). |
| `ticket_imagenes` | Imágenes adjuntas (Base64). |
| `ticket_adjuntos` | Documentos (Excel/PDF en Base64) y links externos. |
| `ticket_resolutores` | Perfil de resolutor por **usuario**: (tipo, país). País NULL = global. |
| `ticket_resolutor_roles` | Defaults de resolutor por **rol**. |
| `ticket_perfiles_usuario` | Nivel del solicitante (NORMAL / IMPLEMENTADOR). |

### Campos relevantes de `tickets`

| Campo | Descripción |
|---|---|
| `tipo` | SOPORTE \| DESARROLLO \| REQUERIMIENTO \| DUDAS |
| `estado` | ABIERTO \| EN_ANALISIS \| EN_IMPLEMENTACION \| SOLUCIONADO \| CERRADO \| TRANSFERIDO \| SUSPENDIDO |
| `created_by_user_id` / `created_by_user_guid` | Autor (cédula int + Guid → `users.id`) |
| `assigned_to_user_id` / `assigned_to_user_guid` | Resolutor asignado |
| `fecha_solucion`, `solucion_descripcion` | Solución del resolutor |
| `fecha_cierre_solicitante`, `cerrado_por_user_id` | Cierre confirmado por el solicitante |
| `notificado_correo`, `fecha_notificacion_correo`, `correo_notificado_a` | Notificación por correo de la solución |

> **Migración:** `20260605182358_AddTicketCierreAdjuntos` (idempotente: `ADD COLUMN IF NOT EXISTS` / `CREATE TABLE IF NOT EXISTS`).

---

## 🧠 CONCEPTOS CLAVE

### 1. Resolutores globales (cross-company)
El equipo de soporte/desarrollo está en la empresa central (ej. Agroavícola Sanmarino, `company_id=1`) y atiende tickets creados por las subsidiarias (Ecuador `=3`, etc.). Por eso **las consultas de resolutor NO filtran por `company_id`**, solo por `tipo` y `pais_id`:

- Un perfil `(DESARROLLO, pais=NULL)` = **global** → atiende ese tipo en todos los países.
- Un perfil `(SOPORTE, pais=2)` → atiende Soporte solo de Ecuador.

### 2. Niveles del solicitante (qué tipos puede crear)
- **NORMAL** → SOPORTE, DUDAS.
- **IMPLEMENTADOR** → SOPORTE, DUDAS, DESARROLLO, REQUERIMIENTO.

> Si el usuario tiene permiso `tickets.gestionar` o `tickets.admin`, se le trata como **IMPLEMENTADOR** automáticamente. Si no, se lee `ticket_perfiles_usuario.nivel` (default NORMAL).
>
> **El nivel es exclusivamente por usuario** (eje "quién CREA"). No existe nivel por rol.

### 2.bis. Resolutor: dos niveles (eje "quién RECIBE")
El enrutamiento de tickets lee **solo** `ticket_resolutores` (perfil por usuario). El perfil por **rol** (`ticket_resolutor_roles`) es una **plantilla**:
- Al **asignar un rol** a un usuario (`UserService.CreateAsync`/`UpdateAsync`), se **siembra** automáticamente su perfil de resolutor desde la plantilla del rol (`SeedPerfilDesdeRolAsync`, idempotente y best-effort: si falla no rompe el alta).
- Editar la plantilla luego no afecta a los usuarios existentes hasta usar **`POST /rol/{roleId}/reaplicar`** (botón "Aplicar a los usuarios de este rol" en Editar Rol). El re-seed solo **agrega** lo faltante; no borra ajustes hechos por usuario.
- En la UI: **Editar Usuario → Perfil de Atención** muestra ① Creación (Nivel) y ② Atención (Resolutor) en tarjetas separadas; **Editar Rol** muestra solo la plantilla de Atención.

### 3. Bandejas
| Bandeja | Endpoint | Qué muestra |
|---|---|---|
| Mis solicitudes | `GET /mis-tickets` | Tickets que **yo creé** |
| Asignados a mí | `GET /asignados` | Tickets con mi `assigned_to_user_guid` (cross-company) |
| Gestión | `GET /gestion` | Tickets que matchean **mis perfiles de resolutor** (tipo+país), global. Si no tengo perfiles → fallback a empresa/país activos |
| Admin | `GET /admin` | Global dentro de la empresa (filtros por país/empresa) |

---

## 🔄 MÁQUINA DE ESTADOS

```
ABIERTO ──► EN_ANALISIS ──► EN_IMPLEMENTACION ──► SOLUCIONADO ──► CERRADO
   │            │ ▲                  │                  │  ▲          (terminal)
   │            │ └──────────────────┘                  │  │
   ├─► SUSPENDIDO ◄───────────────────────────────────┘  │ (reapertura)
   └─► TRANSFERIDO ──► EN_ANALISIS                         │
                       SOLUCIONADO ──► EN_ANALISIS ────────┘
```

Transiciones (fuente de verdad: `TicketEstados.Transiciones`):
- `ABIERTO` → EN_ANALISIS, SUSPENDIDO, TRANSFERIDO
- `EN_ANALISIS` → EN_IMPLEMENTACION, SOLUCIONADO, SUSPENDIDO, TRANSFERIDO
- `EN_IMPLEMENTACION` → SOLUCIONADO, EN_ANALISIS, SUSPENDIDO, TRANSFERIDO
- `SOLUCIONADO` → EN_ANALISIS (reapertura), CERRADO (confirmación del solicitante)
- `CERRADO` → (terminal)
- `TRANSFERIDO` → EN_ANALISIS, SUSPENDIDO
- `SUSPENDIDO` → EN_ANALISIS

### Cierre por doble confirmación (regla de negocio central)
1. El **resolutor** marca **SOLUCIONADO** → debe escribir la **descripción de la solución** (obligatoria). Se encola un **correo automático** al solicitante.
2. El **solicitante** revisa y:
   - **Confirma el cierre** → estado **CERRADO** (cierre por ambas partes), o
   - **Reabre** si no está conforme → vuelve a **EN_ANALISIS**.

---

## 🔐 SEGREGACIÓN DE FUNCIONES (reglas críticas)

| Regla | Dónde se aplica |
|---|---|
| El **solicitante NO gestiona** su propio ticket (no toma, no cambia estado). | Backend: `TomarAsync` y `CambiarEstadoAsync` rechazan si `EsCreador`. Front: panel de gestión oculto si `soyCreador`. |
| Única excepción del creador: **reabrir** (SOLUCIONADO → EN_ANALISIS). | `CambiarEstadoAsync` |
| El **cierre definitivo** (CERRADO) lo confirma **solo el solicitante**. | `ConfirmarCierreAsync` (verifica `EsCreador` y estado SOLUCIONADO). `CambiarEstadoAsync` bloquea CERRADO directo. |
| **Visibilidad del detalle**: lo ve el creador, el asignado, un resolutor con perfil que matchea (tipo,país), o quien tenga `tickets.admin`. | `PuedeVerTicketAsync` en `GetByIdAsync`. |

---

## 🌐 ENDPOINTS API (`/api/tickets`)

### Solicitante
| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/` | Crear ticket (asigna resolutor obligatorio) |
| `GET` | `/mis-tickets` | Bandeja "Mis solicitudes" |
| `GET` | `/{id}` | Detalle (notas, imágenes, adjuntos, solución, cierre) |
| `POST` | `/{id}/notas` | Agregar comentario a la conversación |
| `POST` | `/{id}/confirmar-cierre` | Confirmar cierre (SOLUCIONADO → CERRADO) |

### Adjuntos
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/{id}/imagenes` · `/{id}/imagenes/{imgId}` | Metadata / imagen on-demand (Base64) |
| `POST` | `/{id}/imagenes` | Agregar imágenes |
| `GET` | `/{id}/adjuntos` | Listar documentos y links |
| `POST` | `/{id}/documentos` | Subir Excel/PDF (Base64, máx 8 MB) |
| `POST` | `/{id}/links` | Agregar link (URL + título) |
| `GET` | `/{id}/adjuntos/{adjId}/descargar` | Descargar documento (Base64) |
| `DELETE` | `/{id}/adjuntos/{adjId}` | Eliminar adjunto |

### Resolutor / gestión
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/gestion` | Bandeja por perfil de resolutor |
| `GET` | `/asignados` | Tickets asignados a mí |
| `POST` | `/{id}/tomar` | Tomar el ticket (asigna y pasa a EN_ANALISIS) |
| `PATCH` | `/{id}/estado` | Cambiar estado (SOLUCIONADO exige `solucionDescripcion`) |
| `POST` | `/{id}/transferir` | Transferir REQUERIMIENTO → DESARROLLO |

### Admin / utilidades
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/admin` | Bandeja global (filtros país/empresa) |
| `GET` | `/catalogos` | Tipos y estados para selects |
| `DELETE` | `/{id}` | Eliminación lógica |

### Perfiles (`/api/ticket-perfiles`)
| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/tipos-permitidos` | Tipos que el usuario puede crear + asignables |
| `GET` | `/asignables?tipo=&paisId=` | Resolutores disponibles |
| `GET`/`PUT` | `/usuario/{userId}` | Perfil de atención de un usuario (nivel + resolutores) |
| `GET`/`PUT` | `/rol/{roleId}` | **Plantilla** de resolutor por rol |
| `POST` | `/usuario/{userId}/seed-desde-rol/{roleId}` | Sembrar perfiles del rol en el usuario |
| `POST` | `/rol/{roleId}/reaplicar` | Re-aplicar la plantilla del rol a **todos** sus usuarios (empresa activa) |

---

## 🔑 PERMISOS

| Permiso | Habilita |
|---|---|
| `tickets.crear` | Crear tickets y ver la bandeja propia |
| `tickets.gestionar` | Bandejas de gestión/asignados + gestionar (tomar, estado, transferir) |
| `tickets.admin` | Bandeja global + ver cualquier ticket |

> El front gatea las rutas con `permissionGuard` (`tickets.routes.ts`). El nivel IMPLEMENTADOR se infiere de `tickets.gestionar`/`tickets.admin`.

---

## 📧 NOTIFICACIÓN POR CORREO

Al marcar **SOLUCIONADO**, `TicketService` resuelve el email del solicitante (`users → UserLogins → Login.email`) y encola un correo con la solución vía `IEmailQueueService.EnqueueEmailAsync(...)` (tipo `ticket_solucionado`). Se registran `notificado_correo`, `fecha_notificacion_correo` y `correo_notificado_a`. Si el encolado falla, **no** bloquea el cambio de estado (try/catch).

El envío real lo procesa el `EmailQueueProcessorService` (background) con la configuración SMTP del proyecto.

---

## 🖥️ DETALLE DEL TICKET (UX)

- **Conversación tipo chat:** las notas propias se alinean a la derecha (verde), las ajenas a la izquierda (gris). Cada burbuja muestra nombre + rol + email + fecha. El backend marca `EsMio` por nota.
- **Solución:** sección destacada con la descripción + indicador "Notificado por correo".
- **Panel de gestión** (solo quien atiende): Tomar, cambiar estado, transferir.
- **Panel de cierre** (solo el solicitante, ticket SOLUCIONADO): Confirmar cierre / Reabrir.
- **Documentos y links:** subir Excel/PDF, pegar links, descargar/abrir, eliminar.
- **Detalles:** país por nombre, creador y asignado con nombre + rol + email.

---

## 📝 NOTAS DE IMPLEMENTACIÓN

- Los listados **nunca** materializan `imagen_base64` / `contenido_base64` (carga pesada on-demand).
- Identidad legible: creador/asignado por **Guid** (fallback a **cédula** para tickets antiguos sin guid); autores de notas/adjuntos por **cédula** (`users.cedula`). El **rol** mostrado es el del usuario en la **empresa del ticket**.
- El país/empresa/autor se infieren del contexto (`ICurrentUser`), **nunca** del body.
- Almacenamiento de archivos: Base64 en BD (mismo patrón que imágenes). La tabla queda preparada para migrar a S3 agregando una columna de key/url.

---

## 📂 REFERENCIAS

- Planes de desarrollo: `fase_de_desarrollo/16_tickets_asignacion_perfiles_niveles_plan.md`, `fase_de_desarrollo/tickets_fix_cross_company_resolutores.md`, `fase_de_desarrollo/tickets_cierre_doble_adjuntos_correo.md`.
- Script de diagnóstico de visibilidad: `backend/sql/diagnostico_tickets_visibilidad.sql`.
