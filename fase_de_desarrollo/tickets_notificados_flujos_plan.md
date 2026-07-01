# Plan — Tickets: notificados + notificaciones por correo (creación/cierre) + transferir + correos "pro" con logo

**Autor:** Claude (arquitecto) · **Fecha:** 2026-07-01
**Módulo:** `tickets` (back `ZooSanMarino.*`, front `frontend/src/app/features/tickets`)

## Objetivo (pedido del usuario)
1. **Validar** el módulo de tickets (flujos solicitante / resolutor / admin).
2. **Mejorar el flujo de transferir** (lo que falte).
3. Al **crear** un ticket, poder **agregar personas que reciben notificación** ("notificados" / copiados).
4. **Notificación de creación** a los notificados: info del ticket (código, título, tipo, descripción), quién lo creó y a quién se asignó.
5. **Notificación de cierre** a los notificados: resumen de lo realizado (solución) + histórico del chat (notas públicas).
6. **Correo con el logo de la app**, estilo "pro" igual al de creación de contraseña / bienvenida.
7. Trazabilidad: los notificados quedan enterados sin ser el solicitante ni el resolutor.

## Estado real hoy (auditoría en código + app corriendo)
- **Entidad `Ticket`** con máquina de estados (`TicketEstados.Transiciones`), tipos (`TicketTipos`), notas (`TicketNota` = chat/bitácora), imágenes, adjuntos. Multi-tenant por company + país (de `ICurrentUser`, nunca del body).
- **Correo:** infra por cola async `IEmailQueueService.EnqueueEmailAsync(to, subject, body, tipo, metadataJson)` procesada por `EmailQueueProcessorService`. `EmailService` tiene plantillas HTML "pro" (marca `#f4b428`, header con nombre+tagline, botón, footer) para **password recovery** y **welcome** — **logo es TEXTO**, no imagen.
- **Notificación tickets hoy:** SOLO al **solicitante** cuando el resolutor marca **SOLUCIONADO** (`CambiarEstadoAsync` → `BuildSolucionEmailBody`, header verde simple, sin logo). No hay correo de creación, ni de cierre, ni a terceros.
- **Transferir:** `TransferirAsync` solo `REQUERIMIENTO → DESARROLLO`, reasigna y agrega nota `TRANSFERIDO`. **No notifica** al nuevo resolutor ni a nadie. Front: modal en `ticket-detalle` con dropdown de resolutores DESARROLLO del país.
- **Crear (front):** `ticket-create` tiene Título, Tipo (por país, con conteo de resolutores), resolutor asignado, Descripción, Imágenes, Adjuntos. **No hay** selector de notificados.
- **Fuente de usuarios:** `GET /api/users` (`UserListDto`) y `TicketPerfilService` (asignables). Emails vía `User.UserLogins → Login.email` (patrón de `ResolveSolicitanteEmailAsync`).
- **Logos disponibles:** `frontend/src/assets/brand/` (`logo_intalfoods_zootenico.png`, `Logo-sanmarino-innovacion.png`, `icono-logo.png`, …).

## Decisiones de diseño (defaults asumidos, senior)
- **Notificados = usuarios registrados** de la empresa efectiva con email (para tener correo + poder mostrarlos en la app). Multi-select en el create. (No email libre externo en v1.)
- **Notificaciones a notificados = 2 hitos**: creación y cierre (CERRADO por el solicitante). No spamear cada cambio de estado.
- **Histórico de chat en el cierre = notas NO internas** (`EsInterna == false`), en orden cronológico, con autor + fecha.
- **Correos** pasan a un layout branded compartido **con logo (imagen)**. URL del logo **configurable** `Email:LogoUrl` (default `{ApplicationUrl}/assets/brand/logo_intalfoods_zootenico.png`).
- **Transferir**: además de reasignar, **notificar al nuevo resolutor** (te asignaron un ticket) y registrar nota. Mantener regla REQUERIMIENTO→DESARROLLO (negocio), pero notificar el evento.

---

## CONTRATO (fuente de verdad para implementación)

### Backend

**Nueva entidad** `Domain/Entities/TicketNotificado.cs`
```
Id (long, PK), TicketId (long, FK→tickets), UserGuid (Guid?), Cedula (string?),
Email (string, requerido), Nombre (string?), CreatedAt (DateTime), CreatedByUserId (int)
```
- Config EF `TicketNotificadoConfiguration` → tabla **`ticket_notificados`**, índice `(ticket_id)`, FK a `tickets` con `OnDelete(Cascade)`, snake_case.
- Navegación en `Ticket`: `ICollection<TicketNotificado> Notificados`.

