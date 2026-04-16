import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { GestionInventarioPageComponent } from './pages/gestion-inventario-page/gestion-inventario-page.component';
import { InventarioHistorialPageComponent } from './pages/inventario-historial-page/inventario-historial-page.component';

const routes: Routes = [
  { path: '', component: GestionInventarioPageComponent },
  { path: 'historial', component: InventarioHistorialPageComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class GestionInventarioRoutingModule {}
