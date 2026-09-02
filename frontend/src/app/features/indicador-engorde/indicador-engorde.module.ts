// frontend/src/app/features/indicador-engorde/indicador-engorde.module.ts
import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

@NgModule({
  imports: [
    RouterModule.forChild([
      {
        path: '',
        // Carga lazy del componente standalone
        loadComponent: () =>
          import('./pages/indicador-engorde-list/indicador-engorde-list.component')
            .then(m => m.IndicadorEngordeListComponent),
        title: 'Indicador Ecuador'
      }
    ])
  ]
})
export class IndicadorEngordeModule {}
