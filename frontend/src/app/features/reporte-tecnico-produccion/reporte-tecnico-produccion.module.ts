// src/app/features/reporte-tecnico-produccion/reporte-tecnico-produccion.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoProduccionRoutingModule } from './reporte-tecnico-produccion-routing.module';
import { ReporteTecnicoProduccionMainComponent } from './pages/reporte-tecnico-produccion-main/reporte-tecnico-produccion-main.component';

@NgModule({
  imports: [
    CommonModule,
    ReporteTecnicoProduccionRoutingModule,
    ReporteTecnicoProduccionMainComponent
  ]
})
export class ReporteTecnicoProduccionModule {}
