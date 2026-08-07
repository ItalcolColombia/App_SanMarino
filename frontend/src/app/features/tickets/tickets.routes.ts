// src/app/features/tickets/tickets.routes.ts
import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/permission.guard';
import { TICKET_PERMS } from './models/ticket.models';

/** Cualquiera de los 3 permisos del módulo habilita ver la bandeja/detalle. */
const ANY_TICKET_PERM = [TICKET_PERMS.crear, TICKET_PERMS.gestionar, TICKET_PERMS.admin];

/**
 * Rutas del módulo de tickets (standalone, lazy).
 * Gating por permiso con permissionGuard (data.permissions). El `:id` va último
 * para no capturar las rutas literales (nuevo/gestion).
 *
 * Tickets quedó con lo que usa el negocio: **Mis solicitudes** (crear y seguir el propio caso) y
 * la **Bandeja de gestión** del resolutor. Todo lo que es gestión del área de desarrollo —tablero,
 * roadmap, panel, configuración y mis asignados— se mudó a **ItalJira** (`/italjira/*`).
 * Las rutas viejas se conservan como REDIRECT: un enlace guardado o un correo antiguo siguen
 * funcionando en vez de caer en un 404.
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
    path: 'gestion',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.gestionar, TICKET_PERMS.admin] },
    loadComponent: () =>
      import('./pages/gestion-tickets/gestion-tickets.component').then(m => m.GestionTicketsComponent),
  },

  // ── Mudadas a ItalJira: se conservan como redirect para no romper enlaces guardados ──
  { path: 'asignados', pathMatch: 'full', redirectTo: '/italjira/asignados' },
  { path: 'admin',     pathMatch: 'full', redirectTo: '/italjira/configuracion' },
  { path: 'tablero',   pathMatch: 'full', redirectTo: '/italjira/tablero' },
  { path: 'roadmap',   pathMatch: 'full', redirectTo: '/italjira/roadmap' },
  { path: 'panel',     pathMatch: 'full', redirectTo: '/italjira/panel' },

  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { permissions: ANY_TICKET_PERM },
    loadComponent: () =>
      import('./pages/ticket-detalle/ticket-detalle.component').then(m => m.TicketDetalleComponent),
  },
];
