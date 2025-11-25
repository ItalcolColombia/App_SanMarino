// src/app/features/reportes-tecnicos/reportes-tecnicos.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportesTecnicosRoutingModule } from './reportes-tecnicos-routing.module';
import { ReporteTecnicoMainComponent } from './pages/reporte-tecnico-main/reporte-tecnico-main.component';

@NgModule({
  imports: [
    CommonModule,
    ReportesTecnicosRoutingModule,
    ReporteTecnicoMainComponent
  ]
})
export class ReportesTecnicosModule {}


