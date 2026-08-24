// lote-levante/seguimiento-lote-levante.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { SeguimientoLoteLevanteRoutingModule } from './seguimiento-lote-levante-routing.module';

import { SeguimientoLoteLevanteListComponent } from './pages/seguimiento-lote-levante-list/seguimiento-lote-levante-list.component';

@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SeguimientoLoteLevanteRoutingModule,
    SeguimientoLoteLevanteListComponent
  ]
})
export class SeguimientoLoteLevanteModule {}
