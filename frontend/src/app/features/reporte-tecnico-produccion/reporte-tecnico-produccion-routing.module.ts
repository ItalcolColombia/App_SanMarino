// src/app/features/reporte-tecnico-produccion/reporte-tecnico-produccion-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReporteTecnicoProduccionMainComponent } from './pages/reporte-tecnico-produccion-main/reporte-tecnico-produccion-main.component';

const routes: Routes = [
  {
    path: '',
    component: ReporteTecnicoProduccionMainComponent,
    data: { title: 'Reporte Técnico Producción SanMarino' }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ReporteTecnicoProduccionRoutingModule {}
