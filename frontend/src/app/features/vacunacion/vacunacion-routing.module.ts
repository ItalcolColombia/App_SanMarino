import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'cronograma', pathMatch: 'full' },
  {
    path: 'cronograma',
    loadComponent: () =>
      import('./pages/cronograma-administracion/cronograma-administracion.page').then(
        (m) => m.CronogramaAdministracionPage
      ),
    title: 'Vacunación — Cronograma',
  },
  {
    path: 'registro',
    loadComponent: () =>
      import('./pages/registro-aplicacion/registro-aplicacion.page').then((m) => m.RegistroAplicacionPage),
    title: 'Vacunación — Registro de aplicación',
  },
  {
    path: 'reportes',
    loadComponent: () =>
      import('./pages/reportes-cumplimiento/reportes-cumplimiento.page').then(
        (m) => m.ReportesCumplimientoPage
      ),
    title: 'Vacunación — Reportes de cumplimiento',
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class VacunacionRoutingModule {}
