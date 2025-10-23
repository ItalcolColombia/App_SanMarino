import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./pages/inventario-dashboard/inventario-dashboard.component')
      .then(m => m.InventarioDashboardComponent),
    title: 'Inventario de Aves - Dashboard'
  },
  {
    path: 'traslados',
    loadComponent: () => import('./pages/traslado-form/traslado-form.component')
      .then(m => m.TrasladoFormComponent),
    title: 'Traslado de Aves'
  },
  {
    path: 'movimientos',
    loadComponent: () => import('./pages/movimientos-list/movimientos-list.component')
      .then(m => m.MovimientosListComponent),
    title: 'Movimientos de Aves'
  },
  {
    path: 'historial',
    loadComponent: () => import('./pages/historial-trazabilidad/historial-trazabilidad.component')
      .then(m => m.HistorialTrazabilidadComponent),
    title: 'Historial y Trazabilidad'
  },
  {
    path: 'historial/:loteId',
    loadComponent: () => import('./pages/historial-trazabilidad/historial-trazabilidad.component')
      .then(m => m.HistorialTrazabilidadComponent),
    title: 'Trazabilidad de Lote'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class TrasladosAvesRoutingModule { }
