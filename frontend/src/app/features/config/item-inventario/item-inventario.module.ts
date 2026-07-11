import { NgModule } from '@angular/core';
import { ItemInventarioRoutingModule } from './item-inventario-routing.module';
import { ItemInventarioListComponent } from './pages/item-inventario-list/item-inventario-list.component';
import { ItemInventarioFormComponent } from './pages/item-inventario-form/item-inventario-form.component';

@NgModule({
  imports: [
    ItemInventarioRoutingModule,
    ItemInventarioListComponent,
    ItemInventarioFormComponent
  ]
})
export class ItemInventarioModule {}
