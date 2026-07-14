import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'inicio', pathMatch: 'full' },
  {
    path: 'inicio',
    loadComponent: () =>
      import('./pages/migraciones-masivas-page/migraciones-masivas-page.component').then(
        (m) => m.MigracionesMasivasPageComponent
      ),
    title: 'Migraciones Masivas'
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class MigracionesMasivasRoutingModule {}
