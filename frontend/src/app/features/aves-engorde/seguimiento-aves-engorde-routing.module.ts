import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoAvesEngordeListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordeFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';
import { SeguimientoEngordeCrudApi } from '../engorde-comun/services/seguimiento-engorde-crud.api';
import { SeguimientoAvesEngordeService } from './services/seguimiento-aves-engorde.service';

// Colombia provee su servicio al form compartido de engorde-comun.
// ENGORDE_FORM_OPCIONES usa su default ({ mostrarQq: false }).
const formProviders = [{ provide: SeguimientoEngordeCrudApi, useExisting: SeguimientoAvesEngordeService }];

const routes: Routes = [
  { path: '', component: SeguimientoAvesEngordeListComponent, title: 'Seguimiento diario pollo de engorde' },
  { path: 'nuevo', component: SeguimientoAvesEngordeFormComponent, title: 'Nuevo Seguimiento diario pollo de engorde', providers: formProviders },
  { path: 'editar/:id', component: SeguimientoAvesEngordeFormComponent, title: 'Editar Seguimiento diario pollo de engorde', providers: formProviders }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoAvesEngordeRoutingModule {}
