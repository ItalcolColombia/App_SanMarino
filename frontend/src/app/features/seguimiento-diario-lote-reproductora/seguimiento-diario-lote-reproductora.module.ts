import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

@NgModule({
  imports: [
    RouterModule.forChild([
      {
        path: '',
        loadComponent: () =>
          import('./pages/seguimiento-diario-lote-reproductora-list/seguimiento-diario-lote-reproductora-list.component')
            .then(m => m.SeguimientoDiarioLoteReproductoraListComponent),
        title: 'Seguimiento Diario Lote Reproductora'
      }
    ])
  ]
})
export class SeguimientoDiarioLoteReproductoraModule {}
