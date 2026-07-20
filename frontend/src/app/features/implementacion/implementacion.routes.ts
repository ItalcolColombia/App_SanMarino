// src/app/features/implementacion/implementacion.routes.ts
import { Routes } from '@angular/router';

/**
 * Rutas del módulo Implementación (standalone, lazy). El acceso al módulo se gobierna por menú
 * (role_menus); `planes/:id` va después de `planes` para no capturar la ruta literal.
 */
export const IMPLEMENTACION_ROUTES: Routes = [
  { path: '', redirectTo: 'planes', pathMatch: 'full' },
  {
    path: 'planes',
    loadComponent: () => import('./pages/planes-list/planes-list.page').then(m => m.PlanesListPage),
    title: 'Planes de implementación',
  },
  {
    path: 'planes/:id',
    loadComponent: () => import('./pages/plan-detail/plan-detail.page').then(m => m.PlanDetailPage),
    title: 'Checklist de implementación',
  },
  {
    path: 'mis-tareas',
    loadComponent: () => import('./pages/mis-tareas/mis-tareas.page').then(m => m.MisTareasPage),
    title: 'Mis tareas de implementación',
  },
];
