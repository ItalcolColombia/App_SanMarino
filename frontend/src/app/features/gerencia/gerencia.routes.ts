// src/app/features/gerencia/gerencia.routes.ts
import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/permission.guard';
import { TICKET_PERMS } from '../tickets/models/ticket.models';

/**
 * Gerencia: vistas de LECTURA para quien mira los números sin operar el módulo.
 *
 * Hoy tiene una sola pantalla, el Panel de control, que es el mismo componente que sirve
 * `/italjira/panel` — no una copia. La diferencia está en el permiso: acá entra
 * `tickets.indicadores`, que en el backend abre el alcance global SOLO en indicadores y reporte
 * (`TicketAlcancePanelCalculos`). Con ese permiso, `/italjira/*` sigue rebotando a `/home` y
 * `GET /api/tickets/tablero` sigue devolviendo únicamente los casos asignados.
 *
 * `tickets.admin` también entra, para que un administrador pueda usar el módulo si se lo asignan.
 */
const LECTURA_PANEL = [TICKET_PERMS.indicadores, TICKET_PERMS.admin];

export const GERENCIA_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'panel',
  },
  {
    path: 'panel',
    canActivate: [permissionGuard],
    data: { permissions: LECTURA_PANEL },
    loadComponent: () =>
      import('../italjira/pages/panel/panel.component').then(m => m.PanelComponent),
  },
];
