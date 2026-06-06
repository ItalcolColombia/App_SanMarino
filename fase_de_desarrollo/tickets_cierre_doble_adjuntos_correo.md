# Plan: Tickets — Cierre doble confirmación + Adjuntos + Correo + Gestión segregada

## Decisiones (confirmadas con el usuario)
1. **Cierre**: Resolutor pone SOLUCIONADO + descripción de solución (obligatoria). Solicitante revisa → "Confirmar cierre" → CERRADO (ambas partes). Si no conforme → reabre (EN_ANALISIS).
2. **Adjuntos**: Excel/PDF en BD (base64, mismo patrón que imágenes) + links como URL+título.
3. **Correo**: al marcar SOLUCIONADO se encola correo automático al solicitante con la solución (vía `IEmailQueueService`).
4. **Gestión**: el panel de gestión (Tomar / cambiar estado / transferir) solo lo ve quien atiende, NUNCA el creador.

## Dominio

### TicketEstados — agregar CERRADO
- `Cerrado = "CERRADO"`.
- Transiciones: `Solucionado → { EnAnalisis (reapertura), Cerrado (confirmar) }`; `Cerrado` = terminal.

### Ticket — campos nuevos
- `SolucionDescripcion` (string?) — la pone el resolutor al solucionar.
- `FechaCierreSolicitante` (DateTime?) + `CerradoPorUserId` (int?) — cierre del solicitante.
- `NotificadoCorreo` (bool) + `FechaNotificacionCorreo` (DateTime?) + `CorreoNotificadoA` (string?).

### TicketAdjunto (nueva entidad / tabla `ticket_adjuntos`)
- `Id, TicketId, Tipo ("ARCHIVO"|"LINK")`.
- Archivo: `ContenidoBase64, FileName, ContentType, SizeBytes`.
- Link: `Url, Titulo`.
- `CreatedByUserId, CreatedAt`.

## Migración EF idempotente (`AddTicketCierreAdjuntos`)
- ALTER tickets ADD COLUMN IF NOT EXISTS (solucion_descripcion, fecha_cierre_solicitante, cerrado_por_user_id, notificado_correo, fecha_notificacion_correo, correo_notificado_a).
- CREATE TABLE IF NOT EXISTS ticket_adjuntos.

## Application
- `CambiarEstadoTicketRequest` (+`SolucionDescripcion?`).
- Nuevo `ConfirmarCierreRequest` (nota opcional) y método `ConfirmarCierreAsync`.
- DTOs adjuntos: `TicketAdjuntoDto`, `AddTicketDocumentoRequest`, `AddTicketLinkRequest`, `TicketDocumentoDto` (descarga).
- `TicketDetailDto` (+`SolucionDescripcion, FechaCierreSolicitante, NotificadoCorreo, FechaNotificacionCorreo, Adjuntos[]`).

## Lógica (TicketService)
- `CambiarEstadoAsync`: rechaza si el actor es el creador (no gestiona su ticket); si SOLUCIONADO exige `SolucionDescripcion`, la guarda, encola correo al solicitante (`IEmailQueueService`), setea `NotificadoCorreo`.
- `ConfirmarCierreAsync`: solo el creador; estado debe ser SOLUCIONADO; → CERRADO + fecha/quien.
- `AddDocumentoAsync` / `AddLinkAsync` / `GetAdjuntosAsync` / `GetDocumentoAsync`.

## API (TicketsController)
- `PATCH {id}/estado` (ya existe; ahora acepta solucionDescripcion).
- `POST {id}/confirmar-cierre`.
- `POST {id}/documentos`, `POST {id}/links`, `GET {id}/adjuntos`, `GET {id}/adjuntos/{adjId}/descargar`.

## Frontend
- Modelos.
- Detalle:
  - Panel gestión `@if (puedeGestionar && !soyCreador)`.
  - Botón "Solucionado" → modal con textarea de descripción de solución (obligatoria).
  - Si `soyCreador && estado=SOLUCIONADO`: botones "Confirmar cierre" / "Reabrir" + mostrar descripción de la solución.
  - Sección "Documentos y links": subir Excel/PDF, agregar link, listar con descarga.
  - Indicador "Notificado por correo el [fecha] a [email]".
  - Badge de estado CERRADO.

## Pruebas
- Creador no ve panel de gestión.
- Resolutor soluciona sin descripción → error; con descripción → ok + correo encolado.
- Solicitante confirma cierre → CERRADO.
- Subir PDF/Excel y link → aparece y se descarga.
