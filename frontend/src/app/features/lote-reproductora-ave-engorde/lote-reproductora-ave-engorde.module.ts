import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

@NgModule({
  imports: [
    RouterModule.forChild([
      {
        path: '',
        loadComponent: () =>
          import('./pages/lote-reproductora-ave-engorde-list/lote-reproductora-ave-engorde-list.component')
            .then(m => m.LoteReproductoraAveEngordeListComponent),
        title: 'Lote Reproductora Aves de Engorde'
      }
    ])
  ]
})
export class LoteReproductoraAveEngordeModule {}
