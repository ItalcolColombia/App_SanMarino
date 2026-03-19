import { NgModule } from '@angular/core';
import { GestionInventarioRoutingModule } from './gestion-inventario-routing.module';
import { GestionInventarioPageComponent } from './pages/gestion-inventario-page/gestion-inventario-page.component';

@NgModule({
  imports: [
    GestionInventarioRoutingModule,
    GestionInventarioPageComponent
  ]
})
export class GestionInventarioModule {}
