import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { CatalogoAlimentosListComponent } from './pages/catalogo-alimentos-list/catalogo-alimentos-list.component';

const routes: Routes = [
  { path: '', component: CatalogoAlimentosListComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CatalogoAlimentosRoutingModule {}
