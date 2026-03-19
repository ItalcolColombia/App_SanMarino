import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { GestionInventarioPageComponent } from './pages/gestion-inventario-page/gestion-inventario-page.component';

const routes: Routes = [
  { path: '', component: GestionInventarioPageComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class GestionInventarioRoutingModule {}
