import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { SeguimientoAvesEngordeRoutingModule } from './seguimiento-aves-engorde-routing.module';
import { SeguimientoAvesEngordeListComponent } from './pages/seguimiento-aves-engorde-list/seguimiento-aves-engorde-list.component';
import { SeguimientoAvesEngordeFormComponent } from './pages/seguimiento-aves-engorde-form/seguimiento-aves-engorde-form.component';

@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SeguimientoAvesEngordeRoutingModule,
    SeguimientoAvesEngordeListComponent,
    SeguimientoAvesEngordeFormComponent
  ]
})
export class SeguimientoAvesEngordeModule {}
