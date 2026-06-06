# Tracker: Tickets — Cierre doble + Adjuntos + Correo + Gestión segregada

**Plan:** [tickets_cierre_doble_adjuntos_correo.md](fase_de_desarrollo/tickets_cierre_doble_adjuntos_correo.md)
**Fecha:** 2026-06-05

## Fase 1 — Dominio ✅
- [x] `TicketEstados`: + CERRADO + transiciones
- [x] `Ticket`: + solucionDescripcion, fechaCierreSolicitante, cerradoPorUserId, notificadoCorreo, fechaNotificacionCorreo, correoNotificadoA
- [x] `TicketAdjunto` entidad + configuración EF + DbSet

## Fase 2 — Migración ✅
- [x] `20260605182358_AddTicketCierreAdjuntos` + idempotente (ADD COLUMN IF NOT EXISTS / CREATE TABLE IF NOT EXISTS)

## Fase 3 — Application ✅
- [x] DTOs: cambiar estado (+solucion), confirmar cierre, adjuntos, detail (+campos)
- [x] `ITicketService`: + ConfirmarCierre, AddDocumento, AddLink, GetAdjuntos, GetDocumento, DeleteAdjunto

## Fase 4 — Infrastructure (TicketService) ✅
- [x] `CambiarEstadoAsync`: bloquear creador + solucion obligatoria + encolar correo + bloquear CERRADO directo
- [x] `ConfirmarCierreAsync` (solo creador, desde SOLUCIONADO)
- [x] Adjuntos (documento/link/listar/descargar/eliminar) + `EsCreador`/`ResolveSolicitanteEmail`/`BuildSolucionEmailBody`
- [x] Inyectar `IEmailQueueService`; `TomarAsync` bloquea creador
- [x] `GetByIdInternalAsync`: + campos cierre + adjuntos

## Fase 5 — API ✅
- [x] Endpoints: confirmar-cierre, adjuntos (GET/documentos/links/descargar/DELETE)
- [x] Compila backend 0 errores

## Fase 6 — Frontend ✅
- [x] Modelos (estado CERRADO, tipos adjunto, requests, campos detail) + servicio (confirmarCierre, adjuntos)
- [x] Detalle: gestión solo `puedeGestionarTicket(t)`; modal solución obligatoria; panel cierre solicitante (confirmar/reabrir); panel "cerrado por ambas partes"; sección documentos/links (subir Excel/PDF + link + descargar/abrir); indicador "Notificado por correo"; sección Solución; estado CERRADO en badges/stepper
- [x] Backend: reapertura permitida al solicitante (SOLUCIONADO→EN_ANALISIS)

## Verificación ✅
- [x] Compila backend (0 errores) + frontend (bundle ok)

## Para probar (reiniciar backend :5002 — aplica migración al arrancar)
- [ ] Creador NO ve panel de gestión (no puede cambiar estado)
- [ ] Resolutor: "Solucionado" abre modal → descripción obligatoria → correo encolado al solicitante
- [ ] Solicitante: ve "Confirmar cierre"/"Reabrir"; al confirmar → CERRADO
- [ ] Subir Excel/PDF y link → aparecen y se descargan/abren
- [ ] Indicador "Notificado por correo a [email]"
