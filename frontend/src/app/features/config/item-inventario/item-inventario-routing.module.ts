import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ItemInventarioListComponent } from './pages/item-inventario-list/item-inventario-list.component';
import { ItemInventarioFormComponent } from './pages/item-inventario-form/item-inventario-form.component';

const routes: Routes = [
  { path: '', component: ItemInventarioListComponent },
  { path: 'nuevo', component: ItemInventarioFormComponent },
  { path: 'editar/:id', component: ItemInventarioFormComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ItemInventarioRoutingModule {}
