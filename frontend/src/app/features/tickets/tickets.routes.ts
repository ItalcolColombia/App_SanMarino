// src/app/features/tickets/tickets.routes.ts
import { Routes } from '@angular/router';

/**
 * Rutas del módulo de tickets (standalone, lazy).
 * En la Fase 7 se agregan los permisos (permissionGuard + data.permissions) y
 * en la siguiente entrega: detalle (stepper/timeline), gestión y admin.
 */
export const TICKETS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/mis-tickets/mis-tickets.component').then(m => m.MisTicketsComponent),
  },
  {
    path: 'nuevo',
    loadComponent: () =>
      import('./pages/ticket-create/ticket-create.component').then(m => m.TicketCreateComponent),
  },
];
