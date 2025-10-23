import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

// Import de componente standalone
import { InventarioTabsComponent } from './components/inventario-tabs/inventario-tabs.component';

const routes: Routes = [
  { path: '', component: InventarioTabsComponent } // /inventario
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class InventarioRoutingModule {}
