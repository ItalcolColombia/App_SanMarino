import { NgModule } from '@angular/core';
import { GestionInventarioRoutingModule } from './gestion-inventario-routing.module';
import { GestionInventarioPageComponent } from './pages/gestion-inventario-page/gestion-inventario-page.component';
import { InventarioHistorialPageComponent } from './pages/inventario-historial-page/inventario-historial-page.component';

@NgModule({
  imports: [
    GestionInventarioRoutingModule,
    GestionInventarioPageComponent,
    InventarioHistorialPageComponent,
  ]
})
export class GestionInventarioModule {}
