# Tracker — Gestión y Admin de Tickets

## Checklist
- [x] Backend — `TicketDtos.cs`: agregar `AssignedToGuid` a `TicketSearchRequest` + nuevo `ResolutorListItemDto`
- [x] Backend — `ITicketService.cs`: agregar `GetResolutoresAdminAsync`
- [x] Backend — `TicketService.SearchGestionAsync`: simplificar a `AssignedToUserGuid == currentUser.UserGuid`
- [x] Backend — `TicketService.SearchAdminAsync`: quitar filtro de empresa, query global de todos los tickets
- [x] Backend — `TicketService.ApplyFilters`: agregar filtro por `AssignedToGuid`
- [x] Backend — `TicketService.GetResolutoresAdminAsync`: nuevo método — resolutores con tickets asignados
- [x] Backend — `TicketsController.Admin`: agregar `assignedToGuid` query param
- [x] Backend — `TicketsController`: nuevo endpoint `GET /api/tickets/admin/resolutores`
- [x] Frontend — `ticket.models.ts`: agregar `assignedToGuid` a `TicketListFilter` + `ResolutorAdminDto`
- [x] Frontend — `ticket.service.ts`: agregar `getResolutoresAdmin()`
- [x] Frontend — `admin-tickets.component.ts`: cargar países + resolutores, pasar `assignedToGuid` al filtro
- [x] Frontend — `admin-tickets.component.html`: reemplazar País (Id) → select real; agregar select Resolutor
- [x] Frontend — `gestion-tickets.component.html`: actualizar subtítulo
- [x] Build backend (Infrastructure) — 0 errores, 5 warnings preexistentes
- [x] Build frontend (`yarn build`) — 0 errores, solo warning preexistente de bundle budget
- [ ] Reiniciar backend para que tome todos los cambios
- [ ] Verificación funcional