**Migración** `AddTicketNotificados` (idempotente): `CREATE TABLE IF NOT EXISTS ticket_notificados (...)` + `CREATE INDEX IF NOT EXISTS`. No tocar `__EFMigrationsHistory` a mano.

**DTOs** (`TicketDtos.cs`):
- `CreateTicketRequest` += `List<Guid>? NotificarUserGuids` (al final, opcional → no rompe el front actual).
- Nuevo `TicketNotificadoDto(long Id, Guid? UserGuid, string? Nombre, string Email)`.
- `TicketDetailDto` += `IReadOnlyList<TicketNotificadoDto>? Notificados = null` (al final).
- Nuevo `UsuarioNotificableDto(Guid Guid, string Nombre, string Email, string? Rol)`.

**Interfaz `ITicketService`** += `Task<IReadOnlyList<UsuarioNotificableDto>> GetNotificablesAsync(CancellationToken ct)`.

**`TicketService`** (respetar patrón; si el archivo crece, partial en `Funciones/`):
- `GetNotificablesAsync`: usuarios de la company efectiva con email (Guid, nombre, email, rol), excluyendo al usuario actual. Reusar patrón de emails.
- `CreateAsync`: tras crear, resolver y persistir `TicketNotificado` por cada `NotificarUserGuids` (email desde `User.UserLogins`), y **encolar correo "ticket_creado"** a cada notificado con `BuildTicketCreadoEmailBody(ticket, creadorNombre, asignadoNombre)`.
- Cierre: al pasar a **CERRADO** (`ConfirmarCierreAsync`), **encolar "ticket_cerrado"** al solicitante + notificados con `BuildTicketCerradoEmailBody(ticket, notasPublicas)`.
- `TransferirAsync`: encolar "ticket_transferido" al nuevo resolutor (`BuildTicketAsignadoEmailBody`).
- Emails no bloquean la operación (try/catch como el actual).
- `GetByIdInternalAsync` / mapeos: incluir `Notificados`.

**Plantillas email** — mover el layout branded a un helper reutilizable con **logo imagen** (nuevo `TicketEmailTemplates` estático en Infrastructure, o extender EmailService). Encabezado/pie idénticos a welcome (marca `#f4b428`, `<img src="{LogoUrl}" .../>`). 3 cuerpos nuevos: creado, asignado/transferido, cerrado (resumen + tabla de chat).

**Controller** `TicketsController`: `GET /api/tickets/notificables` → `GetNotificablesAsync`. Verificar que `CreateTicketRequest` con el nuevo campo fluya.

**Config**: `Email:LogoUrl` en `appsettings*.json` (default señalado). Leer en el builder.

### Frontend

**Models** (`ticket.models.ts`):
- `CreateTicketRequest` += `notificarUserGuids?: string[]`.
- `TicketDetail` += `notificados?: TicketNotificadoDto[]`.
- Nuevos `TicketNotificadoDto { id; userGuid; nombre; email }`, `UsuarioNotificableDto { guid; nombre; email; rol }`.

**Service** (`ticket.service.ts`): `getNotificables(): Observable<UsuarioNotificableDto[]>` → `GET {base}/notificables`.

**`ticket-create`**: multi-select "Notificar a (opcional)" — buscador + chips de seleccionados; carga `getNotificables()` en `ngOnInit`; manda `notificarUserGuids` en el submit. Estilo Tailwind Italfoods, accesible.

**`ticket-detalle`**: mostrar los notificados (chips) en el panel de info; mensajes toast ya existentes.

**Transferir**: mensajería/labels; confirmar que el nuevo resolutor recibe correo (info al usuario).

## Validación (obligatoria)
- Back: `cd backend && dotnet build` (0 errores, sin nuevas advertencias) + `dotnet ef migrations add AddTicketNotificados` (idempotente) + `dotnet test`.
- Front: `cd frontend && yarn build`.
- App corriendo: crear ticket con notificados → ver 2 correos encolados (creado); cerrar → correo de cierre con resumen+chat; transferir → correo al nuevo resolutor. Revisar consola/red sin errores.
- Correos: validar HTML (logo carga, marca, resumen, tabla de chat) — inspeccionar el body encolado en `email_queue`.

## Casos de prueba
1. Crear ticket sin notificados → funciona igual que hoy (campo opcional).
2. Crear con 2 notificados → 2 filas en `ticket_notificados` + 2 correos "ticket_creado" en cola.
3. Cerrar (solicitante confirma) → correo "ticket_cerrado" a solicitante + notificados, con solución + notas públicas (sin internas).
4. Transferir REQUERIMIENTO→DESARROLLO → correo al nuevo resolutor; error claro si no es resolutor DESARROLLO.
5. Notificables excluye al usuario actual y solo trae los que tienen email.
6. Idempotencia migración: correr dos veces no falla.
