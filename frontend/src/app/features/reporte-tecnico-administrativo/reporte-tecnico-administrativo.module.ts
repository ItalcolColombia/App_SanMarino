// src/app/features/reporte-tecnico-administrativo/reporte-tecnico-administrativo.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoAdministrativoRoutingModule } from './reporte-tecnico-administrativo-routing.module';
import { ReporteTecnicoAdministrativoMainComponent } from './pages/reporte-tecnico-administrativo-main/reporte-tecnico-administrativo-main.component';

@NgModule({
  imports: [
    CommonModule,
    ReporteTecnicoAdministrativoRoutingModule,
    ReporteTecnicoAdministrativoMainComponent
  ]
})
export class ReporteTecnicoAdministrativoModule {}
