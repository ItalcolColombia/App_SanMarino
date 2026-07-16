import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'sincronizacion-panama', pathMatch: 'full' },
  {
    path: 'sincronizacion-panama',
    loadComponent: () =>
      import('./pages/sincronizacion-panama-page/sincronizacion-panama-page.component').then(
        (m) => m.SincronizacionPanamaPageComponent
      ),
    title: 'Sincronización Panamá'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SincronizacionPanamaRoutingModule {}
