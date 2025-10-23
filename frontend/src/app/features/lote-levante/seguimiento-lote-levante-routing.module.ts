// lote-levante/seguimiento-lote-levante-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoLoteLevanteListComponent } from './pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component';
import { SeguimientoLoteLevanteFormComponent } from './pages/seguimiento-lote-form/seguimiento-lote-levante-form.component';
import { SeguimientoLoteLevanteService } from './services/seguimiento-lote-levante.service';

const routes: Routes = [
  { path: '', component: SeguimientoLoteLevanteListComponent },
  { path: 'nuevo', component: SeguimientoLoteLevanteFormComponent },
  { path: 'editar/:id', component: SeguimientoLoteLevanteFormComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoLoteLevanteRoutingModule {}
