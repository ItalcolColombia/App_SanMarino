// lote-levante/seguimiento-lote-levante-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoLoteLevanteListComponent } from './pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component';

const routes: Routes = [
  { path: '', component: SeguimientoLoteLevanteListComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoLoteLevanteRoutingModule {}
