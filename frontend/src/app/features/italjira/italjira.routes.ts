// src/app/features/italjira/italjira.routes.ts
import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/permission.guard';
import { TICKET_PERMS } from '../tickets/models/ticket.models';

/**
 * ItalJira es el módulo del ÁREA DE DESARROLLO: gestión de historias, tareas, tiempos y roadmap.
 * No lo ven los usuarios finales, así que todas sus rutas exigen permiso de gestión o de
 * administración del módulo — los mismos que ya protegían estas vistas cuando vivían en
 * `/tickets/*`. Reutilizarlos (en vez de inventar permisos nuevos) es lo que hace que los roles ya
 * configurados sigan entrando después del deploy.
 */
const GESTION = [TICKET_PERMS.gestionar, TICKET_PERMS.admin];

export const ITALJIRA_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'backlog',
  },
  {
    // El árbol historia → tarea → subtarea/bug + la bandeja «sin historia».
    path: 'backlog',
    canActivate: [permissionGuard],
    data: { permissions: GESTION },
    loadComponent: () =>
      import('./pages/backlog/backlog.component').then(m => m.BacklogComponent),
  },
  {
    path: 'tablero',
    canActivate: [permissionGuard],
    data: { permissions: GESTION },
    loadComponent: () =>
      import('./pages/tablero/tablero.component').then(m => m.TableroComponent),
  },
  {
    path: 'roadmap',
    canActivate: [permissionGuard],
    data: { permissions: GESTION },
    loadComponent: () =>
      import('./pages/roadmap/roadmap.component').then(m => m.RoadmapComponent),
  },
  {
    path: 'panel',
    canActivate: [permissionGuard],
    data: { permissions: GESTION },
    loadComponent: () =>
      import('./pages/panel/panel.component').then(m => m.PanelComponent),
  },
  {
    path: 'asignados',
    canActivate: [permissionGuard],
    data: { permissions: GESTION },
    loadComponent: () =>
      import('./pages/mis-asignados/mis-asignados.component').then(m => m.MisAsignadosComponent),
  },
  {
    // Antes `/tickets/admin`. El nombre cambió también porque AWS WAF devuelve 403 a cualquier
    // path que contenga `admin` — incidente ya documentado en el repo.
    path: 'configuracion',
    canActivate: [permissionGuard],
    data: { permissions: [TICKET_PERMS.admin] },
    loadComponent: () =>
      import('./pages/configuracion/configuracion.component').then(m => m.ItalJiraConfiguracionComponent),
  },
];
