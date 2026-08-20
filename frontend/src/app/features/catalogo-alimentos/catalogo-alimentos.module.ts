import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { CatalogoAlimentosRoutingModule } from './catalogo-alimentos-routing.module';
import { CatalogoAlimentosListComponent } from './pages/catalogo-alimentos-list/catalogo-alimentos-list.component';

@NgModule({
  declarations: [],
  imports: [
    CommonModule,
    CatalogoAlimentosRoutingModule,
    CatalogoAlimentosListComponent
  ], exports: [
    CatalogoAlimentosListComponent  // 👈 **EXPORTAR**
  ]
})
export class CatalogoAlimentosModule {}
