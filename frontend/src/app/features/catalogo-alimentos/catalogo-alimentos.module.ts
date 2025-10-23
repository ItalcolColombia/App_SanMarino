import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { CatalogoAlimentosRoutingModule } from './catalogo-alimentos-routing.module';
import { CatalogoAlimentosListComponent } from './pages/catalogo-alimentos-list/catalogo-alimentos-list.component';
import { CatalogoAlimentosFormComponent } from './pages/catalogo-alimentos-form/catalogo-alimentos-form.component';

@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    CatalogoAlimentosRoutingModule,
    CatalogoAlimentosListComponent,
    CatalogoAlimentosFormComponent
  ], exports: [
    CatalogoAlimentosListComponent  // 👈 **EXPORTAR**
  ]
})
export class CatalogoAlimentosModule {}
