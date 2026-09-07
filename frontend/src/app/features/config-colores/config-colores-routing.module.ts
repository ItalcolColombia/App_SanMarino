import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { ConfigColoresMainComponent } from './pages/config-colores-main/config-colores-main.component';

const routes: Routes = [
  { path: '', component: ConfigColoresMainComponent, data: { title: 'Configuración de colores' } },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ConfigColoresRoutingModule {}
