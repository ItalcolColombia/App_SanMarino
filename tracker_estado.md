# Tracker — Tickets: notificados + correos (creación/cierre) + transferir + logo "pro"

**Plan:** [tickets_notificados_flujos_plan.md](./fase_de_desarrollo/tickets_notificados_flujos_plan.md)
**Módulo:** `tickets` · back `ZooSanMarino.*` · front `frontend/src/app/features/tickets`
> (El estado del trabajo previo de **postura Colombia** quedó en memoria `postura-colombia-guia-genetica.md` + su plan/matriz en `fase_de_desarrollo/`.)

## Fase 0 — Auditoría (COMPLETADA)
- [x] Mapeado back: `Ticket`/estados/tipos/`TicketNota`(chat), `TicketService` (Create/Transferir/CambiarEstado/ConfirmarCierre), notificación actual solo a solicitante al SOLUCIONAR.
- [x] Mapeado correo: `IEmailQueueService` (cola async) + plantillas "pro" welcome/password (logo = texto). `BuildSolucionEmailBody` simple sin logo.
- [x] Mapeado front: `ticket-create` (sin notificados), `ticket-detalle` (transferir), service/models.
- [x] Validado en app: create form carga OK (tipos por país + resolutores); login re-hecho.
- [x] Gaps: sin notificados, sin correo creación/cierre, transferir no notifica al nuevo resolutor, correos sin logo.

## Fase 1 — Backend (notificados + correos + transferir) — c-sharp-pro ✅ COMPLETADA
- [x] Entidad `TicketNotificado` + config EF (`ticket_notificados`, FK cascade, índice) — ApplyConfigurationsFromAssembly, sin registro manual.
- [x] Navegación `Ticket.Notificados` + `DbSet<TicketNotificado>` en el contexto.
- [x] Migración idempotente `20260701141132_AddTicketNotificados` (CREATE TABLE/INDEX IF NOT EXISTS).
- [x] DTOs: `CreateTicketRequest.NotificarUserGuids` (opcional), `TicketNotificadoDto`, `TicketDetailDto.Notificados`, `UsuarioNotificableDto`.
- [x] `ITicketService.GetNotificablesAsync` + impl (usuarios company vía UserRoles con email, excluye actual).
- [x] `CreateAsync`: persistir notificados + encolar "ticket_creado" (try/catch).
- [x] `ConfirmarCierreAsync`: encolar "ticket_cerrado" (solución + notas públicas) a solicitante + notificados.
- [x] `TransferirAsync`: encolar "ticket_transferido" al nuevo resolutor.
- [x] `TicketEmailTemplates` branded con **logo imagen** (Wrap + Creado/Asignado/Cerrado con tabla de chat) + `Email:LogoUrl` en appsettings.
- [x] `GET /api/tickets/notificables` en `TicketsController`.
- [x] `dotnet build` 0 err (5 warnings preexistentes) + `dotnet test` 26/26 PASS + migración generada (no aplicada por el agente).

## Fase 2 — Frontend (multiselect notificados + detalle + transferir) — frontend-developer ✅ COMPLETADA
- [x] Models: `notificarUserGuids`, `TicketNotificadoDto`, `UsuarioNotificableDto`, `TicketDetail.notificados`.
- [x] Service: `getNotificables()` → GET {baseUrl}/notificables.
- [x] `ticket-create`: multi-select "Notificar a" (buscador role=combobox + chips, máx 8 resultados, sin duplicados, signals) + `notificarUserGuids` en submit. Importó FormsModule para el ngModel del buscador.
- [x] `ticket-detalle`: sección "Notificados" (chips) en panel Detalles si `t.notificados?.length`.
- [x] Transferir: copy "El nuevo resolutor será notificado por correo".
- [x] `yarn build` OK (0 errores; solo warning preexistente de bundle size).
- [ ] Pendiente: validación visual en el navegador (el agente no tenía tools de browser) — la hago yo vía preview MCP en la integración.

## Fase 3 — Integración + validación en app (arquitecto) ✅ COMPLETADA
- [x] **Incidente de seguridad detectado y contenido:** relanzar con `--no-launch-profile` SIN `ASPNETCORE_ENVIRONMENT=Development` cargó `appsettings.json` (PROD) → intentó migrar la RDS `sanmarinoappprod` (falló por timeout de red, sin daño). Fix: relanzar con `$env:ASPNETCORE_ENVIRONMENT="Development"` → BD local :5433. **Lección: SIEMPRE fijar el env Development al relanzar el backend.**
- [x] Rebuild back Debug (kill :5002 + relanzar env Development) 0 err; front ya buildeado + hot-reload.
- [x] Migración aplicada en LOCAL al arrancar: tabla `ticket_notificados` creada + en `__EFMigrationsHistory`.
- [x] `GET /tickets/notificables` → **200** (10 usuarios) tras el restart (antes 404). Multiselect carga los 10.
- [x] Crear ticket TK-2026-000001 con 2 notificados (Alexander Mejia, Alex Londoño) → **persistidos** en `ticket_notificados` + **2 correos "ticket_creado" enviados** (status=sent) con HTML branded + **logo imagen**.
- [x] Detalle del ticket: **chips de Notificados renderizan** (tras recarga de chunk; era caché viejo, no bug).
- [x] Correos "ticket_cerrado" y "ticket_transferido" **renderizados en modo controlado (fuera de la app, sin SMTP)** vía mini-proyecto `scratchpad/emailrender` → HTML: logo + marca + solución + **tabla de histórico de chat** (3 filas autor/fecha/nota) / "te asignaron".
- [x] Consola/red sin errores nuevos (los 404 de /notificables son PRE-restart; el mis-tickets ABORTED es navegación cancelada).
- [ ] Nota: transferir/cerrar por flujo real end-to-end no ejecutado en UI (requiere 2do usuario resolutor); wiring implementado + build + tests OK + plantillas renderizadas. Opcional a futuro.
- [ ] PROD: al desplegar, la migración `AddTicketNotificados` se aplica sola; agregar la sección `Email:LogoUrl` en la config de prod si el logo debe apuntar a un dominio público (hoy default = {ApplicationUrl}/assets/brand/...).

## Fase 4 — Estilo/tema del módulo (pedido usuario) ✅ COMPLETADA
- [x] **Fondo se veía oscuro** en modo oscuro del navegador (`prefers-color-scheme: dark`): el wrapper usaba `bg-gradient-to-b from-ital-cream/60 to-transparent` (SEMI-TRANSPARENTE) → el canvas oscuro del navegador se filtraba. Fix: fondo **opaco claro con degradado naranja** `min-h-full bg-white bg-gradient-to-b from-ital-orange-50 via-white to-white` en las **5 páginas** (mis-tickets, ticket-create, ticket-detalle, gestion-tickets, admin-tickets). Verificado en vivo: claro incluso en modo oscuro.
- [x] Chip de filtro activo "Todos"/estados en mis-tickets pasó de `bg-slate-800`/`ring-slate-800` (azul oscuro, fuera de paleta) a **`bg-ital-green`/`ring-ital-green`** para concordar con el botón "Nuevo ticket" y la app. (Botones ya usaban `from-ital-green`; bordes ya slate-100/200 estándar.)
- [x] ng serve recompiló OK (builds 21:10 y 21:13 "complete"). Verificado visualmente mis-tickets + ticket-create (fondo claro + naranja, filtro verde).
