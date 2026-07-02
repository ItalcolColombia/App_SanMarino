import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { SeguimientoAvesEngordePanamaListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordePanamaFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';
import { SeguimientoEngordeCrudApi, ENGORDE_FORM_OPCIONES } from '../engorde-comun/services/seguimiento-engorde-crud.api';
import { SeguimientoAvesEngordePanamaService } from './services/seguimiento-aves-engorde-panama.service';

// Panamá provee su servicio al form compartido y activa los campos QQ (quintales).
const formProviders = [
  { provide: SeguimientoEngordeCrudApi, useExisting: SeguimientoAvesEngordePanamaService },
  { provide: ENGORDE_FORM_OPCIONES, useValue: { mostrarQq: true } }
];

const routes: Routes = [
  { path: '', component: SeguimientoAvesEngordePanamaListComponent, title: 'Seguimiento diario pollo de engorde Panamá' },
  { path: 'nuevo', component: SeguimientoAvesEngordePanamaFormComponent, title: 'Nuevo Seguimiento pollo de engorde Panamá', providers: formProviders },
  { path: 'editar/:id', component: SeguimientoAvesEngordePanamaFormComponent, title: 'Editar Seguimiento pollo de engorde Panamá', providers: formProviders }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class SeguimientoAvesEngordePanamaRoutingModule {}
