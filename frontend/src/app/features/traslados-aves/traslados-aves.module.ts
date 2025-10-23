import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { TrasladosAvesRoutingModule } from './traslados-aves-routing.module';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TrasladosAvesRoutingModule
  ]
})
export class TrasladosAvesModule { }
