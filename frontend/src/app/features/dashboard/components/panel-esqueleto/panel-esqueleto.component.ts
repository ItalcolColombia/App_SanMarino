import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/**
 * Esqueleto de carga de un panel.
 *
 * Es lo que se dibuja en el `@placeholder` / `@loading` de cada `@defer`: reserva el alto del panel
 * para que la página no salte cuando el contenido llega. Presentacional puro ⇒ `OnPush`.
 */
@Component({
  selector: 'app-panel-esqueleto',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./panel-esqueleto.component.scss'],
  template: `
    <div class="esqueleto" [style.min-height.px]="alto" role="status" [attr.aria-label]="'Cargando ' + titulo">
      <div class="esqueleto__barra esqueleto__barra--titulo"></div>
      <div class="esqueleto__grid">
        @for (_ of celdas; track $index) {
          <div class="esqueleto__barra esqueleto__barra--celda"></div>
        }
      </div>
    </div>
  `
})
export class PanelEsqueletoComponent {
  @Input() titulo = 'panel';
  @Input() alto = 180;
  /** Cuántas celdas grises dibujar. Array fijo: no se recrea por ciclo de detección. */
  @Input() celdas: readonly number[] = [1, 2, 3, 4];
}
