// src/app/features/reporte-contable/reporte-contable.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteContableRoutingModule } from './reporte-contable-routing.module';
import { ReporteContableMainComponent } from './pages/reporte-contable-main/reporte-contable-main.component';

@NgModule({
  imports: [
    CommonModule,
    ReporteContableRoutingModule,
    ReporteContableMainComponent
  ]
})
export class ReporteContableModule {}

