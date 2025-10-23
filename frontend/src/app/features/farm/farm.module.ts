import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';

import { FarmRoutingModule } from './farm-routing.module';
import { FarmListComponent } from './components/farm-list/farm-list.component';
import { FarmFormComponent } from './components/farm-form/farm-form.component';

@NgModule({
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FontAwesomeModule,
    FarmRoutingModule,

    // ✅ Se importan porque son standalone
    FarmListComponent,
    FarmFormComponent
  ]
})
export class FarmModule {}
