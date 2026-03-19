import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ItemInventarioEcuadorListComponent } from './pages/item-inventario-ecuador-list/item-inventario-ecuador-list.component';
import { ItemInventarioEcuadorFormComponent } from './pages/item-inventario-ecuador-form/item-inventario-ecuador-form.component';

const routes: Routes = [
  { path: '', component: ItemInventarioEcuadorListComponent },
  { path: 'nuevo', component: ItemInventarioEcuadorFormComponent },
  { path: 'editar/:id', component: ItemInventarioEcuadorFormComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ItemInventarioEcuadorRoutingModule {}
