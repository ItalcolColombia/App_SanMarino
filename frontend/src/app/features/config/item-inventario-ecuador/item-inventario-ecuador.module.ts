import { NgModule } from '@angular/core';
import { ItemInventarioEcuadorRoutingModule } from './item-inventario-ecuador-routing.module';
import { ItemInventarioEcuadorListComponent } from './pages/item-inventario-ecuador-list/item-inventario-ecuador-list.component';
import { ItemInventarioEcuadorFormComponent } from './pages/item-inventario-ecuador-form/item-inventario-ecuador-form.component';

@NgModule({
  imports: [
    ItemInventarioEcuadorRoutingModule,
    ItemInventarioEcuadorListComponent,
    ItemInventarioEcuadorFormComponent
  ]
})
export class ItemInventarioEcuadorModule {}
