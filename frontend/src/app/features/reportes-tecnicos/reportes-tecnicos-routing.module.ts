// src/app/features/reportes-tecnicos/reportes-tecnicos-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReporteTecnicoMainComponent } from './pages/reporte-tecnico-main/reporte-tecnico-main.component';

const routes: Routes = [
  {
    path: '',
    component: ReporteTecnicoMainComponent,
    data: { title: 'Reportes Técnicos' }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ReportesTecnicosRoutingModule {}


