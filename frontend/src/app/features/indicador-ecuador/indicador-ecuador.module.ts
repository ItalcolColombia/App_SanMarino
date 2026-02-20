// frontend/src/app/features/indicador-ecuador/indicador-ecuador.module.ts
import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

@NgModule({
  imports: [
    RouterModule.forChild([
      {
        path: '',
        // Carga lazy del componente standalone
        loadComponent: () =>
          import('./pages/indicador-ecuador-list/indicador-ecuador-list.component')
            .then(m => m.IndicadorEcuadorListComponent),
        title: 'Indicador Ecuador'
      }
    ])
  ]
})
export class IndicadorEcuadorModule {}
