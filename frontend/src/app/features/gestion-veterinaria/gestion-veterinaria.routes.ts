import { Routes } from '@angular/router';

export const GESTION_VETERINARIA_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/gestion-veterinaria/gestion-veterinaria.page')
      .then((m) => m.GestionVeterinariaPage),
    title: 'Gestión veterinaria',
  },
  {
    path: 'tareas/:id/cumplir',
    loadComponent: () => import('./pages/cumplir-tarea/cumplir-tarea.page')
      .then((m) => m.CumplirTareaPage),
    title: 'Cumplir tarea de campo',
  },
];
