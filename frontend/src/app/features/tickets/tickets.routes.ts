// src/app/features/tickets/tickets.routes.ts
import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/permission.guard';
import { TICKET_PERMS } from './models/ticket.models';

/** Cualquiera de los 3 permisos del módulo habilita ver la bandeja/detalle. */
const ANY_TICKET_PERM = [TICKET_PERMS.crear, TICKET_PERMS.gestionar, TICKET_PERMS.admin];

/**
 * Rutas del módulo de tickets (standalone, lazy).
 * Gating por permiso con permissionGuard (data.permissions). El `:id` va último
 * para no capturar las rutas literales (nuevo/gestion/admin).
 */
export const TICKETS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard],
    data: { permissions: ANY_TICKET_PERM },
    loadComponent: () =>
      import('./pages/mis-tickets/mis-tickets.component').then(m => m.MisTicketsComponent),
  },
  {
    path: 'nuevo',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.crear] },
    loadComponent: () =>
      import('./pages/ticket-create/ticket-create.component').then(m => m.TicketCreateComponent),
  },
  {
    path: 'asignados',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.gestionar, TICKET_PERMS.admin] },
    loadComponent: () =>
      import('./pages/mis-asignados/mis-asignados.component').then(m => m.MisAsignadosComponent),
  },
  {
    path: 'gestion',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.gestionar, TICKET_PERMS.admin] },
    loadComponent: () =>
      import('./pages/gestion-tickets/gestion-tickets.component').then(m => m.GestionTicketsComponent),
  },
  {
    path: 'admin',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.admin] },
    loadComponent: () =>
      import('./pages/admin-tickets/admin-tickets.component').then(m => m.AdminTicketsComponent),
  },
  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { permissions: ANY_TICKET_PERM },
    loadComponent: () =>
      import('./pages/ticket-detalle/ticket-detalle.component').then(m => m.TicketDetalleComponent),
  },
];
