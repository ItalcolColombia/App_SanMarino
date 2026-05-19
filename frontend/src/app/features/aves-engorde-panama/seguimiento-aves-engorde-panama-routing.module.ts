import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoAvesEngordePanamaListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordePanamaFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';

const routes: Routes = [
  { path: '', component: SeguimientoAvesEngordePanamaListComponent, title: 'Seguimiento diario pollo de engorde Panamá' },
  { path: 'nuevo', component: SeguimientoAvesEngordePanamaFormComponent, title: 'Nuevo Seguimiento pollo de engorde Panamá' },
  { path: 'editar/:id', component: SeguimientoAvesEngordePanamaFormComponent, title: 'Editar Seguimiento pollo de engorde Panamá' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoAvesEngordePanamaRoutingModule {}
