// src/app/features/reporte-tecnico-administrativo/reporte-tecnico-administrativo-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ReporteTecnicoAdministrativoMainComponent } from './pages/reporte-tecnico-administrativo-main/reporte-tecnico-administrativo-main.component';

const routes: Routes = [
  {
    path: '',
    component: ReporteTecnicoAdministrativoMainComponent,
    data: { title: 'Reporte Técnico Administrativo SanMarino' }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ReporteTecnicoAdministrativoRoutingModule {}
