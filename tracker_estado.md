# Tracker: Módulo Sistema Centralizado de Tickets de Soporte

**Plan:** [14_modulo_tickets_soporte_plan.md](fase_de_desarrollo/14_modulo_tickets_soporte_plan.md)
**Fecha:** 2026-06-04
**Estado:** 🟢 BACKEND IMPLEMENTADO Y VALIDADO (Fases 1–4) · 🟡 Frontend pendiente (Fases 5–8)
**Arquitectura:** Hexagonal / Ports & Adapters (misma del proyecto, plantilla = vertical slice `Lesion`)

> **Validación 2026-06-04:** `dotnet build` OK (0 errores) · migración `AddTicketsModule` aplicada a la BD local `sanmarinoapplocal` (3 tablas + índices) · smoke test HTTP completo verde (crear/validaciones/listado-sin-base64/tomar/transiciones/imágenes/bandejas). Datos de prueba truncados, API detenida, sin procesos huérfanos.

---

## Checklist de implementación

### Fase 1 — Datos + Dominio ✅
- [x] Entidad `Ticket : AuditableEntity` ([Ticket.cs](backend/src/ZooSanMarino.Domain/Entities/Ticket.cs))
- [x] Entidad `TicketImagen` ([TicketImagen.cs](backend/src/ZooSanMarino.Domain/Entities/TicketImagen.cs))
- [x] Entidad `TicketNota` ([TicketNota.cs](backend/src/ZooSanMarino.Domain/Entities/TicketNota.cs))
- [x] Constantes `TicketTipos` / `TicketEstados` + diccionario de transiciones ([TicketConstants.cs](backend/src/ZooSanMarino.Domain/Entities/TicketConstants.cs))
- [x] `TicketConfiguration` (tabla `tickets`, snake_case, índices)
- [x] `TicketImagenConfiguration` (tabla `ticket_imagenes`, FK cascade)
- [x] `TicketNotaConfiguration` (tabla `ticket_notas`, FK cascade)
- [x] DbSets en `ZooSanMarinoContext`
- [x] Migración EF `AddTicketsModule` + `dotnet ef database update` local OK

### Fase 2 — Application (puerto + DTOs) ✅
- [x] Interfaz `ITicketService`
- [x] DTOs entrada: `CreateTicketRequest`, `AddTicketImagenesRequest`, `CambiarEstadoTicketRequest`, `CreateTicketNotaRequest`, `TicketSearchRequest`
- [x] DTOs salida: `TicketListItemDto`, `TicketDetailDto`, `TicketNotaDto`, `TicketImagenMetaDto`, `TicketImagenDto`

### Fase 3 — Infrastructure (adaptador) ✅
- [x] `TicketService` — `CreateAsync` (contexto país/usuario, estado `ABIERTO`, código `TK-YYYY-NNNNNN`)
- [x] `TicketService` — `SearchMisTicketsAsync` (scope `created_by == UserId`, sin base64)
- [x] `TicketService` — `SearchGestionAsync` (scope país/empresa)
- [x] `TicketService` — `SearchAdminAsync` (global empresa, filtros opcionales)
- [x] `TicketService` — `GetByIdAsync` (notas + metadata imágenes, sin base64 inline)
- [x] `TicketService` — imágenes: `GetImagenesMetaAsync`, `GetImagenAsync` (on-demand), `AddImagenesAsync`
- [x] `TicketService` — `TomarAsync` (ABIERTO→EN_ANALISIS, asigna, idempotente)
- [x] `TicketService` — `CambiarEstadoAsync` (valida máquina de estados, registra nota)
- [x] `TicketService` — `AddNotaAsync`, `DeleteAsync` (lógico)

### Fase 4 — API ✅
- [x] `TicketsController` — Solicitante (crear, mis-tickets, detalle, imágenes, notas)
- [x] `TicketsController` — Resolutor (gestión, tomar, estado)
- [x] `TicketsController` — Super Admin (admin) + catálogos + delete
- [x] DI en `Program.cs` (`AddScoped<ITicketService, TicketService>()`)
- [x] Validación HTTP con requests reales (listados NO traen base64) ✓ smoke test

### Fase 5 — Frontend modelos/servicio ✅
- [x] `models/ticket.models.ts` (interfaces + tipos TipoTicket/EstadoTicket + PagedResult + helpers de UI)
- [x] `services/ticket.service.ts` (HttpClient, calca `lesion.service.ts`)
- [x] `services/image-compression.util.ts` (OffscreenCanvas/canvas + blobToBase64, con fallback)
- [ ] Web Worker de compresión (opcional — pendiente, mejora futura)

### Fase 6 — Frontend UI 🟡 (base lista)
- [ ] Componente `ticket-stepper` (horizontal desktop / vertical móvil) ← siguiente
- [x] Componente `ticket-estado-badge`
- [x] Componente `image-dropzone` (drag&drop, miniaturas object URL, progreso, compresión)
- [ ] Componente `image-lightbox` (carga base64 on-demand) ← siguiente
- [x] Página `ticket-create` (formulario + dropzone)
- [x] Página `mis-tickets` (filtro año/estado, badges, paginación)
- [ ] Página `ticket-detalle` (stepper + timeline + notas + galería lazy) ← siguiente
- [ ] Página `gestion-tickets` (bandeja resolutor) ← siguiente
- [ ] Página `admin-tickets` (bandeja super admin) ← siguiente
- [x] `tickets.routes.ts` (mis-tickets + nuevo) registrado en `app.config.ts` con `authGuard`
- [ ] Detalle: linkear "Ver" desde mis-tickets + `permissionGuard` + entrada de menú ← siguiente

> **Validación frontend 2026-06-04:** `ng build` (development) **completo sin errores**. Contrato tipado alineado con los DTOs del backend (ya verificados por smoke test HTTP).

### Fase 7 — Permisos ⬜
- [ ] Sembrar permisos `tickets.crear`, `tickets.gestionar`, `tickets.admin`
- [ ] Asignar a roles correspondientes

### Fase 8 — Validación end-to-end ⬜
- [ ] Casos de prueba §10 del plan desde el frontend real
- [ ] `make down` — apagar servicios locales tras pruebas

---

## Decisiones aplicadas (defaults del §12 del plan)
- [x] Almacenamiento imágenes: **Base64 en BD** (tabla preparada para migrar a S3)
- [x] Transporte: **Base64 optimizado** (compresión en cliente antes de enviar)
- [x] Especialidad resolutor: **filtro front** (tabla mapping queda como mejora futura)
- [x] Código legible `TK-2026-NNNNNN`: **sí** (generado tras obtener el Id)
- [x] Catálogo tipos/estados: **constantes** en dominio (máquina de estados)
