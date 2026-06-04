# 14 — Módulo: Sistema Centralizado de Tickets de Soporte y Requerimientos

**Fecha:** 2026-06-04
**Estado:** 🟡 DISEÑO (sin código aún — pendiente aprobación)
**Arquitectura:** Hexagonal / Ports & Adapters (la **misma** del proyecto). Vertical slice tomado como plantilla: `Lesion` (`LesionesController` → `ILesionService` → `LesionService` + `LesionConfiguration`).

---

## 1. Objetivo y alcance

Centralizar errores, dudas, soportes y requerimientos en un módulo propio, con:
- Trazabilidad por **país** y **empresa** (multi-tenant, ya soportado por `ICurrentUser`).
- 3 perfiles: **Solicitante**, **Resolutor** y **Super Admin**.
- Adjuntar **múltiples imágenes en Base64** sin degradar el rendimiento de listados ni del navegador.
- Línea de tiempo visual (stepper) + bitácora de novedades.

**Fuera de alcance (v1):** notificaciones por correo (se puede enganchar luego al `IEmailQueueService` existente), SLA/tiempos de respuesta, métricas/dashboard de tickets.

---

## 2. Arquitectura — mapeo por capa (NO se introduce nada nuevo)

| Capa | Patrón existente (plantilla `Lesion`) | A crear para Tickets |
|---|---|---|
| **Domain** (`ZooSanMarino.Domain/Entities`) | `Lesion : AuditableEntity` | `Ticket`, `TicketImagen`, `TicketNota` (`: AuditableEntity` salvo nota/imagen, ver §3) + `TicketTipos`, `TicketEstados` (constantes) |
| **Application — puerto** (`Application/Interfaces`) | `ILesionService` | `ITicketService` |
| **Application — DTOs** (`Application/DTOs/Tickets`) | `LesionDto`, `CreateLesionRequest`… (records) | `TicketListItemDto`, `TicketDetailDto`, `CreateTicketRequest`, … (§7) |
| **Infrastructure — adaptador** (`Infrastructure/Services`) | `LesionService` (usa `ZooSanMarinoContext`, `ICurrentUser`, `ICompanyResolver`) | `TicketService` |
| **Infrastructure — EF config** (`Persistence/Configurations`) | `LesionConfiguration` | `TicketConfiguration`, `TicketImagenConfiguration`, `TicketNotaConfiguration` |
| **Infrastructure — DbSets** | `ZooSanMarinoContext` | `DbSet<Ticket>`, `DbSet<TicketImagen>`, `DbSet<TicketNota>` |
| **API — adaptador entrada** (`API/Controllers`) | `LesionesController` | `TicketsController` |
| **API — DI** (`Program.cs`) | `AddScoped<ILesionService, LesionService>()` | `AddScoped<ITicketService, TicketService>()` |
| **Puerto de contexto país/usuario** | `ICurrentUser` → `HttpCurrentUser` | **Reutilizado tal cual** (no se toca) |

### El requisito de "inyección de contexto" del PRD ya está resuelto
`ICurrentUser` (implementado por `HttpCurrentUser`) ya provee:
- `PaisId` → header `X-Active-Pais` o claim `pais_id` (país de origen del ticket).
- `CompanyId` efectivo → header `X-Active-Company` / claim `company_id` (multi-tenant).
- `UserId` / `UserGuid` → del JWT (autor del ticket).

`TicketService` hará lo mismo que `LesionService`: resolver `companyId` con `ICompanyResolver` y tomar `PaisId`/`UserId` de `ICurrentUser`. **El front no envía nunca el país ni el userId en el body** — se infieren del request de forma segura.

---

## 3. Modelo de datos (PostgreSQL, snake_case, schema `public`)

