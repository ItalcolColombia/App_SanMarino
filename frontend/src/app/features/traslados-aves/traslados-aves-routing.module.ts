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
  },
  {
    path: 'nuevo',
    loadComponent: () => import('./pages/traslado-aves-huevos/traslado-aves-huevos.component')
      .then(m => m.TrasladoAvesHuevosComponent),
    title: 'Nuevo Traslado'
  },
  {
    path: 'registros',
    loadComponent: () => import('./pages/registros-traslados/registros-traslados.component')
      .then(m => m.RegistrosTrasladosComponent),
    title: 'Registros de Traslados'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class TrasladosAvesRoutingModule { }
