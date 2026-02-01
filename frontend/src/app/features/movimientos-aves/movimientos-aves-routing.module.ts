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
    loadComponent: () => import('./pages/movimientos-aves-list/movimientos-aves-list.component')
      .then(m => m.MovimientosAvesListComponent),
    title: 'Movimientos de Aves'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class MovimientosAvesRoutingModule { }
