import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoAvesEngordeListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordeFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';

const routes: Routes = [
  { path: '', component: SeguimientoAvesEngordeListComponent, title: 'Seguimiento diario pollo de engorde' },
  { path: 'nuevo', component: SeguimientoAvesEngordeFormComponent, title: 'Nuevo Seguimiento diario pollo de engorde' },
  { path: 'editar/:id', component: SeguimientoAvesEngordeFormComponent, title: 'Editar Seguimiento diario pollo de engorde' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoAvesEngordeRoutingModule {}
