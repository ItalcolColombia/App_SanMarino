import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { CatalogoAlimentosListComponent } from './pages/catalogo-alimentos-list/catalogo-alimentos-list.component';
import { CatalogoAlimentosFormComponent } from './pages/catalogo-alimentos-form/catalogo-alimentos-form.component';

const routes: Routes = [
  { path: '', component: CatalogoAlimentosListComponent },
  { path: 'nuevo', component: CatalogoAlimentosFormComponent },
  { path: 'editar/:id', component: CatalogoAlimentosFormComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CatalogoAlimentosRoutingModule {}
