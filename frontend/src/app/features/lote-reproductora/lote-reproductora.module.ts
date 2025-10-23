import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

@NgModule({
  imports: [
    RouterModule.forChild([
      {
        path: '',
        // Carga DIRECTA del standalone (no necesitas routing module aparte)
        loadComponent: () =>
          import('./pages/lote-reproductora-list/lote-reproductora-list.component')
            .then(m => m.LoteReproductoraListComponent),
        title: 'Lotes Reproductora'
      }
    ])
  ]
})
export class LoteReproductoraModule {}
