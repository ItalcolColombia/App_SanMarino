# Tracker — Reorg tickets: "quién crea" vs "quién recibe"

**Plan:** [17_tickets_reorg_solicitante_resolutor_plan.md](fase_de_desarrollo/17_tickets_reorg_solicitante_resolutor_plan.md)
**Sin migración de BD.** Reutiliza `ticket_resolutor_roles` + `ticket_resolutores`.

## Backend
- [x] `ITicketPerfilService`: overload `SeedPerfilDesdeRolAsync(userId, roleId, companyId, ct)` + `ReaplicarPlantillaRolAsync(roleId, ct)`
- [x] `TicketPerfilService`: implementar overload por empresa (el existente delega) + `ReaplicarPlantillaRolAsync`
- [x] `UserService`: inyectar `ITicketPerfilService`; auto-seed post-commit en `CreateAsync` y `UpdateAsync` (try/catch, no rompe el alta)
- [x] `TicketPerfilesController`: `POST /rol/{roleId}/reaplicar`
- [x] `dotnet build` 0 errores + `dotnet test` (16+1 OK)

## Frontend
- [x] `ticket-perfil.service.ts`: `reaplicarPlantillaRol(roleId)`
- [x] `ticket-perfil-editor.html`: 2 tarjetas mode-aware (Usuario: ①Creación/②Atención · Rol: Plantilla + nota + botón aplicar)
- [x] `ticket-perfil-editor.ts`: `reaplicarPlantilla()` (modo rol)
- [x] `role-management.component.html`: alinear título externo (sin doble título)
- [x] `modal-create-edit.component.html`: intro corta en el tab
- [x] `yarn build` (OK; warning de budget preexistente)

## Validación
- [x] Actualizar `backend/documentacion/MODULO_TICKETS.md`
- [ ] E2E manual (HMR) de los 6 casos del plan — pendiente con el usuario
- [ ] `make down` (sin procesos huérfanos) tras el e2e
