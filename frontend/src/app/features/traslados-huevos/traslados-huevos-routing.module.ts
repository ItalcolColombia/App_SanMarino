import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  {
    path: '',
    redirectTo: 'lista',
    pathMatch: 'full'
  },
  {
    path: 'lista',
    loadComponent: () => import('./pages/traslados-huevos-list/traslados-huevos-list.component')
      .then(m => m.TrasladosHuevosListComponent),
    title: 'Traslados de Huevos'
  },
  {
    path: 'nuevo',
    loadComponent: () => import('./pages/traslado-huevos-form/traslado-huevos-form.component')
      .then(m => m.TrasladoHuevosFormComponent),
    title: 'Nuevo Traslado de Huevos'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class TrasladosHuevosRoutingModule { }
