import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'lista', pathMatch: 'full' },
  {
    path: 'lista',
    loadComponent: () =>
      import('./pages/movimientos-pollo-engorde-list/movimientos-pollo-engorde-list.component').then(
        (m) => m.MovimientosPolloEngordeListComponent
      ),
    title: 'Movimiento de Pollo Engorde'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class MovimientosPolloEngordeRoutingModule {}