> Convenciones calcadas de `lesiones`: `bigint` identity-always, columnas de auditoría de `AuditableEntity`, `status` char(1), índices `ix_<tabla>_<col>`. Migración EF **idempotente** (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`) según `CLAUDE.md`.

### 3.1 `tickets`  (entidad `Ticket : AuditableEntity`)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | bigint identity PK | |
| `codigo` | varchar(20) | legible: `TK-2026-000123` (generado en backend) — opcional, ver §12 |
| `company_id` | int NOT NULL | de `AuditableEntity` (multi-tenant) |
| `pais_id` | int NOT NULL | de `ICurrentUser.PaisId` |
| `tipo` | varchar(20) NOT NULL | `SOPORTE` \| `DESARROLLO` \| `REQUERIMIENTO` \| `DUDAS` |
| `estado` | varchar(20) NOT NULL | `ABIERTO` \| `EN_ANALISIS` \| `EN_IMPLEMENTACION` \| `SOLUCIONADO` \| `TRANSFERIDO` \| `SUSPENDIDO` |
| `titulo` | varchar(160) NOT NULL | |
| `descripcion` | text NOT NULL | |
| `assigned_to_user_id` | int NULL | resolutor que tomó el ticket |
| `fecha_primera_apertura` | timestamptz NULL | cuándo un resolutor lo abrió por 1ª vez |
| `fecha_solucion` | timestamptz NULL | cuándo pasó a `SOLUCIONADO` |
| `status` | char(1) NOT NULL `A` | activo/inactivo (patrón del proyecto) |
| `created_by_user_id` | int NOT NULL | autor (de `AuditableEntity`) |
| `created_at` | timestamptz NOT NULL `now()` | |
| `updated_by_user_id` | int NULL | |
| `updated_at` | timestamptz NULL | |
| `deleted_at` | timestamptz NULL | soft-delete |

Índices: `ix_tickets_company_id`, `ix_tickets_pais_id`, `ix_tickets_estado`, `ix_tickets_tipo`, `ix_tickets_created_by_user_id`, `ix_tickets_assigned_to_user_id`, `ix_tickets_created_at`.

### 3.2 `ticket_imagenes`  (entidad `TicketImagen`)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | bigint identity PK | |
| `ticket_id` | bigint NOT NULL FK→`tickets.id` ON DELETE CASCADE | |
| `imagen_base64` | text NOT NULL | **carga pesada** — nunca en listados |
| `file_name` | varchar(200) NULL | |
| `content_type` | varchar(60) NULL | `image/webp`, `image/jpeg`… |
| `size_bytes` | int NULL | tamaño tras compresión (metadato ligero) |
| `created_at` | timestamptz NOT NULL `now()` | |

Índice: `ix_ticket_imagenes_ticket_id`.
> **Decisión de almacenamiento:** v1 guarda Base64 en BD por el PRD. La tabla queda lista para migrar a S3 (ya hay S3 en infra) agregando `s3_key`/`url` y vaciando `imagen_base64` — no rompe el contrato.

### 3.3 `ticket_notas`  (entidad `TicketNota`)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | bigint identity PK | |
| `ticket_id` | bigint NOT NULL FK→`tickets.id` ON DELETE CASCADE | |
| `user_id` | int NOT NULL | quién dejó la nota (creador o resolutor) |
| `nota` | text NOT NULL | |
| `estado_resultante` | varchar(20) NULL | si la nota acompañó un cambio de estado → alimenta la línea de tiempo |
| `es_interna` | boolean NOT NULL default false | nota solo visible para resolutores/admin |
| `created_at` | timestamptz NOT NULL `now()` | |

Índices: `ix_ticket_notas_ticket_id`, `ix_ticket_notas_created_at`.

> `TicketImagen` y `TicketNota` **no** heredan `AuditableEntity` (no necesitan `company_id`/soft-delete propios; cuelgan del ticket y se filtran por él). Si se prefiere uniformidad, pueden heredarlo — decisión menor.

---

## 4. Máquina de estados

```
            ┌─────────────► TRANSFERIDO ──┐
            │                              ▼
ABIERTO ─► EN_ANALISIS ─► EN_IMPLEMENTACION ─► SOLUCIONADO
   │            │                  │              ▲
   │            └──────────────────┴──────────────┘ (reapertura opcional)
   └─────────────► SUSPENDIDO ◄────────────────────┘
```

- **ABIERTO**: estado inicial al crear (Solicitante).
- **Primera apertura por Resolutor** → `tomar`: `ABIERTO → EN_ANALISIS`, set `assigned_to_user_id` + `fecha_primera_apertura`. (Reemplaza el "cambio automático al abrir" del PRD, pero **explícito** vía endpoint, no como efecto colateral de un GET — un GET debe ser seguro/idempotente.)
- Resolutor cambia manualmente entre `EN_ANALISIS`, `EN_IMPLEMENTACION`, `SOLUCIONADO`, `TRANSFERIDO`, `SUSPENDIDO`.
- Transiciones validadas en `TicketService` (un diccionario `estado → estados permitidos`); transición inválida ⇒ `400`.
- `SOLUCIONADO`/`TRANSFERIDO`/`SUSPENDIDO` permiten reapertura a `EN_ANALISIS` (configurable).

Constantes en Domain (patrón `Lesion.ModuloOrigen` con `HashSet` validador), no enums mapeados a BD:
```csharp
public static class TicketEstados {
    public const string Abierto="ABIERTO", EnAnalisis="EN_ANALISIS",
        EnImplementacion="EN_IMPLEMENTACION", Solucionado="SOLUCIONADO",
        Transferido="TRANSFERIDO", Suspendido="SUSPENDIDO";
    public static readonly IReadOnlyDictionary<string,string[]> Transiciones = ...;
}
public static class TicketTipos { public const string Soporte="SOPORTE", Desarrollo="DESARROLLO", Requerimiento="REQUERIMIENTO", Dudas="DUDAS"; }
```

---

## 5. Roles y permisos (gating ya existente en el front)

El front ya usa `permisos: string[]`, la directiva `*appHasPermission` y `permissionGuard` (`data.permissions`). Definimos estas **claves de permiso**:

| Clave | Perfil | Capacidad |
|---|---|---|
| `tickets.crear` | Solicitante | Crear tickets, ver/comentar los propios |
| `tickets.gestionar` | Resolutor | Bandeja de gestión por país, tomar, cambiar estado, notas técnicas |
| `tickets.admin` | Super Admin | Acceso global multi-país, todos los filtros |

**Scoping en backend** (defensa real, no solo UI):
- Solicitante (`tickets.crear` sin `gestionar`/`admin`): sus queries fuerzan `created_by_user_id == UserId`.
- Resolutor (`tickets.gestionar`): scope por `company_id` + `pais_id` del request.
- Super Admin (`tickets.admin`): sin filtro de país; puede pasar `paisId`/`companyId` opcionales.

**Especialidad del resolutor (priorización por tipo):** v1 = filtro/orden por `tipo` en el front (default según el rol). Si más adelante se requiere asignación estricta resolutor↔tipo, se agrega tabla `ticket_resolutor_especialidad(user_id, tipo)` sin romper nada. (Decisión abierta §12.)

---

## 6. API REST — diseño de endpoints

Ruta base `api/tickets`. Estilo calcado de `LesionesController` (`[ApiController]`, `PagedResult<T>`, `[FromQuery]`, `CancellationToken`, validación → `BadRequest`). **Regla de oro de performance:** ningún endpoint de listado devuelve `imagen_base64`.

### Solicitante
| Verbo | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/tickets` | Crea ticket (`titulo`, `tipo`, `descripcion`, opcional `imagenesBase64[]`). `pais_id`/`company_id`/`created_by` del contexto. Estado inicial `ABIERTO`. → `201` |
| `GET` | `/api/tickets/mis-tickets?anio=&estado=&tipo=&page=&pageSize=` | Bandeja propia (filtro **obligatorio** por año + estado). Devuelve `PagedResult<TicketListItemDto>` **sin** base64. |
| `GET` | `/api/tickets/{id}` | Detalle: ticket + `notas[]` + **metadata** de imágenes (id, fileName, contentType, sizeBytes) — **sin** base64 inline. |
| `GET` | `/api/tickets/{id}/imagenes` | Lista metadata de imágenes del ticket (ligero). |
| `GET` | `/api/tickets/{id}/imagenes/{imgId}` | Devuelve **una** imagen bajo demanda (Base64 en JSON, ver §9.7). Carga perezosa. |
| `POST` | `/api/tickets/{id}/imagenes` | Sube imágenes adicionales (`imagenesBase64[]`), incremental. |
| `POST` | `/api/tickets/{id}/notas` | Agrega comentario/respuesta del solicitante. |

### Resolutor
| Verbo | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/tickets/gestion?tipo=&estado=&anio=&page=&pageSize=` | Bandeja de gestión. País inyectado del request (no del query). Orden por especialidad/tipo. Sin base64. |
| `POST` | `/api/tickets/{id}/tomar` | Toma el ticket: `assigned_to_user_id=UserId`; si `ABIERTO` → `EN_ANALISIS` + `fecha_primera_apertura`. Idempotente. |
| `PATCH` | `/api/tickets/{id}/estado` | Body `{ estado, nota? }`. Cambia estado (valida transición). Registra `TicketNota` con `estado_resultante` para la línea de tiempo. |
| `POST` | `/api/tickets/{id}/notas` | Nota técnica (`es_interna` opcional). |

### Super Admin
| Verbo | Ruta | Descripción |
|---|---|---|
| `GET` | `/api/tickets/admin?paisId=&companyId=&estado=&tipo=&anio=&page=&pageSize=` | Acceso global multi-país/multi-estado/tipo para auditoría. Sin base64. |

### Catálogos (opcional)
| `GET` | `/api/tickets/catalogos` | `{ tipos[], estados[] }` para poblar selects. (O constantes en el front.) |

### Códigos de estado
`201` crear · `200` lecturas/updates · `204` delete lógico · `400` validación/transición inválida · `403` fuera de scope (país/propiedad) · `404` no existe.

---

## 7. DTOs (records, en `Application/DTOs/Tickets`)

```csharp
// Entrada
public record CreateTicketRequest(string Titulo, string Tipo, string Descripcion, List<string>? ImagenesBase64);
public record AddTicketImagenesRequest(List<TicketImagenInput> Imagenes);
public record TicketImagenInput(string Base64, string? FileName, string? ContentType, int? SizeBytes);
public record CambiarEstadoTicketRequest(string Estado, string? Nota);
public record CreateTicketNotaRequest(string Nota, bool EsInterna = false);
public record TicketSearchRequest(int? Anio, string? Estado, string? Tipo, int? PaisId,
    int? CompanyId, bool SoloPropios, int Page = 1, int PageSize = 20);

// Salida — LISTADO (ligero, sin base64)
public record TicketListItemDto(long Id, string? Codigo, string Titulo, string Tipo, string Estado,
    int PaisId, string? PaisNombre, int CreatedByUserId, string? CreatedByNombre,
    int? AssignedToUserId, DateTime CreatedAt, int CantidadImagenes, int CantidadNotas);

// Salida — DETALLE (sin base64 inline; solo metadata de imágenes)
public record TicketDetailDto(long Id, string? Codigo, string Titulo, string Tipo, string Estado,
    string Descripcion, int PaisId, string? PaisNombre, int CreatedByUserId, string? CreatedByNombre,
    int? AssignedToUserId, DateTime CreatedAt, DateTime? FechaPrimeraApertura, DateTime? FechaSolucion,
    IReadOnlyList<TicketNotaDto> Notas, IReadOnlyList<TicketImagenMetaDto> Imagenes);

public record TicketNotaDto(long Id, int UserId, string? UserNombre, string Nota,
    string? EstadoResultante, bool EsInterna, DateTime CreatedAt);
public record TicketImagenMetaDto(long Id, string? FileName, string? ContentType, int? SizeBytes, DateTime CreatedAt);

// Salida — UNA imagen (on-demand)
public record TicketImagenDto(long Id, string ImagenBase64, string? ContentType, string? FileName);
```

---

## 8. Diseño UI (Angular standalone + Tailwind, paleta Italfoods)

Paleta: `ital-orange #e85c25` (acento/estado actual), `ital-green #2d7a3e` (completado/acciones primarias), `ital-cream #faf8f5` (fondo). Mobile-first (los reportes de error ocurren "en caliente" desde cualquier dispositivo).

### 8.1 Estructura de carpetas (calca `features/lesiones`)
```
frontend/src/app/features/tickets/
├── tickets.routes.ts                 # rutas + permissionGuard (data.permissions)
├── models/ticket.models.ts           # interfaces + enums TipoTicket/EstadoTicket + PagedResult
├── services/
│   ├── ticket.service.ts             # HttpClient (calca lesion.service.ts)
│   └── image-compression.util.ts     # optimización Base64 (§9)
├── pages/
│   ├── ticket-create/                # formulario + dropzone + preview
│   ├── mis-tickets/                  # bandeja solicitante (filtro año/estado, badges)
│   ├── ticket-detalle/               # stepper + timeline + notas + visor lazy
│   ├── gestion-tickets/              # bandeja resolutor
│   └── admin-tickets/                # bandeja super admin (reusa tabla con filtros extra)
└── components/
    ├── ticket-stepper/               # barra de progreso de estado
    ├── ticket-estado-badge/          # badge de color por estado
    ├── image-dropzone/               # drag&drop multi-imagen + miniaturas
    └── image-lightbox/               # visor a pantalla completa (carga on-demand)
```

### 8.2 Stepper (barra de progreso del estado) — pieza clave

Pasos lineales: **Abierto → En Análisis → En Implementación → Solucionado**.
Estados laterales (`Transferido`, `Suspendido`) **no** van en la barra: se muestran como badge prominente que reemplaza/acompaña al stepper (evita romper la metáfora lineal).

Comportamiento visual:
- Paso **completado**: círculo relleno `ital-green` + check.
- Paso **actual**: círculo `ital-orange`, anillo/pulso, label en negrita.
- Paso **pendiente**: círculo gris `slate-300`, label `slate-400`.
- Conector: barra `ital-green` (tramo completado) / `slate-200` (pendiente).
- **Responsive:** horizontal en `md+`, **vertical** en móvil (la línea pasa a columna).

Boceto (Tailwind):
```html
<!-- ticket-stepper: horizontal desktop / vertical mobile -->
<ol class="flex flex-col md:flex-row md:items-center gap-4 md:gap-0">
  @for (step of steps; track step.key; let i = $index) {
    <li class="flex md:flex-1 items-center gap-3">
      <!-- nodo -->
      <span class="grid place-items-center w-9 h-9 rounded-full shrink-0 ring-4 transition"
            [class.bg-ital-green]="step.done"
            [class.text-white]="step.done || step.current"
            [class.bg-ital-orange]="step.current"
            [class.ring-ital-orange/20]="step.current"
            [class.bg-slate-200]="!step.done && !step.current"
            [class.text-slate-400]="!step.done && !step.current"
            [class.ring-transparent]="!step.current">
        @if (step.done) { <svg><!-- check --></svg> } @else { {{ i + 1 }} }
      </span>
      <span class="text-sm font-medium"
            [class.text-slate-800]="step.done || step.current"
            [class.font-semibold]="step.current"
            [class.text-slate-400]="!step.done && !step.current">
        {{ step.label }}
      </span>
      <!-- conector (oculto en el último; horizontal solo en desktop) -->
      @if (!$last) {
        <span class="hidden md:block flex-1 h-0.5 mx-2"
              [class.bg-ital-green]="step.done" [class.bg-slate-200]="!step.done"></span>
      }
    </li>
  }
</ol>

<!-- estado especial -->
@if (esEstadoEspecial) {
  <div class="mt-3 inline-flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium"
       [ngClass]="badgeClass">
    <span class="w-2 h-2 rounded-full bg-current"></span> {{ estadoLabel }}
  </div>
}
```

### 8.3 Badges de estado (color por estado)
| Estado | Clases Tailwind |
|---|---|
| ABIERTO | `bg-sky-50 text-sky-700 ring-sky-200` |
| EN_ANALISIS | `bg-amber-50 text-amber-700 ring-amber-200` |
| EN_IMPLEMENTACION | `bg-indigo-50 text-indigo-700 ring-indigo-200` |
| SOLUCIONADO | `bg-emerald-50 text-emerald-700 ring-emerald-200` (≈ `ital-green`) |
| TRANSFERIDO | `bg-slate-100 text-slate-600 ring-slate-200` |
| SUSPENDIDO | `bg-rose-50 text-rose-700 ring-rose-200` |

### 8.4 Dropzone multi-imagen + preview
- Zona `border-2 border-dashed` (`ital-orange` al hacer dragover), soporta **drag & drop** y click (`<input type=file multiple accept="image/*">`).
- Grid de miniaturas (`grid-cols-3 md:grid-cols-4`) con: imagen (object URL, **no** data URL), botón ✕ para quitar, barra de progreso de compresión, badge de tamaño final.
- Validación previa: tipo `image/*`, tamaño máx original (p.ej. 8 MB), tope de cantidad (p.ej. 10) → contador `3/10`.
- Estados: "comprimiendo…", "lista", "error (formato/peso)".

### 8.5 Línea de tiempo (detalle)
- Arriba: stepper (§8.2) + badge de estado + metadatos (código, tipo, país, autor, fechas).
- Debajo: feed cronológico de `notas` (avatar/inicial del autor, texto, fecha, chip de `estado_resultante` si la nota acompañó un cambio). Notas `es_interna` solo para resolutor/admin.
- Input para responder/agregar comentario (solicitante y resolutor).
- Galería de imágenes: miniaturas que abren `image-lightbox` cargando el Base64 **on-demand** (`GET /imagenes/{imgId}`).

---

## 9. Optimización de Base64 en el frontend (requisito técnico clave)

**Problema:** Base64 infla el payload ~33 % y, con varias fotos de móvil (3–8 MB c/u), un solo request monolítico congela la UI y puede tumbar memoria/red.

### Estrategia (en `image-compression.util.ts` + componente dropzone)

1. **Comprimir/redimensionar ANTES de codificar.** Cada imagen se reescala con `<canvas>`/`OffscreenCanvas` (lado largo máx ~1600 px) y se reexporta con `canvas.toBlob(blob, 'image/webp', 0.7)` (fallback `image/jpeg`). Reduce el peso **80–95 %**. Recién al final se convierte el **Blob ya liviano** a Base64.
2. **Fuera del hilo principal.** `createImageBitmap(file)` + `OffscreenCanvas` dentro de un **Web Worker** → procesar varias imágenes sin bloquear el render. (Si no hay worker, degradar a `requestIdleCallback`/procesado secuencial.)
3. **Previsualizar con `URL.createObjectURL(blob)`, nunca con data URL.** Las miniaturas usan object URLs (baratos); se hace `URL.revokeObjectURL()` al quitar la imagen o destruir el componente. Evita inflar el DOM con data URLs gigantes.
4. **Codificar a Base64 solo al enviar**, y **subir de forma incremental** (una imagen por request a `POST /tickets/{id}/imagenes`) con barra de progreso, en vez de un payload de varios MB. Primero se crea el ticket (liviano), luego se adjuntan las imágenes.
5. **Validar límites antes de procesar:** tipo `image/*`, tamaño/cantidad máximos → descartar y avisar.
6. **Liberar memoria:** limpiar arrays de blobs y revocar object URLs en `ngOnDestroy`.
7. **Carga perezosa en el detalle:** los listados nunca traen Base64; el visor pide la imagen on-demand vía `HttpClient` (así el `AuthInterceptor` agrega el JWT) y construye un object URL para `<img>`. Nota: no usar `<img src="/api/...">` directo porque no llevaría el header `Authorization`.

> **Recomendación de arquitecto (decisión abierta §12):** si se acepta apartarse del Base64, `multipart/form-data` con bytes crudos elimina el overhead del 33 % y es el camino preferible a producción (encaja con el S3 ya disponible). El diseño deja la puerta abierta sin reescrituras. Mientras tanto, optimizamos Base64 como pide el PRD.

Snippet de compresión (núcleo de `image-compression.util.ts`):
```ts
export async function compressImage(file: File, maxSide = 1600, quality = 0.7): Promise<Blob> {
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const w = Math.round(bitmap.width * scale), h = Math.round(bitmap.height * scale);
  const canvas = new OffscreenCanvas(w, h);
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, w, h);
  bitmap.close();
  return canvas.convertToBlob({ type: 'image/webp', quality }); // fallback image/jpeg
}
export const blobToBase64 = (blob: Blob) => new Promise<string>((res, rej) => {
  const r = new FileReader(); r.onload = () => res(r.result as string); r.onerror = rej; r.readAsDataURL(blob);
});
```

---

## 10. Reglas de negocio / criterios de aceptación

1. Crear ticket: estado `ABIERTO`; `pais_id`, `company_id`, `created_by_user_id` **del contexto** (nunca del body); `tipo` validado contra catálogo; `titulo`/`descripcion` requeridos.
2. Solicitante solo ve/edita **sus** tickets (`created_by == UserId`).
3. Resolutor ve tickets de **su país/empresa**; no ve otros países.
4. Super Admin ve **todo**, con filtros multi-país/estado/tipo.
5. `tomar`: `ABIERTO → EN_ANALISIS`, asigna resolutor; idempotente si ya está tomado.
6. Cambios de estado validados por la máquina de estados; transición inválida ⇒ `400`. Cada cambio puede registrar nota con `estado_resultante`.
7. **Imágenes nunca en listados**; detalle trae solo metadata; Base64 solo en el endpoint por-imagen.
8. Soft-delete (`deleted_at`); todas las queries excluyen borrados y filtran por `company_id`.

### Casos de prueba (integración backend — disciplina de `CLAUDE.md`)
- `POST /tickets` sin título → `400`.
- `POST /tickets` con `tipo` inválido → `400`.
- `GET /mis-tickets` del usuario A **no** incluye tickets del usuario B.
- `GET /gestion` con país 1 **no** muestra tickets de país 2.
- `POST /{id}/tomar` cambia estado a `EN_ANALISIS` y asigna; segunda llamada no rompe (idempotente).
- `PATCH /{id}/estado` con transición inválida → `400`.
- `GET /{id}` **no** trae Base64; `GET /{id}/imagenes/{imgId}` **sí**.
- Super Admin `GET /admin` ve multi-país.

---

## 11. Plan de implementación por fases

**Fase 1 — Datos + Dominio:** entidades `Ticket`/`TicketImagen`/`TicketNota`, constantes de tipos/estados, configs EF, DbSets, migración EF idempotente. Probar `dotnet ef database update` local.
**Fase 2 — Application:** `ITicketService` + DTOs.
**Fase 3 — Infrastructure:** `TicketService` (scoping por rol/país, máquina de estados, generación de `codigo`). Pruebas de integración de los casos §10.
**Fase 4 — API:** `TicketsController` + DI en `Program.cs`. Validar endpoints con requests reales (sin imágenes en listados).
**Fase 5 — Frontend modelos/servicio:** `ticket.models.ts`, `ticket.service.ts`, `image-compression.util.ts` (+ worker).
**Fase 6 — Frontend UI:** stepper, badges, dropzone, mis-tickets, detalle/timeline, gestión, admin; rutas con `permissionGuard`; menú.
**Fase 7 — Permisos:** sembrar `tickets.crear` / `tickets.gestionar` / `tickets.admin` y asignarlos a roles.
**Fase 8 — Validación end-to-end** y limpieza de servicios locales (`make down`).

---

## 12. Decisiones abiertas (con recomendación)

1. **Almacenamiento de imágenes:** Base64 en BD (PRD) ✅ por defecto · *Recomendado a futuro:* S3 + `s3_key`. → **Default: Base64**, schema preparado para migrar.
2. **Base64 vs multipart/form-data:** PRD pide Base64 ✅ · *Recomendado:* multipart (sin overhead 33 %). → **Default: Base64 optimizado** (§9).
3. **Especialidad del resolutor:** filtro por `tipo` en front (v1) vs tabla `ticket_resolutor_especialidad`. → **Default: filtro front**, tabla opcional luego.
4. **Código legible de ticket (`TK-2026-NNNNNN`):** útil para soporte. → **Default: sí**, secuencia por año/empresa.
5. **Catálogo de tipos/estados:** constantes en código (state machine) vs `MasterList` dinámico. → **Default: constantes** (más seguro para la máquina de estados).
