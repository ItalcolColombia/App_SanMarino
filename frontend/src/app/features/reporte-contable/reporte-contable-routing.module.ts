// src/app/features/reporte-contable/reporte-contable-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReporteContableMainComponent } from './pages/reporte-contable-main/reporte-contable-main.component';

const routes: Routes = [
  {
    path: '',
    component: ReporteContableMainComponent,
    data: { title: 'Reporte Contable' }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ReporteContableRoutingModule {}

