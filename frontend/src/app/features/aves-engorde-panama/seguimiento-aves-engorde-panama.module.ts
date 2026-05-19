import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { SeguimientoAvesEngordePanamaRoutingModule } from './seguimiento-aves-engorde-panama-routing.module';
import { SeguimientoAvesEngordePanamaListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordePanamaFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';

@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SeguimientoAvesEngordePanamaRoutingModule,
    SeguimientoAvesEngordePanamaListComponent,
    SeguimientoAvesEngordePanamaFormComponent
  ]
})
export class SeguimientoAvesEngordePanamaModule {}
